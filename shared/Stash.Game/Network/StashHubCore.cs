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

    /// <param name="withCrafting">
    /// true = 无线合成终端，界面右下角多一块 3×3 合成格。
    /// 合成格挂在玩家实体上（<c>ComponentStashCraftingGrid</c>），跟着存档走。
    /// </param>
    public static bool OpenTerminal(
        ComponentPlayer player,
        List<IInventory> containers,
        string? title = null,
        bool withCrafting = false)
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

        ComponentStashCraftingGrid? grid = null;
        if (withCrafting)
        {
            grid = player.Entity.FindComponent<ComponentStashCraftingGrid>(throwOnError: false);
            if (grid == null)
            {
                // 组件没注入上（旧存档 / 注入失败）。不该因此打不开终端，退回普通终端就行。
                Log.Warning("[Stash] 玩家身上没有合成格组件，无线合成终端退回普通终端。");
            }
        }

        player.ComponentGui.ModalPanelWidget = new StashTerminalWidget(
            player, containers, title ?? StashText.HubName, grid);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        return true;
    }
}
