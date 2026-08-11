using Engine;
using Game.NetWork;
using Stash.Game;

namespace Game;

/// <summary>
/// 无线终端的往返包（包号 233）。
///
/// 客户端把"我按了右键，当时指着哪个格子（可能什么都没指）"发上去；
/// 服务端负责绑定或算出要打开哪个终端，再把库存 Id 列表回给这个客户端。
/// 回程复用 <see cref="StashOpenTerminalPackage"/> 的格式没意义（那边不带标题），
/// 所以这里自己带上终端名字。
/// </summary>
public sealed class StashWirelessPackage : IPackage
{
    public const byte PackageId = 233;

    public byte ID => PackageId;

    public Client To { get; set; } = null!;

    public Client Except { get; set; } = null!;

    public Client From { get; set; } = null!;

    public ClientState MinNeedState => ClientState.Playing;

    public bool IsResponse { get; private set; }

    public bool HasAim { get; private set; }

    public Point3 Aim { get; private set; }

    public string HubName { get; private set; } = string.Empty;

    public List<int> InventoryIds { get; private set; } = new();

    public StashWirelessPackage()
    {
    }

    public StashWirelessPackage(Point3? aim)
    {
        HasAim = aim.HasValue;
        Aim = aim ?? default;
        IsResponse = false;
    }

    private StashWirelessPackage(string hubName, List<int> inventoryIds)
    {
        HubName = hubName;
        InventoryIds = inventoryIds;
        IsResponse = true;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(IsResponse);
        if (!IsResponse)
        {
            writer.Write(HasAim);
            if (HasAim)
            {
                writer.Write(Aim);
            }

            return;
        }

        writer.Write(HubName);
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
            HasAim = reader.ReadBoolean();
            if (HasAim)
            {
                Aim = reader.ReadPoint3();
            }

            return;
        }

        HubName = reader.ReadString();
        int count = reader.ReadInt32();
        if (count is < 0 or > StashNetworkScanner.MaxContainers)
        {
            throw new InvalidDataException($"Stash 无线终端包的容器数量非法：{count}");
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

        ComponentPlayer? player = FindPlayer(projectNet);
        if (player?.ComponentMiner?.Inventory is not { } inventory)
        {
            return;
        }

        int activeSlot = inventory.ActiveSlotIndex;
        int held = inventory.GetSlotValue(activeSlot);
        if (Terrain.ExtractContents(held) != StashWirelessTerminalBlock.Index)
        {
            return;
        }

        SubsystemTerrain terrain = projectNet.FindSubsystem<SubsystemTerrain>();
        TerrainRaycastResult? hit = null;
        if (HasAim)
        {
            hit = new TerrainRaycastResult
            {
                CellFace = new CellFace(Aim.X, Aim.Y, Aim.Z, 0),
                Value = terrain.Terrain.GetCellValue(Aim.X, Aim.Y, Aim.Z),
            };
        }

        StashWirelessUse.Result result = StashWirelessUse.Use(
            projectNet, terrain, player.ComponentMiner, hit, held);
        if (!result.Consumed)
        {
            return;
        }

        if (result.Bound)
        {
            inventory.RemoveSlotItems(activeSlot, 1);
            inventory.AddSlotItems(activeSlot, StashWirelessTerminalBlock.Bind(held, result.HubId), 1);
            return;
        }

        if (result.HubId <= 0)
        {
            return;
        }

        Stash.Shared.Storage.StashHubRecord? hub = StashHubNaming.Find(result.HubId);
        if (hub == null)
        {
            return;
        }

        var ids = new List<int>();
        foreach (IInventory container in StashHubCore.ScanInventories(projectNet, new Point3(hub.X, hub.Y, hub.Z)))
        {
            ids.Add(container.Id);
        }

        netNode.QueuePackage(new StashWirelessPackage(hub.Name, ids) { To = From });
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

        StashHubCore.OpenTerminal(player, containers, HubName);
    }

    private ComponentPlayer? FindPlayer(ProjectNet projectNet)
    {
        SubsystemPlayers players = projectNet.FindSubsystem<SubsystemPlayers>();
        if (players == null || From == null)
        {
            return null;
        }

        foreach (ComponentPlayer candidate in players.ComponentPlayers)
        {
            if (candidate.PlayerGuid == From.PlayerGuid)
            {
                return candidate;
            }
        }

        return null;
    }
}
