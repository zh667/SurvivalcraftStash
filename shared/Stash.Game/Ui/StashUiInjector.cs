using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 往刚打开的库存界面里挂按钮条。
///
/// 挂钩点是 <c>ModLoader.OnModalPanelWidgetSet</c>——它在 <c>ComponentGui.ModalPanelWidget</c> 的
/// setter 里触发，此时新界面**已经构造完毕**（子控件都在）。
/// 另一个候选 <c>OnWidgetConstruct</c> 是在 XML 加载**之前**触发的，那时还没有子控件，用不了。
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

        PanelContainer? player = containers.Find(c => c.IsPlayerInventory);
        PanelContainer? container = containers.Find(c => !c.IsPlayerInventory && !c.IsCreative);

        if (!StashButtonBar.IsUseful(player, container))
        {
            return;
        }

        var bar = new StashButtonBar(gui, player, container);
        host.Children.Add(bar);
        s_injected.Add(panel);

        Log.Information(
            $"[Stash] 已在 {panel.GetType().Name} 挂上按钮栏（玩家侧 {(player != null ? "有" : "无")}，" +
            $"另一侧 {(container != null ? container.Inventory.GetType().Name : "无")}）");
    }

    public static void Reset() => s_injected.Clear();
}
