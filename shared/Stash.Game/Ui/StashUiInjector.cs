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

            // 关掉合成终端时报一次残留。那块 3×3 挂在玩家身上、跟着存档走，
            // 关了界面从别处一格都看不见——不打这一行，"东西留在合成格里"
            // 和"东西没了"在日志上长得一模一样。
            if (oldWidget is StashTerminalWidget)
            {
                gui?.m_componentPlayer?.Entity
                    ?.FindComponent<ComponentStashCraftingGrid>(throwOnError: false)
                    ?.ReportLeftovers("关闭");
            }
        }

        // 界面一关就把"正在打字"复位，否则搜索框带着焦点被销毁，按键会一直被吃掉。
        StashHotkeys.TypingInProgress = false;

        // 配方浮层是跟着某个界面开的（"＋"要往那个界面的合成格里填料）。
        // 界面换了它就失去了意义，而且藏起来的旧面板得放回去，否则玩家再打开会看到空白。
        if (StashOverlayHost.IsOpen)
        {
            StashOverlayHost.Close(gui);
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

        float barHeight = MakeRoomForBar(host);

        // 我们自己的容器界面已经自带"物品栏 / 背包"切换，别再挂一个。
        host.Children.Add(new StashButtonBar(
            gui, targets,
            allowSideToggle: panel is not StashContainerWidget,
            barHeight: barHeight,
            craftTarget: FindCraftTarget(containers)));
        s_injected.Add(panel);

        Log.Information($"[Stash] {panel.GetType().Name} 挂上 {targets.Count} 个整理按钮");
    }

    /// <summary>
    /// 找出这个界面里能"填材料"的格子。按 E 打开的是 2×2，工作台是 3×3，熔炉是 N×1。
    ///
    /// 合成格的边长直接读 <c>ComponentCraftingTable.m_craftingGridSize</c>
    /// （两个平台都是 public 字段），不自己按 <c>sqrt(SlotsCount - 2)</c> 反推。
    ///
    /// 熔炉的原料槽数按 <c>SlotsCount - 3</c> 算——这正是 <c>ComponentFurnace.Load</c>
    /// 里的算法（末尾三格固定是燃料/产物/残留），而且它**只接受 1~3**，超出直接抛异常。
    /// </summary>
    private static StashCraftTarget? FindCraftTarget(List<PanelContainer> containers)
    {
        foreach (PanelContainer container in containers)
        {
            if (container.Inventory is ComponentCraftingTable table && table.m_craftingGridSize >= 2)
            {
                return new StashCraftTarget(table, table.m_craftingGridSize, table.m_craftingGridSize, IsFurnace: false);
            }

            if (container.Inventory is ComponentFurnace furnace)
            {
                int inputs = furnace.SlotsCount - 3;
                if (inputs >= 1)
                {
                    return new StashCraftTarget(furnace, inputs, 1, IsFurnace: true);
                }
            }
        }

        return null;
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
    /// <returns>实际腾出来的高度。空间不够时会小于 <see cref="StashButtonBar.BarHeight"/>。</returns>
    private static float MakeRoomForBar(ContainerWidget host)
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

        // 加高之前先看画布还剩多少。
        //
        // UI 缩放拉到 1.2 时画布只有 398 高，而原版面板本身就 382——只剩 16 单位。
        // 照直加 40 会让面板变成 422，被 GameWidget 的 ClampToBounds 上下各切掉 12，
        // 按钮的下半截就没了。这里改成"有多少用多少"，实在不够就压缩按钮条本身。
        float panelHeight = background?.Size.Y ?? (host as CanvasWidget)?.Size.Y ?? 0f;
        float barHeight = StashButtonBar.BarHeight;
        if (panelHeight > 0f && !StashScreenMetrics.HasRoomBelow(panelHeight, barHeight))
        {
            float room = StashScreenMetrics.PanelBudget.Y - panelHeight;
            barHeight = MathUtils.Clamp(room, StashButtonBar.MinBarHeight, StashButtonBar.BarHeight);
            Log.Information($"[Stash] 画布不够高（面板 {panelHeight:0}，预算 {StashScreenMetrics.PanelBudget.Y:0}），"
                + $"按钮条压到 {barHeight:0}。玩家可以把设置里的界面缩放调小一点。");
        }

        if (background != null && background.Size.Y >= 0f)
        {
            background.Size = new Vector2(background.Size.X, background.Size.Y + barHeight);
        }

        // 根节点自己写了 Size 的（我们自己的界面就是），也要跟着长高，
        // 否则 MeasureOverride 会用根节点那个偏小的值把底板裁掉。
        if (host is CanvasWidget canvas && canvas.Size.Y >= 0f)
        {
            canvas.Size = new Vector2(canvas.Size.X, canvas.Size.Y + barHeight);
        }
        else if (background == null)
        {
            Log.Warning($"[Stash] {host.GetType().Name} 既没有底板也没写 Size，整理按钮只能贴底边");
        }

        return barHeight;
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

    public static void Reset()
    {
        s_injected.Clear();
        StashOverlayHost.Reset();
        StashDiag.Reset();

        // 配方索引里存的是方块索引，而插件版每局都可能重新分配（SCAPI 会改写 Block.Index），
        // 换世界不清就会拿着上一局的号去查表。
        StashRecipeIndex.Invalidate();
    }
}
