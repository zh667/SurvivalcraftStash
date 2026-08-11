using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 从枢纽出发，找出连成一片的所有容器。
///
/// **连接方式是"容器彼此贴着"**，而不是设计文档里最初设想的"跑在原版电线上"。
/// 换掉的理由：电路子系统的连接语义（挂载面、连接器方向、电流模拟）复杂，
/// 而开发机跑不了游戏，没法验证；相邻聚类的规则玩家一眼就懂，也能纯逻辑单测。
/// 电线方案留作后续可选增强。
/// </summary>
public static class StashNetworkScanner
{
    /// <summary>一次最多接管多少容器，防止有人拿几千个箱子拼一片把服务端拖垮。</summary>
    public const int MaxContainers = 128;

    private static readonly Point3[] Neighbors =
    {
        new(1, 0, 0), new(-1, 0, 0),
        new(0, 1, 0), new(0, -1, 0),
        new(0, 0, 1), new(0, 0, -1),
    };

    /// <summary>这个方块算不算网络的一部分（枢纽自己、我们的箱子和抽屉、原版木箱）。</summary>
    public static bool IsNetworkBlock(int contents) =>
        contents == StashChestTiers.VanillaChestIndex
        || StashChestTiers.IsStashChest(contents)
        || StashDrawerTiers.IsDrawer(contents)
        || contents == StashHubBlock.Index;

    public static List<ComponentBlockEntity> Scan(
        SubsystemTerrain terrain,
        SubsystemBlockEntities blockEntities,
        Point3 hub)
    {
        var found = new List<ComponentBlockEntity>();
        var visited = new HashSet<Point3> { hub };
        var queue = new Queue<Point3>();
        queue.Enqueue(hub);

        while (queue.Count > 0 && found.Count < MaxContainers)
        {
            Point3 current = queue.Dequeue();

            foreach (Point3 offset in Neighbors)
            {
                var next = new Point3(current.X + offset.X, current.Y + offset.Y, current.Z + offset.Z);
                if (!visited.Add(next))
                {
                    continue;
                }

                int contents = terrain.Terrain.GetCellContents(next.X, next.Y, next.Z);
                if (!IsNetworkBlock(contents))
                {
                    continue;
                }

                queue.Enqueue(next);

                ComponentBlockEntity blockEntity = blockEntities.GetBlockEntity(next.X, next.Y, next.Z);
                if (blockEntity != null && HasInventory(blockEntity))
                {
                    found.Add(blockEntity);
                }
            }
        }

        return found;
    }

    private static bool HasInventory(ComponentBlockEntity blockEntity) =>
        blockEntity.Entity.FindComponent<IInventory>(throwOnError: false) != null;

    public static List<IInventory> ToInventories(IEnumerable<ComponentBlockEntity> blockEntities)
    {
        var inventories = new List<IInventory>();
        foreach (ComponentBlockEntity blockEntity in blockEntities)
        {
            IInventory inventory = blockEntity.Entity.FindComponent<IInventory>(throwOnError: false);
            if (inventory != null)
            {
                inventories.Add(inventory);
            }
        }

        return inventories;
    }
}
