using Engine;
using Game.NetWork;
using Game.NetWork.Packages;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 联机版的分级熔炉行为。
///
/// 开界面**直接复用原版的 <see cref="BlockEditPackage"/>**（EventType.OpenInventoryByPoint）：
/// 服务端拿坐标找到方块实体的 <c>IInventory</c> 发回客户端，客户端那边按
/// <c>inventory is ComponentFurnace</c> 分发到 <c>FurnaceWidget</c>——
/// 我们的 <c>ComponentStashFurnace</c> 是 <c>ComponentFurnace</c> 的子类，正好命中。
///
/// 这一点和分级箱子不同：箱子必须自己发包，因为原版那条路是按 <c>is ComponentChest</c>
/// 开写死 4×4 的界面，我们 32~80 格的箱子套进去只能看到前 16 格。
/// 熔炉的格子布局和原版完全一样（只是烧得快），所以能白嫖整条链路。
///
/// 另外，原版 <c>SubsystemFurnaceBlockBehavior.Update</c> 每秒会遍历**所有**方块实体收集
/// <c>ComponentFurnace</c> 发 <c>ComponentFurnacePackage</c> 同步给客户端——
/// 我们的熔炉同样会被收进去，火焰和进度条的同步也是白嫖的。
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

    /// <summary>
    /// 注意：联机版 <c>SubsystemTerrain.ChangeCell</c> 在 <c>miner == null</c> 时**只调 5 参版**。
    /// 只覆写 6 参版的话，升级熔炉（走 ChangeCell）就不会建方块实体——
    /// 表现是"外观变了但打不开"。两个都要覆写，分级箱子当初就踩过这个坑。
    /// </summary>
    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) =>
        StashFurnaceCore.OnFurnaceAdded(Project, m_blockEntities, value, x, y, z);

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        ComponentBlockEntity? blockEntity = StashFurnaceCore.OnFurnaceAdded(Project, m_blockEntities, value, x, y, z);

        // Owner 是联机版特有的（领地权限），原版建方块实体时也会写。
        if (blockEntity != null && miner?.ComponentPlayer != null)
        {
            blockEntity.Owner = miner.ComponentPlayer.PlayerGuid;
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        StashFurnaceCore.OnFurnaceRemoved(Project, m_blockEntities, x, y, z);

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        StashFurnaceTier? tier = StashFurnaceTiers.ByBlockIndex(Terrain.ExtractContents(raycastResult.Value));
        if (tier == null)
        {
            return false;
        }

        if (CommonLib.WorkType == WorkType.Client && CommonLib.MainPlayer == componentMiner.ComponentPlayer)
        {
            IPackage package = new BlockEditPackage(
                new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z),
                BlockEditPackage.EventType.OpenInventoryByPoint);
            CommonLib.Net.QueuePackage(package);

            // 界面由服务端回包后才打开，档位提示我们这边立刻显示——
            // 否则联机客户端会比单机少一条信息，两边行为不一致。
            componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                StashText.FurnaceOpened(tier), Color.White, blinking: false, playNotificationSound: false);
            return true;
        }

        ComponentBlockEntity blockEntity = m_blockEntities.GetBlockEntity(
            raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        if (blockEntity == null || componentMiner.ComponentPlayer == null)
        {
            return false;
        }

        var furnace = blockEntity.Entity.FindComponent<ComponentFurnace>(throwOnError: false);
        if (furnace == null)
        {
            return false;
        }

        // 单人/主机：直接开。
        if (componentMiner.ComponentPlayer.PlayerData.IsMainPlayer)
        {
            StashFurnaceCore.OpenFurnaceUi(componentMiner.ComponentPlayer, furnace, tier);
        }

        return true;
    }
}
