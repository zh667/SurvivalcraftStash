using Engine;
using Game;
using GameEntitySystem;
using Stash.Shared.Storage;

namespace Stash.Game;

/// <summary>
/// 无线终端的使用逻辑：右键存储终端方块 = 绑定；右键别处 = 远程打开已绑定的终端。
/// </summary>
public static class StashWirelessUse
{
    /// <summary>远程能连多远（格）。0 表示不限制。</summary>
    public const float MaxRange = 0f;

    public sealed record Result(bool Consumed, bool Bound, int HubId);

    /// <summary>
    /// 处理一次使用。返回 Consumed=true 表示这次右键归我们管（调用方不要再走原版逻辑）。
    /// 只在权威端调用。
    /// </summary>
    public static Result Use(
        Project project,
        SubsystemTerrain terrain,
        ComponentMiner miner,
        TerrainRaycastResult? hit,
        int heldValue)
    {
        ComponentPlayer? player = miner.ComponentPlayer;
        if (player == null)
        {
            return new Result(false, false, 0);
        }

        // 指着存储终端 → 绑定到它
        if (hit is { } raycast)
        {
            var point = new Point3(raycast.CellFace.X, raycast.CellFace.Y, raycast.CellFace.Z);
            if (Terrain.ExtractContents(terrain.Terrain.GetCellValue(point.X, point.Y, point.Z)) == StashHubBlock.Index)
            {
                StashHubRecord record = StashHubNaming.Register(point);
                StashStore.Save();
                Notify(player, StashText.WirelessBoundTo(record.Name));
                return new Result(true, true, record.Id);
            }
        }

        // 否则 → 远程打开已绑定的终端
        int hubId = StashWirelessTerminalBlock.GetBoundHubId(heldValue);
        if (hubId <= 0)
        {
            Notify(player, StashText.WirelessNotBound);
            return new Result(true, false, 0);
        }

        StashHubRecord? hub = StashHubNaming.Find(hubId);
        if (hub == null)
        {
            Notify(player, StashText.WirelessHubGone);
            return new Result(true, false, 0);
        }

        var hubPoint = new Point3(hub.X, hub.Y, hub.Z);
        if (Terrain.ExtractContents(terrain.Terrain.GetCellValue(hubPoint.X, hubPoint.Y, hubPoint.Z)) != StashHubBlock.Index)
        {
            // 终端被挖了：把登记也清掉，免得留一堆连不上的号。
            StashHubNaming.Forget(hubPoint);
            Notify(player, StashText.WirelessHubGone);
            return new Result(true, false, 0);
        }

        return new Result(true, false, hubId);
    }

    /// <summary>拿到 hubId 后真正开界面（客户端一侧执行）。</summary>
    public static bool OpenRemote(ComponentPlayer player, Project project, int hubId)
    {
        StashHubRecord? hub = StashHubNaming.Find(hubId);
        if (hub == null)
        {
            Notify(player, StashText.WirelessHubGone);
            return true;
        }

        List<IInventory> containers = StashHubCore.ScanInventories(project, new Point3(hub.X, hub.Y, hub.Z));
        return StashHubCore.OpenTerminal(player, containers, hub.Name);
    }

    private static void Notify(ComponentPlayer player, string message) =>
        player.ComponentGui?.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
}
