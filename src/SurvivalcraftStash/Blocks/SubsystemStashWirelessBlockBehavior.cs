using Engine;
using Game.NetWork;
using Stash.Game;

namespace Game;

/// <summary>
/// 联机版的无线终端行为。
///
/// 绑定要在服务端做（改物品 data + 写世界登记簿），开界面要在客户端做，
/// 所以走和实体终端同一条往返路：客户端发请求 → 服务端算出 hubId 与容器 → 回给客户端开界面。
/// </summary>
public class SubsystemStashWirelessBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemTerrain m_terrain = null!;

    public override int[] HandledBlocks => new[] { StashWirelessTerminalBlock.Index };

    public override void Load(TemplatesDatabase.ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        m_terrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
    }

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        IInventory inventory = componentMiner.Inventory;
        if (inventory == null)
        {
            return false;
        }

        int activeSlot = inventory.ActiveSlotIndex;
        int held = inventory.GetSlotValue(activeSlot);
        if (Terrain.ExtractContents(held) != StashWirelessTerminalBlock.Index)
        {
            return false;
        }

        // 客户端只发意图：服务端才有权改物品和读登记簿。
        if (CommonLib.WorkType == WorkType.Client)
        {
            if (CommonLib.MainPlayer == componentMiner.ComponentPlayer)
            {
                TerrainRaycastResult? aimed = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
                CommonLib.Net.QueuePackage(new StashWirelessPackage(
                    aimed.HasValue
                        ? new Point3(aimed.Value.CellFace.X, aimed.Value.CellFace.Y, aimed.Value.CellFace.Z)
                        : null));
            }

            return true;
        }

        TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
        StashWirelessUse.Result result = StashWirelessUse.Use(Project, m_terrain, componentMiner, hit, held);
        if (!result.Consumed)
        {
            return false;
        }

        if (result.Bound)
        {
            // 绑定：把编号写进这一件物品的 data 位。
            inventory.RemoveSlotItems(activeSlot, 1);
            inventory.AddSlotItems(activeSlot, StashWirelessTerminalBlock.Bind(held, result.HubId), 1);
        }
        else if (result.HubId > 0 && componentMiner.ComponentPlayer is { } player && player.PlayerData.IsMainPlayer)
        {
            StashWirelessUse.OpenRemote(player, Project, result.HubId);
        }

        return true;
    }
}
