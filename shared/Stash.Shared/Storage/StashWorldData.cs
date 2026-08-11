using Stash.Shared.Sorting;

namespace Stash.Shared.Storage;

/// <summary>
/// 世界级持久化数据。写在世界目录下的 <c>SurvivalcraftStash.json</c>。
///
/// 带 <see cref="WorldId"/> / <see cref="Seed"/> 是因为 SC 会回收复用世界文件夹名——
/// 不校验的话，删掉世界再建一个同名的，上一个世界的数据会串进来（SCTM 踩过这个坑）。
/// </summary>
public sealed class StashWorldData
{
    public string WorldId { get; set; } = string.Empty;

    public string Seed { get; set; } = string.Empty;

    /// <summary>玩家 GUID → 该玩家的数据。单人版固定用 <see cref="SinglePlayerKey"/>。</summary>
    public Dictionary<string, PlayerStashData> Players { get; set; } = new();

    public const string SinglePlayerKey = "single";

    /// <summary>存储终端登记簿（编号 / 名字 / 坐标），无线终端靠它认人。</summary>
    public StashHubRegistry Hubs { get; set; } = new();

    public PlayerStashData GetOrCreate(string playerKey)
    {
        if (string.IsNullOrEmpty(playerKey))
        {
            playerKey = SinglePlayerKey;
        }

        if (!Players.TryGetValue(playerKey, out PlayerStashData? data))
        {
            data = new PlayerStashData();
            Players[playerKey] = data;
        }

        return data;
    }

    /// <summary>世界身份是否对得上。对不上就不能用这份数据。</summary>
    public bool Matches(string worldId, string seed) =>
        string.IsNullOrEmpty(WorldId) && string.IsNullOrEmpty(Seed)
        || (WorldId == worldId && Seed == seed);
}

public sealed class PlayerStashData
{
    /// <summary>玩家自己库存里被锁定的槽位（不参与整理、不被一键存入带走）。</summary>
    public List<int> LockedSlots { get; set; } = new();

    /// <summary>记忆槽位：槽位下标 → 记住的物品值。键是字符串只为 JSON 友好。</summary>
    public Dictionary<string, int> MemorySlots { get; set; } = new();

    public SortMethod SortMethod { get; set; } = SortMethod.CategoryThenDisplayOrder;

    public HashSet<int> LockedSlotSet() => new(LockedSlots);

    public Dictionary<int, int> MemorySlotMap()
    {
        var map = new Dictionary<int, int>();
        foreach ((string key, int value) in MemorySlots)
        {
            if (int.TryParse(key, out int slot))
            {
                map[slot] = value;
            }
        }

        return map;
    }

    public void ToggleLock(int slotIndex)
    {
        if (!LockedSlots.Remove(slotIndex))
        {
            LockedSlots.Add(slotIndex);
            LockedSlots.Sort();
        }
    }

    public void SetMemory(int slotIndex, int value)
    {
        if (value == 0)
        {
            MemorySlots.Remove(slotIndex.ToString());
        }
        else
        {
            MemorySlots[slotIndex.ToString()] = value;
        }
    }
}
