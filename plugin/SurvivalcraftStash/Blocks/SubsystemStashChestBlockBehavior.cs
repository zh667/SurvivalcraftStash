using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 插件版的分级箱子行为。单人游戏，直接开界面，不需要往返包。
/// </summary>
public class SubsystemStashChestBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities m_blockEntities = null!;
    private SubsystemAudio m_audio = null!;

    public override int[] HandledBlocks =>
        StashChestTiers.All.Select(tier => tier.Index).ToArray();

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        m_blockEntities = Project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true);
        m_audio = Project.FindSubsystem<SubsystemAudio>(throwOnError: true);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) =>
        StashChestCore.OnChestAdded(Project, m_blockEntities, value, x, y, z);

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        StashChestCore.OnChestRemoved(Project, m_blockEntities, x, y, z);

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        StashChestTier? tier = StashChestTiers.ByBlockIndex(Terrain.ExtractContents(raycastResult.Value));
        if (tier == null || componentMiner.ComponentPlayer == null)
        {
            return false;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        var chest = blockEntity?.Entity.FindComponent<ComponentStashChest>(throwOnError: false);
        if (chest == null)
        {
            return false;
        }

        return StashChestCore.OpenChestUi(componentMiner.ComponentPlayer, chest, tier);
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (worldItem.ToRemove)
        {
            return;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(cellFace.X, cellFace.Y, cellFace.Z);
        var chest = blockEntity?.Entity.FindComponent<ComponentStashChest>(throwOnError: false);
        if (chest == null)
        {
            return;
        }

        int count = (worldItem as Pickable)?.Count ?? 1;
        int left = ComponentInventoryBase.AcquireItems(chest, worldItem.Value, count);
        if (left < count)
        {
            m_audio.PlaySound("Audio/PickableCollected", 1f, 0f, worldItem.Position, 3f, autoDelay: true);
        }

        if (left <= 0)
        {
            worldItem.ToRemove = true;
        }
        else if (worldItem is Pickable pickable)
        {
            pickable.Count = left;
        }
    }
}
