using Engine;
using Game;
using Stash.Shared.Network;

namespace Stash.Game;

/// <summary>
/// 存储终端界面：左边是整片网络的物品（排好序、可翻页、可搜索），右边是玩家物品栏或背包。
///
/// **左边这些格子是真实槽位**——每个 <see cref="InventorySlotWidget"/> 直接绑到
/// "某个箱子的第几格"上，所以取放完全走原版那套拖放逻辑：能拖、能拆半组、能单个拿，
/// 联机下也自动走原版的服务端权威流程，不需要我们自己发包。
///
/// 早先这里是"每格一种物品的汇总视图"（自己画图标和数量），结果只能取不能存、
/// 而且一次只能取一组。汇总视图看着聪明，但它没有真实槽位可绑，
/// 等于把原版免费给的交互全丢了。
/// </summary>
public sealed class StashTerminalWidget : CanvasWidget
{
    private const int Columns = 8;
    private const int Rows = 5;
    private const float CellSize = 60f;
    private const int PlayerColumns = 4;
    private const int PlayerRows = 4;
    private const float PlayerSlotSize = 60f;
    private const float Padding = 12f;
    private const float HeaderHeight = 84f;
    private const double RefreshInterval = 0.4;

    /// <summary>玩家物品栏在容器界面里从第 10 格开始显示（0~9 是快捷栏，原版也是这么做的）。</summary>
    private const int PlayerFirstSlot = 10;

    private readonly ComponentPlayer m_player;
    private readonly List<IInventory> m_containers;
    private readonly TextBoxWidget m_searchBox;
    private readonly LabelWidget m_statusLabel;
    private readonly ButtonWidget m_pageUpButton;
    private readonly ButtonWidget m_pageDownButton;
    private readonly ButtonWidget? m_sideToggleButton;
    private readonly List<InventorySlotWidget> m_cells = new();
    private readonly List<InventorySlotWidget> m_sideSlots = new();
    private readonly List<(IInventory Inventory, int Slot)> m_view = new();

    private double m_nextRefresh;
    private int m_page;
    private string m_lastQuery = string.Empty;
    private bool m_showBackpack;

    public StashTerminalWidget(ComponentPlayer player, List<IInventory> containers, string title)
    {
        m_player = player;
        m_containers = containers;

        float gridWidth = Columns * CellSize;
        float sideWidth = PlayerColumns * PlayerSlotSize;
        float width = Padding * 3f + gridWidth + sideWidth;
        float height = Padding * 2f + HeaderHeight + MathUtils.Max(Rows * CellSize, PlayerRows * PlayerSlotSize) + 30f;
        Size = new Vector2(width, height);

        Children.Add(new BevelledRectangleWidget { Size = new Vector2(width, height), BevelSize = 3f });

        AddLabel(title, new Vector2(Padding, 12f));

        m_searchBox = new TextBoxWidget { Size = new Vector2(gridWidth - 76f, 34f) };
        Children.Add(m_searchBox);
        SetWidgetPosition(m_searchBox, new Vector2(Padding, 42f));

        m_pageUpButton = new BevelledButtonWidget { Text = "▲", Size = new Vector2(34f, 34f) };
        Children.Add(m_pageUpButton);
        SetWidgetPosition(m_pageUpButton, new Vector2(Padding + gridWidth - 72f, 42f));

        m_pageDownButton = new BevelledButtonWidget { Text = "▼", Size = new Vector2(34f, 34f) };
        Children.Add(m_pageDownButton);
        SetWidgetPosition(m_pageDownButton, new Vector2(Padding + gridWidth - 36f, 42f));

        // 右侧在"物品栏 / 背包"之间切换：背包里的东西也要能方便地丢进网络。
        if (StashBackpack.GetWornTier(player) != null)
        {
            m_sideToggleButton = new BevelledButtonWidget
            {
                Text = StashText.PlayerInventory,
                Size = new Vector2(sideWidth, 30f),
            };
            Children.Add(m_sideToggleButton);
            SetWidgetPosition(m_sideToggleButton, new Vector2(Padding * 2f + gridWidth, 46f));
        }
        else
        {
            AddLabel(StashText.PlayerInventory, new Vector2(Padding * 2f + gridWidth, 52f));
        }

        BuildGrid(m_cells, Columns, Rows, CellSize, new Vector2(Padding, Padding + HeaderHeight));
        BuildGrid(m_sideSlots, PlayerColumns, PlayerRows, PlayerSlotSize,
            new Vector2(Padding * 2f + gridWidth, Padding + HeaderHeight));

        m_statusLabel = new LabelWidget { Color = new Color(255, 255, 255, 160) };
        Children.Add(m_statusLabel);
        SetWidgetPosition(m_statusLabel, new Vector2(Padding, height - 26f));

        BindSide();
        Refresh();
    }

    private void AddLabel(string text, Vector2 position)
    {
        var label = new LabelWidget { Text = text, Color = new Color(255, 255, 255, 192) };
        Children.Add(label);
        SetWidgetPosition(label, position);
    }

    private void BuildGrid(List<InventorySlotWidget> into, int columns, int rows, float slotSize, Vector2 position)
    {
        var grid = new GridPanelWidget { ColumnsCount = columns, RowsCount = rows };

        for (int i = 0; i < columns * rows; i++)
        {
            var slot = new InventorySlotWidget { Size = new Vector2(slotSize, slotSize) };
            grid.Children.Add(slot);
            grid.SetWidgetCell(slot, new Point2(i % columns, i / columns));
            into.Add(slot);
        }

        Children.Add(grid);
        SetWidgetPosition(grid, position);
    }

