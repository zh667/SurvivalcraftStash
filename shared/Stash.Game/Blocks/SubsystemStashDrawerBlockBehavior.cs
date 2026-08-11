using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 抽屉行为。两个平台通用——它不开界面，也就不需要联机版那套"服务端回库存 Id"的往返，
/// 交互本身在原版流程里就是服务端权威执行的。
/// </summary>
public class SubsystemStashDrawerBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities m_blockEntities = null!;

    public override int[] HandledBlocks =>
        StashDrawerTiers.All.Select(tier => tier.Index).ToArray();

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        m_blockEntities = Project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) =>
        StashDrawerCore.OnDrawerAdded(Project, m_blockEntities, value, x, y, z);

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        StashChestCore.OnChestRemoved(Project, m_blockEntities, x, y, z);

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (!StashDrawerTiers.IsDrawer(Terrain.ExtractContents(raycastResult.Value)))
        {
            return false;
        }

        // 联机版里客户端也会走一遍 OnInteract（原版靠 WorkType 判断来分流）。
        // 这里直接消费掉：客户端发出去的交互事件会让服务端再执行一次，两边都做就会双份扣减。
        if (StashPlatform.IsReady && !StashPlatform.Current.IsAuthoritative)
        {
            return true;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        var drawer = blockEntity?.Entity.FindComponent<ComponentStashDrawer>(throwOnError: false);

        return drawer != null && StashDrawerCore.Interact(drawer, componentMiner);
    }
}
