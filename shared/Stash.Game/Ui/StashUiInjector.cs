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

        List<PanelContainer> containers = PanelInventoryScanner.Scan(panel);
        if (containers.Count == 0)
        {
            return;
        }

        PanelContainer? player = containers.Find(c => c.IsPlayerInventory);
        PanelContainer? container = containers.Find(c => !c.IsPlayerInventory);

        // 创造物品栏之类的"无限库存"整理没有意义，直接跳过。
        if (container?.Inventory is ComponentCreativeInventory)
        {
            container = null;
        }

        if (!StashButtonBar.IsUseful(player, container))
        {
            return;
        }

        var bar = new StashButtonBar(gui, player, container);
        host.Children.Add(bar);
        s_injected.Add(panel);
    }

    public static void Reset() => s_injected.Clear();
}