    public override void Update()
    {
        // 搜索框有焦点时把按键吃掉，免得打字触发原版和别的 Mod 的热键。
        StashHotkeys.TypingInProgress = m_searchBox.HasFocus;

        double now = m_player.Project.FindSubsystem<SubsystemTime>()?.GameTime ?? 0.0;
        string query = m_searchBox.Text ?? string.Empty;

        if (query != m_lastQuery)
        {
            m_lastQuery = query;
            m_page = 0;
            Refresh();
        }
        else if (now >= m_nextRefresh)
        {
            m_nextRefresh = now + RefreshInterval;
            Refresh();
        }

        if (m_pageUpButton.IsClicked && m_page > 0)
        {
            m_page--;
            Refresh();
        }

        if (m_pageDownButton.IsClicked && (m_page + 1) * m_cells.Count < m_view.Count)
        {
            m_page++;
            Refresh();
        }

        if (m_sideToggleButton is { IsClicked: true })
        {
            m_showBackpack = !m_showBackpack;
            m_sideToggleButton.Text = m_showBackpack ? StashText.OpenBackpack : StashText.PlayerInventory;
            BindSide();
        }
    }

    /// <summary>右侧绑玩家物品栏或背包。</summary>
    private void BindSide()
    {
        IInventory inventory = m_player.ComponentMiner.Inventory;
        int firstSlot = PlayerFirstSlot;

        if (m_showBackpack && StashBackpack.GetInventory(m_player) is { } backpack)
        {
            inventory = backpack;
            firstSlot = 0;
        }

        for (int i = 0; i < m_sideSlots.Count; i++)
        {
            int slot = firstSlot + i;
            if (slot < inventory.SlotsCount)
            {
                m_sideSlots[i].AssignInventorySlot(inventory, slot);
                m_sideSlots[i].IsVisible = true;
            }
            else
            {
                m_sideSlots[i].IsVisible = false;
            }
        }
    }

    /// <summary>
    /// 重算左边要显示哪些槽位：命中搜索的非空槽在前（排好序），空槽垫在后面——
    /// 空槽是留给玩家往里放东西的，没有它就只能取不能存。
    /// </summary>
    private void Refresh()
    {
        NetworkSearch.Query query = NetworkSearch.Parse(m_lastQuery);
        SubsystemTerrain terrain = m_player.Project.FindSubsystem<SubsystemTerrain>();

        var filled = new List<(IInventory Inventory, int Slot, int Value, int Count)>();
        var empty = new List<(IInventory Inventory, int Slot)>();

        foreach (IInventory inventory in m_containers)
        {
            for (int slot = 0; slot < inventory.SlotsCount; slot++)
            {
                int count = inventory.GetSlotCount(slot);
                if (count <= 0)
                {
                    empty.Add((inventory, slot));
                    continue;
                }

                int value = inventory.GetSlotValue(slot);
                Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
                if (query.Matches(block.GetDisplayName(terrain, value), block.GetCategory(value)))
                {
                    filled.Add((inventory, slot, value, count));
                }
            }
        }

        filled.Sort(CompareForDisplay);

        m_view.Clear();
        foreach ((IInventory inventory, int slot, int _, int _) in filled)
        {
            m_view.Add((inventory, slot));
        }

        foreach ((IInventory inventory, int slot) in empty)
        {
            m_view.Add((inventory, slot));
        }

        int maxPage = Math.Max(0, (m_view.Count - 1) / m_cells.Count);
        m_page = MathUtils.Clamp(m_page, 0, maxPage);

        int first = m_page * m_cells.Count;
        for (int i = 0; i < m_cells.Count; i++)
        {
            int index = first + i;
            if (index < m_view.Count)
            {
                m_cells[i].AssignInventorySlot(m_view[index].Inventory, m_view[index].Slot);
                m_cells[i].IsVisible = true;
            }
            else
            {
                m_cells[i].IsVisible = false;
            }
        }

        m_statusLabel.Text = StashText.TerminalStatus(m_containers.Count, filled.Count, m_page + 1, maxPage + 1);
    }

    private static int CompareForDisplay(
        (IInventory Inventory, int Slot, int Value, int Count) a,
        (IInventory Inventory, int Slot, int Value, int Count) b)
    {
        Stash.Shared.Items.IItemCatalog catalog = StashOperations.Catalog;

        int c = string.Compare(
            catalog.GetCategory(a.Value), catalog.GetCategory(b.Value), StringComparison.CurrentCultureIgnoreCase);
        if (c != 0)
        {
            return c;
        }

        c = catalog.GetDisplayOrder(a.Value).CompareTo(catalog.GetDisplayOrder(b.Value));
        if (c != 0)
        {
            return c;
        }

        c = Terrain.ExtractContents(a.Value).CompareTo(Terrain.ExtractContents(b.Value));
        if (c != 0)
        {
            return c;
        }

        c = Terrain.ExtractData(a.Value).CompareTo(Terrain.ExtractData(b.Value));
        return c != 0 ? c : b.Count.CompareTo(a.Count);
    }
}
