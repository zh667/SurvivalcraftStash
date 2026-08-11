using Engine;
using Game.NetWork;
using Stash.Game;

namespace Game;

/// <summary>
/// 联机版的存储枢纽行为：客户端发坐标请求，服务端扫网络回库存 Id 列表。
/// </summary>
public class SubsystemStashHubBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => new[] { StashHubBlock.Index };

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (Terrain.ExtractContents(raycastResult.Value) != StashHubBlock.Index)
        {
            return false;
        }

        var point = new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);

        if (CommonLib.WorkType == WorkType.Client && CommonLib.MainPlayer == componentMiner.ComponentPlayer)
        {
            CommonLib.Net.QueuePackage(new StashOpenTerminalPackage(point));
            return true;
        }

        if (componentMiner.ComponentPlayer == null || !componentMiner.ComponentPlayer.PlayerData.IsMainPlayer)
        {
            return true;
        }

        return StashHubCore.OpenTerminal(componentMiner.ComponentPlayer, StashHubCore.ScanInventories(Project, point));
    }
}
