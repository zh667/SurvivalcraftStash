using Engine;
using Game;
using GameEntitySystem;

namespace Stash.Game;

/// <summary>存储枢纽的平台无关逻辑：扫网络、开终端。</summary>
public static class StashHubCore
{
    public static List<IInventory> ScanInventories(Project project, Point3 hub)
    {
        SubsystemTerrain terrain = project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
        SubsystemBlockEntities blockEntities = project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true);

        return StashNetworkScanner.ToInventories(StashNetworkScanner.Scan(terrain, blockEntities, hub));
    }

    public static bool OpenTerminal(ComponentPlayer player, List<IInventory> containers)
    {
        if (player?.ComponentGui == null)
        {
            return false;
        }

        if (containers.Count == 0)
        {
            player.ComponentGui.DisplaySmallMessage(StashText.HubEmpty, Color.White, blinking: false, playNotificationSound: false);
            return true;
        }

        player.ComponentGui.ModalPanelWidget = new StashTerminalWidget(player, containers);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        return true;
    }
}
