using Engine;
using Stash.Shared.Storage;

namespace Stash.Game;

/// <summary>
/// 存储终端的编号与命名。
///
/// 玩家不改名时按放置顺序叫"存储终端1、存储终端2…"；无线终端绑定后
/// 自己的名字变成"存储终端1的远程终端"，一眼能看出连的是哪一个。
/// </summary>
public static class StashHubNaming
{
    /// <summary>取（必要时登记）这个坐标上的终端记录。</summary>
    public static StashHubRecord Register(Point3 point) =>
        StashStore.Data.Hubs.GetOrCreate(point.X, point.Y, point.Z, StashText.DefaultHubName);

    public static StashHubRecord? Find(int hubId) => StashStore.Data.Hubs.Find(hubId);

    public static StashHubRecord? At(Point3 point) => StashStore.Data.Hubs.At(point.X, point.Y, point.Z);

    public static void Forget(Point3 point)
    {
        StashStore.Data.Hubs.Remove(point.X, point.Y, point.Z);
        StashStore.Save();
    }

    public static void Rename(StashHubRecord record, string name)
    {
        record.Name = string.IsNullOrWhiteSpace(name) ? StashText.DefaultHubName(record.Id) : name.Trim();
        StashStore.Save();
    }
}
