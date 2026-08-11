using Engine;
using Game.NetWork;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 联机版的分级箱子行为。
///
/// 开界面走的是"客户端发请求 → 服务端查库存 Id → 回给该客户端 → 客户端开界面"，
/// 和原版箱子同一套流程（原版用 BlockEditPackage，我们用自己的包）。
/// 不能复用原版那个包：它在客户端侧是按 `is ComponentChest` 硬判类型开 4×4 的原版界面的，
/// 我们的箱子有 32~80 格，套那个界面只能看到前 16 格。
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

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        ComponentBlockEntity? blockEntity = StashChestCore.OnChestAdded(Project, m_blockEntities, value, x, y, z);

        // Owner 是联机版特有的（用于领地权限），原版建箱子时也会写。
        if (blockEntity != null && miner?.ComponentPlayer != null)
        {
            blockEntity.Owner = miner.ComponentPlayer.PlayerGuid;
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        StashChestCore.OnChestRemoved(Project, m_blockEntities, x, y, z);

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        StashChestTier? tier = StashChestTiers.ByBlockIndex(Terrain.ExtractContents(raycastResult.Value));
        if (tier == null)
        {
            return false;
        }

        if (CommonLib.WorkType == WorkType.Client && CommonLib.MainPlayer == componentMiner.ComponentPlayer)
        {
            CommonLib.Net.QueuePackage(new StashOpenChestPackage(
                new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z)));
            return true;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        if (blockEntity == null || componentMiner.ComponentPlayer == null)
        {
            return false;
        }

        IInventory inventory = blockEntity.Entity.FindComponent<ComponentStashChest>(throwOnError: false);
        if (inventory == null)
        {
            return false;
        }

        // 单人/主机：直接开。
        if (componentMiner.ComponentPlayer.PlayerData.IsMainPlayer)
        {
            StashChestCore.OpenChestUi(componentMiner.ComponentPlayer, inventory, tier);
        }

        return true;
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        // 和原版箱子一致：扔进去的东西会被收下。
        if (worldItem.ToRemove)
        {
            return;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(cellFace.X, cellFace.Y, cellFace.Z);
        IInventory inventory = blockEntity?.Entity.FindComponent<ComponentStashChest>(throwOnError: false);
        if (inventory == null)
        {
            return;
        }

        int count = (worldItem as Pickable)?.Count ?? 1;
        int left = ComponentInventoryBase.AcquireItems(inventory, worldItem.Value, count);
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
