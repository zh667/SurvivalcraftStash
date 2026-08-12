using Engine;
using Engine.Input;
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
    private const int PlayerColumns = 6;
    private const float PlayerSlotSize = 50f;
    private const float Padding = 12f;
    private const float HeaderHeight = 84f;
    private const double RefreshInterval = 0.4;

    private const float SearchY = 42f;
    private const float SearchHeight = 34f;

    /// <summary>"搜索"这两个字占的宽度，输入框从这之后开始。</summary>
    private const float SearchLabelWidth = 52f;

    /// <summary>文字离输入框内边的距离，不留的话字会贴着边框。</summary>
    private const float SearchInset = 8f;

    /// <summary>玩家物品栏在容器界面里从第 10 格开始显示（0~9 是快捷栏，原版也是这么做的）。</summary>
    private const int PlayerFirstSlot = 10;

    private readonly ComponentPlayer m_player;
    private readonly List<IInventory> m_containers;
    private readonly TextBoxWidget m_searchBox;
    private readonly BevelledRectangleWidget m_searchFrame;
    private readonly LabelWidget m_searchHint;
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
    private string m_status = string.Empty;
    private bool m_showBackpack;

    public StashTerminalWidget(ComponentPlayer player, List<IInventory> containers, string title)
    {
        m_player = player;
        m_containers = containers;

        float gridWidth = Columns * CellSize;
        float sideWidth = PlayerColumns * PlayerSlotSize;

        // 右侧要能装下两种内容：玩家物品栏（去掉快捷栏那 10 格）和背包（最大档 32 格）。
        int sideSlots = MathUtils.Max(
            player.ComponentMiner.Inventory.SlotsCount - PlayerFirstSlot,
            StashBackpack.GetWornTier(player) != null ? StashBackpackTiers.MaxSlots : 0);
        int sideRows = MathUtils.Max(1, (sideSlots + PlayerColumns - 1) / PlayerColumns);

        float width = Padding * 3f + gridWidth + sideWidth;
        float height = Padding * 2f + HeaderHeight + MathUtils.Max(Rows * CellSize, sideRows * PlayerSlotSize) + 30f;
        Size = new Vector2(width, height);

        Children.Add(new BevelledRectangleWidget { Size = new Vector2(width, height), BevelSize = 3f });

        AddLabel(title, new Vector2(Padding, 12f));

        // 搜索框。原版 TextBoxWidget 是个"裸"控件——不画背景也不画边框，
        // 直接摆在面板上只能看见一根闪烁的光标（实机反馈"不容易看到"）。
        // 自己给它垫一个内凹的框，再配一行占位提示，才看得出这里能打字。
        AddLabel(StashText.Search, new Vector2(Padding, SearchY + 8f));

        float frameX = Padding + SearchLabelWidth;
        float frameWidth = gridWidth - 76f - SearchLabelWidth;

        m_searchFrame = new BevelledRectangleWidget
        {
            Size = new Vector2(frameWidth, SearchHeight),
            BevelSize = -2f, // 负值 = 内凹，看起来像个输入槽
            RoundingRadius = 3f,
            CenterColor = new Color(0, 0, 0, 110),
            IsHitTestVisible = false,
        };
        Children.Add(m_searchFrame);
        SetWidgetPosition(m_searchFrame, new Vector2(frameX, SearchY));

        // 占位提示：文字为空时显示。LabelWidget 默认 IsHitTestVisible=false，不会挡住点击。
        m_searchHint = new LabelWidget
        {
            Text = StashText.SearchHint,
            Size = new Vector2(frameWidth - SearchInset * 2f, SearchHeight),
            TextAnchor = Engine.Graphics.TextAnchor.VerticalCenter,
            Color = new Color(255, 255, 255, 90),
        };
        Children.Add(m_searchHint);
        SetWidgetPosition(m_searchHint, new Vector2(frameX + SearchInset, SearchY));

        m_searchBox = new TextBoxWidget { Size = new Vector2(frameWidth - SearchInset * 2f, SearchHeight) };
        Children.Add(m_searchBox);
        SetWidgetPosition(m_searchBox, new Vector2(frameX + SearchInset, SearchY));

        m_pageUpButton = new BevelledButtonWidget { Text = "▲", Size = new Vector2(34f, 34f) };
        Children.Add(m_pageUpButton);
        SetWidgetPosition(m_pageUpButton, new Vector2(Padding + gridWidth - 72f, SearchY));

        m_pageDownButton = new BevelledButtonWidget { Text = "▼", Size = new Vector2(34f, 34f) };
        Children.Add(m_pageDownButton);
        SetWidgetPosition(m_pageDownButton, new Vector2(Padding + gridWidth - 36f, SearchY));

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
        BuildGrid(m_sideSlots, PlayerColumns, sideRows, PlayerSlotSize,
            new Vector2(Padding * 2f + gridWidth, Padding + HeaderHeight));

        m_statusLabel = new LabelWidget { Color = new Color(255, 255, 255, 160) };
        Children.Add(m_statusLabel);
        SetWidgetPosition(m_statusLabel, new Vector2(Padding, height - 26f));

        BindSide();
        Refresh();
        m_statusLabel.Text = m_status;
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
        // 退出搜索的三条路，缺一不可（实机反馈"光标一直闪，退不出去"）：
        //   1. Esc —— 这里处理；
        //   2. 回车 —— 确认，保留已输入的内容；
        //   3. 点界面上别处 —— 这条是原版 TextBoxWidget 自带的，
        //      之前被我们在 UpdateInput 里调 input.Clear() 给弄瘸了，现在不清了。
        bool wasFocused = m_searchBox.HasFocus;
        if (wasFocused)
        {
            if (base.Input.IsKeyDownOnce(Key.Escape))
            {
                // Esc = 彻底退出：连搜索词一起清掉，列表回到全量。
                m_searchBox.Text = string.Empty;
                m_searchBox.HasFocus = false;

                // 把 Esc 吃掉，否则同一下按键会连界面一起关掉。
                // 想关界面就再按一次——那时 TypingInProgress 已经是 false 了。
                base.Input.Back = false;
                base.Input.Cancel = false;
            }
            else if (base.Input.IsKeyDownOnce(Key.Enter))
            {
                // 回车 = 确认：保留搜索词，只是把焦点放掉。
                m_searchBox.HasFocus = false;
            }
        }

        // 用**退出前**的状态：这一帧仍然按"正在打字"处理，
        // 否则上面刚吃掉的 Esc 会被本帧稍后的原版逻辑捡回去。
        StashHotkeys.TypingInProgress = wasFocused;

        m_searchHint.IsVisible = string.IsNullOrEmpty(m_searchBox.Text);
        m_searchFrame.CenterColor = m_searchBox.HasFocus
            ? new Color(20, 60, 70, 160)
            : new Color(0, 0, 0, 110);
        m_statusLabel.Text = m_searchBox.HasFocus ? StashText.SearchExit : m_status;

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

        // 背包能露出几格看**穿着的档位**（铜 16 / 铁 24 / 钻石 32），
        // 不是组件那 32 格的物理上限——超出档位的格子是锁住的。
        int usable = inventory.SlotsCount - firstSlot;

        if (m_showBackpack && StashBackpack.GetInventory(m_player) is { } backpack)
        {
            inventory = backpack;
            firstSlot = 0;
            usable = StashBackpack.GetWornTier(m_player)?.SlotsCount ?? 0;
        }

        for (int i = 0; i < m_sideSlots.Count; i++)
        {
            int slot = firstSlot + i;
            if (i < usable && slot < inventory.SlotsCount)
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
        // 先把网络里同种物品的零散堆并起来，再算显示。
        // 玩家把东西放回终端时，原版拖放只会丢进松手的那一格，哪怕别处已经有半堆同样的
        // （实机反馈"取出一部分再放回去没有自动堆叠"）。没什么可并的时候这一步不产生任何改动。
        StashOperations.Compact(m_containers);

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
                if (query.Matches(
                    block.GetDisplayName(terrain, value),
                    block.GetCategory(value),
                    EnglishIdOf(block)))
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

        // 只更新缓存的文本，**不要**直接写 Label。
        // Update() 每帧按"搜索框有没有焦点"决定显示哪一句；这里再写一次的话，
        // 每 0.4 秒刷新一次就会插进来一帧别的字——看起来就是底下那行白字一直在闪。
        m_status = StashText.TerminalStatus(m_containers.Count, filled.Count, m_page + 1, maxPage + 1);
    }

    /// <summary>
    /// 拿来当"英文名"用的标识。
    ///
    /// 游戏运行时只加载当前语言，拿不到英文显示名；但 <c>CraftingId</c>（"copperingot"）
    /// 和类名（"CopperIngotBlock"）本来就是英文单词拼出来的，合起来足够玩家用英文搜。
    /// </summary>
    private static string EnglishIdOf(Block block) => block.CraftingId + " " + block.GetType().Name;

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
