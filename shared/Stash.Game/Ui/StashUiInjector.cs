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
/// **挂在哪些界面**：玩家物品栏、背包、箱子（原版箱子和我们的分级箱子都算）。
/// 一个界面里有几块值得整理的库存就给几个按钮——箱子界面上"整理箱子"和"整理物品栏"各一个。
/// 熔炉、工作台、衣物这些格子少、顺序有含义的界面不挂。
/// </summary>
public static class StashUiInjector
{
    /// <summary>已经挂过的界面，避免重复挂。</summary>
    private static readonly HashSet<Widget> s_injected = new();

    /// <summary>格子数少于这个的库存不当作"仓库"（工作台 3×3、衣物 4 格、熔炉几格）。</summary>
    private const int MinContainerSlots = 8;

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
        // 存储终端不挂：它左边一屏格子分属整个网络里的几十个箱子，
        // 按"每块库存一个按钮"来会挂出一排"整理箱子"。而且终端的显示本来就是排好序的。
        if (panel is StashTerminalWidget)
        {
            return;
        }

        if (PanelInventoryScanner.FindHost(panel) is not { } host)
        {
            return;
        }

        IInventory? viewerInventory = gui?.m_componentPlayer?.ComponentMiner?.Inventory;
        List<PanelContainer> containers = PanelInventoryScanner.Scan(panel, viewerInventory);
        List<(StashSortKind Kind, PanelContainer Target)> targets = ChooseSortTargets(containers);
        if (targets.Count == 0)
        {
            return;
        }

        MakeRoomForBar(host);

        // 我们自己的容器界面已经自带"物品栏 / 背包"切换，别再挂一个。
        host.Children.Add(new StashButtonBar(gui, targets, allowSideToggle: panel is not StashContainerWidget));
        s_injected.Add(panel);

        Log.Information($"[Stash] {panel.GetType().Name} 挂上 {targets.Count} 个整理按钮");
    }

    /// <summary>
    /// 把面板加高一条，按钮就落在新腾出来的空白里而不是压在格子上。
    ///
    /// **原版面板的尺寸不在根节点上**。看 ChestWidget.xml / FullInventoryWidget.xml：
    /// <code>
    /// &lt;ChestWidget&gt;
    ///   &lt;BevelledRectangleWidget Size="614, 382" BevelSize="3" /&gt;   ← 尺寸在这
    ///   &lt;GridPanelWidget CanvasWidget.Position="12, 52" … /&gt;
    /// &lt;/ChestWidget&gt;
    /// </code>
    /// 根节点是个没写 Size 的 CanvasWidget（Size = -1 自适应），
    /// <c>CanvasWidget.MeasureOverride</c> 会取所有子控件"位置 + 期望尺寸"的最大值——
    /// 也就是那块底板撑出来的 614×382。
    /// 所以要加高的是**底板**，改根节点的 Size 没用（上一版就是这么写的，等于没加）。
    ///
    /// 子控件都是按显式坐标（左上角锚定）摆的，底板变高只会在下面多出一块空白，格子不会动。
    /// </summary>
    private static void MakeRoomForBar(ContainerWidget host)
    {
        // 面板底板：取最大的那块 BevelledRectangleWidget（一个面板通常就一块）。
        BevelledRectangleWidget? background = null;
        foreach (Widget child in host.Children)
        {
            if (child is BevelledRectangleWidget rectangle
                && (background == null || rectangle.Size.Y > background.Size.Y))
            {
                background = rectangle;
            }
        }

        if (background != null && background.Size.Y >= 0f)
        {
            background.Size = new Vector2(background.Size.X, background.Size.Y + StashButtonBar.BarHeight);
        }

        // 根节点自己写了 Size 的（我们自己的界面就是），也要跟着长高，
        // 否则 MeasureOverride 会用根节点那个偏小的值把底板裁掉。
        if (host is CanvasWidget canvas && canvas.Size.Y >= 0f)
        {
            canvas.Size = new Vector2(canvas.Size.X, canvas.Size.Y + StashButtonBar.BarHeight);
        }
        else if (background == null)
        {
            Log.Warning($"[Stash] {host.GetType().Name} 既没有底板也没写 Size，整理按钮只能贴底边");
        }
    }

    /// <summary>
    /// 挑出这个界面里值得整理的库存。
    ///
    /// - 背包 / 分级箱子 / 原版箱子：整理
    /// - 玩家自己的物品栏：整理（创造模式的那份是无限物品面板，跳过）
    /// - 工作台、熔炉、衣物槽：格子少或者顺序本身有含义，不整理
    /// </summary>
    private static List<(StashSortKind Kind, PanelContainer Target)> ChooseSortTargets(List<PanelContainer> containers)
    {
        var targets = new List<(StashSortKind, PanelContainer)>();

        foreach (PanelContainer container in containers)
        {
            if (container.Inventory is ComponentStashBackpack)
            {
                targets.Add((StashSortKind.Backpack, container));
            }
            else if (!container.IsViewerInventory && container.SlotIndexes.Count >= MinContainerSlots
                && container.Inventory is not ComponentClothing
                && !targets.Exists(t => t.Item1 == StashSortKind.Container))
            {
                // 只认第一块——正常界面就一个容器，多出来的多半是我们没预料到的界面，
                // 与其挂一排按钮不如少挂。
                targets.Add((StashSortKind.Container, container));
            }
        }

        foreach (PanelContainer container in containers)
        {
            // 创造模式下这份是无限物品面板，整理它没有意义（但同一个界面里的箱子照样能整理）。
            if (container.IsViewerInventory && !container.IsCreative)
            {
                targets.Add((StashSortKind.PlayerInventory, container));
            }
        }

        return targets;
    }

    public static void Reset() => s_injected.Clear();
}
