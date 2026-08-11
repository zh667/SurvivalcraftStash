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

    public static StashWorldData Data => s_data ??= new StashWorldData();

    public static PlayerStashData ForCurrentPlayer() =>
        Data.GetOrCreate(StashPlatform.IsReady ? StashPlatform.Current.CurrentPlayerKey : StashWorldData.SinglePlayerKey);

    /// <summary>进入世界时调用。世界身份对不上就当作全新数据，并把旧文件另存备份。</summary>
    public static void Load(SubsystemGameInfo gameInfo)
    {
        s_data = null;
        s_path = null;

        if (gameInfo == null || string.IsNullOrEmpty(gameInfo.DirectoryName))
        {
            s_data = new StashWorldData();
            return;
        }

        string worldId = gameInfo.WorldSettings?.Name ?? string.Empty;
        string seed = gameInfo.WorldSeed.ToString();
        s_path = Storage.CombinePaths(gameInfo.DirectoryName, FileName);

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
    }

    public static void Save()
    {
        if (s_path == null || s_data == null)
        {
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
