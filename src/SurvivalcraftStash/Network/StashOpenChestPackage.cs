using Engine;
using Game.NetWork;
using Stash.Game;

namespace Game;

/// <summary>
/// 打开分级箱子的往返包。
///
/// 客户端发坐标 → 服务端找到方块实体、取出库存 Id 与档位 → 只回给这个客户端 →
/// 客户端按 Id 找到已同步过来的库存，开我们自己的界面。
///
/// 包号 231，紧挨着 <see cref="StashOpPackage"/>（230）。
/// </summary>
public sealed class StashOpenChestPackage : IPackage
{
    public const byte PackageId = 231;

    public byte ID => PackageId;

    public Client To { get; set; } = null!;

    public Client Except { get; set; } = null!;

    public Client From { get; set; } = null!;

    public ClientState MinNeedState => ClientState.Playing;

    /// <summary>请求方向：客户端发坐标；服务端回 Id + 档位。</summary>
    public bool IsResponse { get; private set; }

    public Point3 Point { get; private set; }

    public int InventoryId { get; private set; }

    public int BlockIndex { get; private set; }

    public StashOpenChestPackage()
    {
    }

    public StashOpenChestPackage(Point3 point)
    {
        Point = point;
        IsResponse = false;
    }

    private StashOpenChestPackage(int inventoryId, int blockIndex)
    {
        InventoryId = inventoryId;
        BlockIndex = blockIndex;
        IsResponse = true;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(IsResponse);
        if (IsResponse)
        {
            writer.Write(InventoryId);
            writer.Write(BlockIndex);
        }
        else
        {
            writer.Write(Point);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        IsResponse = reader.ReadBoolean();
        if (IsResponse)
        {
            InventoryId = reader.ReadInt32();
            BlockIndex = reader.ReadInt32();
        }
        else
        {
            Point = reader.ReadPoint3();
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        if (isServer)
        {
            HandleRequest(projectNet, netNode);
        }
        else
        {
            HandleResponse(projectNet);
        }
    }

    private void HandleRequest(ProjectNet projectNet, NetNode netNode)
    {
        if (IsResponse || From == null)
        {
            return;
        }

        ComponentBlockEntity blockEntity = projectNet.FindSubsystem<SubsystemBlockEntities>()
            ?.GetBlockEntity(Point.X, Point.Y, Point.Z);
        if (blockEntity == null)
        {
            return;
        }

        var chest = blockEntity.Entity.FindComponent<ComponentStashChest>(throwOnError: false);
        if (chest == null)
        {
            return;
        }

        // 联机版的 ComponentBlockEntity 没有 BlockValue（那是插件版才有的），直接读地形。
        SubsystemTerrain terrain = projectNet.FindSubsystem<SubsystemTerrain>();
        if (terrain == null)
        {
            return;
        }

        int blockIndex = Terrain.ExtractContents(terrain.Terrain.GetCellValue(Point.X, Point.Y, Point.Z));
        if (!StashChestTiers.IsStashChest(blockIndex))
        {
            return;
        }

        netNode.QueuePackage(new StashOpenChestPackage(chest.Id, blockIndex) { To = From });
    }

    private void HandleResponse(ProjectNet projectNet)
    {
        if (!IsResponse)
        {
            return;
        }

        StashChestTier? tier = StashChestTiers.ByBlockIndex(BlockIndex);
        if (tier == null)
        {
            return;
        }

        IInventory inventory = projectNet.FindSubsystem<SubsystemInventories>()?.GetInventoryById(InventoryId);
        ComponentPlayer player = CommonLib.MainPlayer;
        if (inventory == null || player == null)
        {
            return;
        }

        StashChestCore.OpenChestUi(player, inventory, tier);
    }
}
