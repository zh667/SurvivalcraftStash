using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 插件版的无线终端行为。单人游戏，绑定和开界面都在本地做。
/// </summary>
public class SubsystemStashWirelessBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemTerrain m_terrain = null!;

    public override int[] HandledBlocks => new[] { StashWirelessTerminalBlock.Index };

    public override void Load(ValuesDictionary valuesDictionary)
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

        TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
        StashWirelessUse.Result result = StashWirelessUse.Use(Project, m_terrain, componentMiner, hit, held);
        if (!result.Consumed)
        {
            return false;
        }

        if (result.Bound)
        {
            inventory.RemoveSlotItems(activeSlot, 1);
            inventory.AddSlotItems(activeSlot, StashWirelessTerminalBlock.Bind(held, result.HubId), 1);
        }
        else if (result.HubId > 0 && componentMiner.ComponentPlayer is { } player)
        {
            StashWirelessUse.OpenRemote(player, Project, result.HubId);
        }

        return true;
    }
}
