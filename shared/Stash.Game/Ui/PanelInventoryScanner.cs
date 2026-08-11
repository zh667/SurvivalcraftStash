using Game;

namespace Stash.Game;

/// <summary>界面里的一个"容器区"：某个库存 + 这个界面实际展示的那些槽位。</summary>
public sealed class PanelContainer
{
    public PanelContainer(IInventory inventory, List<int> slotIndexes, List<InventorySlotWidget> widgets)
    {
        Inventory = inventory;
        SlotIndexes = slotIndexes;
        Widgets = widgets;
    }

    /// <summary>是不是当前玩家自己那份库存（按对象比较，不看类型）。</summary>
    public bool IsViewerInventory { get; set; }

    /// <summary>创造模式的物品栏：里面是无限物品面板，整理它没有意义。</summary>
    public bool IsCreative => Inventory is ComponentCreativeInventory;

    public IInventory Inventory { get; }

    public List<int> SlotIndexes { get; }

    public List<InventorySlotWidget> Widgets { get; }

    /// <summary>
    /// 是不是玩家自己的那份库存。
    ///
    /// **不能按类型判断**：创造模式下玩家的库存是 <see cref="ComponentCreativeInventory"/>，
    /// 它直接继承 Component 而不是 ComponentInventory。早先写成 `is ComponentInventory`，
    /// 结果创造模式下整排按钮一个都不显示（实机反馈"玩家物品栏依旧没有整理功能"）。
    /// </summary>
    public bool IsPlayerInventory => IsViewerInventory;
}

/// <summary>
/// 扫描一个已构造好的界面，找出里面所有的库存槽位控件，按库存分组。
///
/// 这样做的好处：不用为箱子、熔炉、发射器、工作台各写一遍适配——
/// 凡是用 <see cref="InventorySlotWidget"/> 搭出来的界面（包括别的 Mod 的）都能识别。
/// </summary>
public static class PanelInventoryScanner
{
    public static List<PanelContainer> Scan(Widget root, IInventory? viewerInventory = null)
    {
        var byInventory = new Dictionary<IInventory, PanelContainer>();
        var order = new List<PanelContainer>();

        Walk(root, slotWidget =>
        {
            IInventory? inventory = slotWidget.m_inventory;
            if (inventory == null)
            {
                return;
            }

            if (!byInventory.TryGetValue(inventory, out PanelContainer? container))
            {
                container = new PanelContainer(inventory, new List<int>(), new List<InventorySlotWidget>());
                byInventory[inventory] = container;
                order.Add(container);
            }

            if (!container.SlotIndexes.Contains(slotWidget.m_slotIndex))
            {
                container.SlotIndexes.Add(slotWidget.m_slotIndex);
                container.Widgets.Add(slotWidget);
            }
        });

        foreach (PanelContainer container in order)
        {
            SortBySlotIndex(container);
            container.IsViewerInventory = viewerInventory != null && ReferenceEquals(container.Inventory, viewerInventory);
        }

        return order;
    }

    private static void SortBySlotIndex(PanelContainer container)
    {
        var pairs = new List<(int Slot, InventorySlotWidget Widget)>(container.SlotIndexes.Count);
        for (int i = 0; i < container.SlotIndexes.Count; i++)
        {
            pairs.Add((container.SlotIndexes[i], container.Widgets[i]));
        }

        pairs.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        container.SlotIndexes.Clear();
        container.Widgets.Clear();
        foreach ((int slot, InventorySlotWidget widget) in pairs)
        {
            container.SlotIndexes.Add(slot);
            container.Widgets.Add(widget);
        }
    }

    private static void Walk(Widget widget, Action<InventorySlotWidget> onSlot)
    {
        if (widget is InventorySlotWidget slot)
        {
            onSlot(slot);
            return;
        }

        if (widget is ContainerWidget container)
        {
            foreach (Widget child in container.Children)
            {
                Walk(child, onSlot);
            }
        }
    }

    /// <summary>找到能挂按钮的容器：优先界面根节点本身。</summary>
    public static ContainerWidget? FindHost(Widget root) => root as ContainerWidget;
}
