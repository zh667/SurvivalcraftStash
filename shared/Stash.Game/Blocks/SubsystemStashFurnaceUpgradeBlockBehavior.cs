using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 熔炉升级件的使用行为：拿着它点熔炉就地升级。
///
/// 和箱子升级件同一个套路——挂在**升级件自己**这个方块上走 <c>OnUse</c>，
/// 因为原版是先按手持物品分发 <c>Use</c>，返回 true 才不会再走 <c>Interact</c>。
/// 挂在熔炉上不行：原版熔炉自己的行为排在前面，会先把熔炉界面打开。
///
/// 这个类两个平台通用：<c>SubsystemBlockBehavior.OnUse</c> 在两版里签名一致。
/// </summary>
public class SubsystemStashFurnaceUpgradeBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities m_blockEntities = null!;
    private SubsystemTerrain m_terrain = null!;

    public override int[] HandledBlocks => new[] { StashFurnaceTiers.UpgradeItemIndex };

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        m_blockEntities = Project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true);
        m_terrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
    }

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        IInventory inventory = componentMiner.Inventory;
        if (inventory == null)
        {
            return false;
        }

        int held = inventory.GetSlotValue(inventory.ActiveSlotIndex);
        if (Terrain.ExtractContents(held) != StashFurnaceTiers.UpgradeItemIndex)
        {
            return false;
        }

        if (componentMiner.Raycast(ray, RaycastMode.Interaction) is not TerrainRaycastResult raycastResult)
        {
            return false;
        }

        int contents = m_terrain.Terrain.GetCellContents(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);

        // 熔炉升级件只对熔炉（原版熔炉或我们的分级熔炉）有反应，其它方块放行给原版逻辑。
        if (contents != StashFurnaceTiers.VanillaFurnaceIndex && !StashFurnaceTiers.IsStashFurnace(contents))
        {
            return false;
        }

        return StashUpgradeUse.TryUseFurnaceUpgradeItem(
            Project, m_terrain, m_blockEntities, componentMiner, raycastResult);
    }
}
