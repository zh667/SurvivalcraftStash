using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 升级件的使用行为：拿着升级件点箱子就地升级。
///
/// 挂在**升级件自己**这个方块上，走 <c>OnUse</c> 而不是 <c>OnInteract</c>：
/// 玩家点方块时，原版先按手持物品分发 <c>Use</c>，返回 true 才不会再走 <c>Interact</c>
/// （见 ComponentPlayer 里的 `if (!ComponentMiner.Use(...)) { ... Interact(...) }`）。
/// 挂在箱子上是不行的——原版木箱自己的行为排在前面，会先把箱子界面打开。
///
/// 这个类两个平台通用：<c>SubsystemBlockBehavior.OnUse</c> 在两版里签名一致。
/// </summary>
public class SubsystemStashUpgradeBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities m_blockEntities = null!;
    private SubsystemTerrain m_terrain = null!;

    public override int[] HandledBlocks => new[] { StashChestTiers.UpgradeItemIndex };

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        m_blockEntities = Project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true);
        m_terrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
    }

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        if (componentMiner.Raycast(ray, RaycastMode.Interaction) is not TerrainRaycastResult raycastResult)
        {
            return false;
        }

        int contents = m_terrain.Terrain.GetCellContents(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);

        // 只对箱子（原版木箱或我们的分级箱）有反应，其它方块放行给原版逻辑。
        if (contents != StashChestTiers.VanillaChestIndex && !StashChestTiers.IsStashChest(contents))
        {
            return false;
        }

        return StashUpgradeUse.TryUseUpgradeItem(Project, m_terrain, m_blockEntities, componentMiner, raycastResult);
    }
}
