using Engine;
using Game.NetWork;
using Stash.Game;

namespace Game;

/// <summary>
/// 打开存储终端的往返包（包号 232）。
///
/// 和箱子那个包同样的道理：网络里有哪些容器只有服务端算得准
/// （客户端不一定持有方块实体），所以客户端只发坐标，服务端把**库存 Id 列表**回过来，
/// 客户端再用 <c>SubsystemInventories</c> 把它们解析成已同步的库存对象。
/// </summary>
public sealed class StashOpenTerminalPackage : IPackage
{
    public const byte PackageId = 232;

    /// <summary>回包里最多带多少个库存 Id，和扫描上限一致。</summary>
    public const int MaxInventories = StashNetworkScanner.MaxContainers;

    public byte ID => PackageId;

    public Client To { get; set; } = null!;

    public Client Except { get; set; } = null!;

    public Client From { get; set; } = null!;

    public ClientState MinNeedState => ClientState.Playing;

    public bool IsResponse { get; private set; }

    public Point3 Point { get; private set; }

    public List<int> InventoryIds { get; private set; } = new();

    public StashOpenTerminalPackage()
    {
    }

    public StashOpenTerminalPackage(Point3 point)
    {
        Point = point;
        IsResponse = false;
    }

    private StashOpenTerminalPackage(List<int> inventoryIds)
    {
        InventoryIds = inventoryIds;
        IsResponse = true;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(IsResponse);
        if (!IsResponse)
        {
            writer.Write(Point);
            return;
        }

        writer.Write(InventoryIds.Count);
        foreach (int id in InventoryIds)
        {
            writer.Write(id);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        IsResponse = reader.ReadBoolean();
        if (!IsResponse)
        {
            Point = reader.ReadPoint3();
            return;
        }

        int count = reader.ReadInt32();
        if (count is < 0 or > MaxInventories)
        {
            throw new InvalidDataException($"Stash 终端包的容器数量非法：{count}");
        }

        InventoryIds = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            InventoryIds.Add(reader.ReadInt32());
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

        SubsystemTerrain terrain = projectNet.FindSubsystem<SubsystemTerrain>();
        if (terrain == null || Terrain.ExtractContents(terrain.Terrain.GetCellValue(Point.X, Point.Y, Point.Z)) != StashHubBlock.Index)
        {
            return;
        }

        var ids = new List<int>();
        foreach (IInventory inventory in StashHubCore.ScanInventories(projectNet, Point))
        {
            ids.Add(inventory.Id);
        }

        netNode.QueuePackage(new StashOpenTerminalPackage(ids) { To = From });
    }

    private void HandleResponse(ProjectNet projectNet)
    {
        if (!IsResponse)
        {
            return;
        }

        SubsystemInventories inventories = projectNet.FindSubsystem<SubsystemInventories>();
        ComponentPlayer player = CommonLib.MainPlayer;
        if (inventories == null || player == null)
        {
            return;
        }

        var containers = new List<IInventory>();
        foreach (int id in InventoryIds)
        {
            IInventory inventory = inventories.GetInventoryById(id);
            if (inventory != null)
            {
                containers.Add(inventory);
            }
        }

        StashHubCore.OpenTerminal(player, containers);
    }
}
