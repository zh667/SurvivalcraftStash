using Engine;
using Stash.Game;

namespace Game;

/// <summary>
/// 插件版的存储枢纽行为。单人游戏，扫完直接开终端。
/// </summary>
public class SubsystemStashHubBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => new[] { StashHubBlock.Index };

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (Terrain.ExtractContents(raycastResult.Value) != StashHubBlock.Index || componentMiner.ComponentPlayer == null)
        {
            return false;
        }

        var point = new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        return StashHubCore.OpenTerminal(componentMiner.ComponentPlayer, StashHubCore.ScanInventories(Project, point));
    }
}
