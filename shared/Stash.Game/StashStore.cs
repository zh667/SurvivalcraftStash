using System.Text.Json;
using Engine;
using Game;
using Stash.Shared.Storage;

namespace Stash.Game;

/// <summary>
/// 世界级数据的读写。落在世界目录下的 <c>SurvivalcraftStash.json</c>，
/// 和 SurvivalcraftRuins 用的是同一套 <see cref="Storage"/> 路径写法（已在真机验证过）。
/// </summary>
public static class StashStore
{
    public const string FileName = "SurvivalcraftStash.json";

    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private static StashWorldData? s_data;
    private static string? s_path;

    /// <summary>
    /// 留着 gameInfo 是为了**保存时再解析一次路径**。
    ///
    /// 之前只在 Load 里算一次路径：如果那一刻 <c>DirectoryName</c> 还是空的，
    /// 就直接 return，<c>s_path</c> 永远是 null，之后每次 Save 都静默什么也不做，
    /// 而且一句日志都不留。实机表现是"退出世界再进来，无线终端说绑定的终端不存在"——
    /// 登记簿根本没落过盘。
    /// </summary>
    private static SubsystemGameInfo? s_gameInfo;

    public static StashWorldData Data
    {
        get
        {
            EnsureLoaded();
            return s_data ??= new StashWorldData();
        }
    }

    public static PlayerStashData ForCurrentPlayer() =>
        Data.GetOrCreate(StashPlatform.IsReady ? StashPlatform.Current.CurrentPlayerKey : StashWorldData.SinglePlayerKey);

    /// <summary>进入世界时调用。世界身份对不上就当作全新数据，并把旧文件另存备份。</summary>
    public static void Load(SubsystemGameInfo gameInfo)
    {
        s_data = null;
        s_path = null;
        s_gameInfo = gameInfo;

        if (gameInfo == null)
        {
            s_data = new StashWorldData();
            Log.Warning("[Stash] 没有 SubsystemGameInfo，世界数据这一轮不会落盘。");
            return;
        }

        string worldId = gameInfo.WorldSettings?.Name ?? string.Empty;
        string seed = gameInfo.WorldSeed.ToString();
        s_path = ResolvePath();

        if (s_path == null)
        {
            // 目录名还没准备好也不要紧：Save 的时候会再解析一次。
            s_data = new StashWorldData { WorldId = worldId, Seed = seed };
            Log.Warning("[Stash] 世界目录名还是空的，读取推迟到保存时再解析路径。");
            return;
        }

        try
        {
            if (Storage.FileExists(s_path))
            {
                using Stream stream = Storage.OpenFile(s_path, OpenFileMode.Read);
                using var reader = new StreamReader(stream);
                StashWorldData? loaded = JsonSerializer.Deserialize<StashWorldData>(reader.ReadToEnd(), s_json);

                if (loaded != null && loaded.Matches(worldId, seed))
                {
                    s_data = loaded;
                }
                else if (loaded != null)
                {
                    // SC 会回收复用世界文件夹名，这里对不上说明这份数据属于另一个世界。
                    BackupMismatched();
                    Log.Warning("[Stash] 世界身份不匹配，已把旧数据另存备份并重新开始。");
                }
            }
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 读取 {FileName} 失败：{exception.Message}");
        }

        s_data ??= new StashWorldData();
        s_data.WorldId = worldId;
        s_data.Seed = seed;

        Log.Information($"[Stash] 世界数据：{s_path}，存储终端登记 {s_data.Hubs.Hubs.Count} 条");
    }

    public static void Save()
    {
        EnsureLoaded();
        if (s_data == null)
        {
            return;
        }

        // 这里再解析一次：Load 那会儿目录名可能还没准备好。
        s_path ??= ResolvePath();
        if (s_path == null)
        {
            Log.Warning($"[Stash] 拿不到世界目录，{FileName} 没能保存。");
            return;
        }

        try
        {
            using Stream stream = Storage.OpenFile(s_path, OpenFileMode.Create);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonSerializer.Serialize(s_data, s_json));
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 写入 {FileName} 失败：{exception.Message}");
        }
    }

    public static void Unload()
    {
        s_data = null;
        s_path = null;
        s_gameInfo = null;
    }

    /// <summary>
    /// 解析世界数据文件的路径。
    ///
    /// 先用 Load 时拿到的那个 <c>SubsystemGameInfo</c>；拿不到就**现问平台要一个**——
    /// 实机日志显示 <c>OnLoadingFinished</c> 那一刻 <c>GameManager.Project</c> 还是 null，
    /// 于是 Load 整段被跳过（连日志都没打），后面每次保存都只能报"拿不到世界目录"。
    /// </summary>
    private static string? ResolvePath()
    {
        s_gameInfo ??= StashPlatform.IsReady ? StashPlatform.Current.FindGameInfo() : null;

        string? directory = s_gameInfo?.DirectoryName;
        return string.IsNullOrEmpty(directory) ? null : Storage.CombinePaths(directory, FileName);
    }

    /// <summary>
    /// 世界数据还没读过就补读一次。
    ///
    /// 进世界的钩子不保证能拿到 <c>SubsystemGameInfo</c>，所以任何一次读写之前都补一刀；
    /// 读成功后 <see cref="s_path"/> 就有值了，之后不会再走这里。
    /// </summary>
    private static void EnsureLoaded()
    {
        if (s_path != null || !StashPlatform.IsReady)
        {
            return;
        }

        if (StashPlatform.Current.FindGameInfo() is not { } gameInfo || string.IsNullOrEmpty(gameInfo.DirectoryName))
        {
            return;
        }

        // 内存里已经攒了东西（比如刚登记的终端还没落盘）就不能重读——那会把它冲掉。
        // 这种情况只补一个路径，内容原样保留，下一次 Save 就能写出去。
        if (s_data != null && (s_data.Hubs.Hubs.Count > 0 || s_data.Players.Count > 0))
        {
            s_gameInfo = gameInfo;
            s_path = ResolvePath();
            Log.Information($"[Stash] 补上了世界数据路径：{s_path}（内存里已有数据，不重读）");
            return;
        }

        Log.Information("[Stash] 进世界时没拿到 SubsystemGameInfo，现在补读世界数据。");
        Load(gameInfo);
    }

    private static void BackupMismatched()
    {
        if (s_path == null)
        {
            return;
        }

        try
        {
            string backup = s_path + ".mismatched";
            using Stream source = Storage.OpenFile(s_path, OpenFileMode.Read);
            using Stream target = Storage.OpenFile(backup, OpenFileMode.Create);
            source.CopyTo(target);
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 备份旧数据失败：{exception.Message}");
        }
    }
}
