using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 往刚打开的界面里挂「整理」按钮。
///
/// 挂钩点是 <c>ModLoader.OnModalPanelWidgetSet</c>——它在 <c>ComponentGui.ModalPanelWidget</c> 的
/// setter 里触发，此时新界面**已经构造完毕**（子控件都在）。
/// 另一个候选 <c>OnWidgetConstruct</c> 是在 XML 加载**之前**触发的，那时还没有子控件，用不了。
///
/// **只在两种界面上挂**：玩家自己的物品栏、背包。
/// 箱子、存储终端、衣物界面一律不挂——那里的排布要么是玩家自己摆的，要么本来就是排好序的。
/// </summary>
public static class StashUiInjector
{
    /// <summary>已经挂过的界面，避免重复挂。</summary>
    private static readonly HashSet<Widget> s_injected = new();

    public static void OnModalPanelChanged(ComponentGui gui, Widget? oldWidget, Widget? newWidget)
    {
        if (oldWidget != null)
        {
            s_injected.Remove(oldWidget);
        }

        // 界面一关就把"正在打字"复位，否则搜索框带着焦点被销毁，按键会一直被吃掉。
        StashHotkeys.TypingInProgress = false;

        if (newWidget == null || s_injected.Contains(newWidget) || !StashPlatform.IsReady)
        {
            return;
        }

        try
        {
            Inject(gui, newWidget);
        }
        catch (Exception exception)
        {
            // 界面注入失败不该把游戏带崩——最坏情况就是这个界面没有按钮。
            Log.Warning($"[Stash] 注入整理按钮失败：{exception.Message}");
        }
    }

    private static void Inject(ComponentGui gui, Widget panel)
    {
        if (PanelInventoryScanner.FindHost(panel) is not { } host)
        {
            return;
        }

        IInventory? viewerInventory = gui?.m_componentPlayer?.ComponentMiner?.Inventory;
        List<PanelContainer> containers = PanelInventoryScanner.Scan(panel, viewerInventory);
        if (containers.Count == 0)
        {
            return;
        }

        PanelContainer? target = ChooseSortTarget(containers);
        if (target == null)
        {
            return;
        }

        host.Children.Add(new StashButtonBar(gui, target));
        s_injected.Add(panel);
    }

    /// <summary>
    /// 整理谁：背包界面整理背包，玩家物品栏界面整理物品栏，其它界面一概不挂。
    /// 创造模式的物品栏是无限物品面板，整理它没有意义。
    /// </summary>
    private static PanelContainer? ChooseSortTarget(List<PanelContainer> containers)
    {
        foreach (PanelContainer container in containers)
        {
            if (container.Inventory is ComponentStashBackpack)
            {
                return container;
            }
        }

        foreach (PanelContainer container in containers)
        {
            if (container.IsPlayerInventory && !container.IsCreative)
            {
                return container;
            }
        }

        return null;
    }

    public static void Reset() => s_injected.Clear();
}
