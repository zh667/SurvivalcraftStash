namespace Stash.Shared.Storage;

/// <summary>一个已登记的存储终端：编号、名字、坐标。</summary>
public sealed class StashHubRecord
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }
}

/// <summary>
/// 存储终端登记簿。无线终端靠**编号**认人，所以每个终端方块放下后要有个稳定的号。
///
/// 为什么不把编号存进方块的 data 位：终端方块的 data 位要留给朝向等用途，
/// 而且方块被挖掉重放会丢；按坐标登记在世界数据里更稳，也方便玩家改名。
/// </summary>
public sealed class StashHubRegistry
{
    public int NextId { get; set; } = 1;

    /// <summary>键是 "x,y,z"，只为 JSON 友好。</summary>
    public Dictionary<string, StashHubRecord> Hubs { get; set; } = new();

    public static string Key(int x, int y, int z) => $"{x},{y},{z}";

    public StashHubRecord GetOrCreate(int x, int y, int z, Func<int, string> defaultName)
    {
        string key = Key(x, y, z);
        if (Hubs.TryGetValue(key, out StashHubRecord? record))
        {
            return record;
        }

        record = new StashHubRecord
        {
            Id = NextId++,
            X = x,
            Y = y,
            Z = z,
        };
        record.Name = defaultName(record.Id);
        Hubs[key] = record;
        return record;
    }

    public StashHubRecord? Find(int id)
    {
        foreach (StashHubRecord record in Hubs.Values)
        {
            if (record.Id == id)
            {
                return record;
            }
        }

        return null;
    }

    public StashHubRecord? At(int x, int y, int z) =>
        Hubs.TryGetValue(Key(x, y, z), out StashHubRecord? record) ? record : null;

    public void Remove(int x, int y, int z) => Hubs.Remove(Key(x, y, z));
}
