using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 插件版的分级熔炉行为。单人游戏，直接开界面，不需要往返包。
/// </summary>
public class SubsystemStashFurnaceBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities m_blockEntities = null!;

    public override int[] HandledBlocks =>
        StashFurnaceTiers.All.Select(tier => tier.Index).ToArray();

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        m_blockEntities = Project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) =>
        StashFurnaceCore.OnFurnaceAdded(Project, m_blockEntities, value, x, y, z);

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        StashFurnaceCore.OnFurnaceRemoved(Project, m_blockEntities, x, y, z);

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        StashFurnaceTier? tier = StashFurnaceTiers.ByBlockIndex(Terrain.ExtractContents(raycastResult.Value));
        if (tier == null || componentMiner.ComponentPlayer == null)
        {
            return false;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        var furnace = blockEntity?.Entity.FindComponent<ComponentFurnace>(throwOnError: false);
        if (furnace == null)
        {
            return false;
        }

        return StashFurnaceCore.OpenFurnaceUi(componentMiner.ComponentPlayer, furnace, tier);
    }
}
