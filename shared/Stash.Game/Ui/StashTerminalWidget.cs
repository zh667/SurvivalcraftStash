using Engine;
using Game;
using Stash.Shared.Inventory;
using Stash.Shared.Network;
using Stash.Shared.Sorting;
using Stash.Shared.Storage;

namespace Stash.Game;

/// <summary>
/// 存储终端界面：把整片网络的东西汇总成一张表，能搜、能点着取、能一键存入。
///
/// 这里**没有**用 <c>InventorySlotWidget</c>：那个控件必须绑一个真实的 <c>IInventory</c> 槽位，
/// 而终端里的一格是"跨若干容器汇总出来的一种物品"，没有对应的真实槽位。
/// 所以格子是自己搭的：方块图标 + 数量 + 可点击区域。
/// </summary>
public sealed class StashTerminalWidget : CanvasWidget
{
    private const int Columns = 8;
    private const int Rows = 5;
    private const float CellSize = 60f;
    private const float Padding = 12f;
    private const double RefreshInterval = 0.4;

    private readonly ComponentPlayer m_player;
    private readonly List<IInventory> m_containers;
    private readonly TextBoxWidget m_searchBox;
    private readonly LabelWidget m_statusLabel;
    private readonly ButtonWidget m_depositButton;
    private readonly ButtonWidget m_pageUpButton;
    private readonly ButtonWidget m_pageDownButton;
    private readonly List<Cell> m_cells = new();

    private List<NetworkEntry> m_entries = new();
    private double m_nextRefresh;
    private int m_page;
    private string m_lastQuery = string.Empty;

    public StashTerminalWidget(ComponentPlayer player, List<IInventory> containers)
    {
        m_player = player;
        m_containers = containers;

        float gridWidth = Columns * CellSize;
        float width = gridWidth + Padding * 2f;
        float height = Rows * CellSize + Padding * 3f + 96f;
        Size = new Vector2(width, height);

        Children.Add(new BevelledRectangleWidget { Size = new Vector2(width, height), BevelSize = 3f });

        var title = new LabelWidget { Text = StashText.HubName, Color = new Color(255, 255, 255, 192) };
        Children.Add(title);
        SetWidgetPosition(title, new Vector2(Padding, 12f));

        m_searchBox = new TextBoxWidget { Size = new Vector2(gridWidth - 150f, 36f) };
        Children.Add(m_searchBox);
        SetWidgetPosition(m_searchBox, new Vector2(Padding, 44f));

        m_depositButton = new BevelledButtonWidget { Text = StashText.Deposit, Size = new Vector2(70f, 36f) };
        Children.Add(m_depositButton);
        SetWidgetPosition(m_depositButton, new Vector2(Padding + gridWidth - 140f, 44f));

        m_pageUpButton = new BevelledButtonWidget { Text = "▲", Size = new Vector2(32f, 36f) };
        Children.Add(m_pageUpButton);
        SetWidgetPosition(m_pageUpButton, new Vector2(Padding + gridWidth - 66f, 44f));

        m_pageDownButton = new BevelledButtonWidget { Text = "▼", Size = new Vector2(32f, 36f) };
        Children.Add(m_pageDownButton);
        SetWidgetPosition(m_pageDownButton, new Vector2(Padding + gridWidth - 32f, 44f));

        for (int i = 0; i < Columns * Rows; i++)
        {
            var cell = new Cell();
            Children.Add(cell);
            SetWidgetPosition(cell, new Vector2(
                Padding + i % Columns * CellSize,
                Padding + 76f + i / Columns * CellSize));
            m_cells.Add(cell);
        }

        m_statusLabel = new LabelWidget { Color = new Color(255, 255, 255, 160) };
        Children.Add(m_statusLabel);
        SetWidgetPosition(m_statusLabel, new Vector2(Padding, height - 26f));

        Refresh();
    }

    public override void Update()
    {
        double now = m_player.Project.FindSubsystem<SubsystemTime>()?.GameTime ?? 0.0;
        if (m_searchBox.Text != m_lastQuery)
        {
            m_lastQuery = m_searchBox.Text ?? string.Empty;
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

        if (m_pageDownButton.IsClicked && (m_page + 1) * m_cells.Count < m_entries.Count)
        {
            m_page++;
            Refresh();
        }

        if (m_depositButton.IsClicked)
        {
            Deposit();
        }

        for (int i = 0; i < m_cells.Count; i++)
        {
            if (m_cells[i].TakeClicked && m_cells[i].Value != 0)
            {
                Extract(m_cells[i].Value);
                break;
            }
        }
    }

    private void Refresh()
    {
        PlayerStashData settings = StashStore.ForCurrentPlayer();
        var containers = new List<NetworkContainer>(m_containers.Count);
        for (int i = 0; i < m_containers.Count; i++)
        {
            containers.Add(new NetworkContainer(i, ReadSlots(m_containers[i])));
        }

        List<NetworkEntry> all = NetworkAggregate.Build(containers, StashOperations.Catalog, settings.SortMethod);
        NetworkSearch.Query query = NetworkSearch.Parse(m_searchBox.Text);

        m_entries = new List<NetworkEntry>();
        SubsystemTerrain terrain = m_player.Project.FindSubsystem<SubsystemTerrain>();
        foreach (NetworkEntry entry in all)
        {
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(entry.Value)];
            if (query.Matches(block.GetDisplayName(terrain, entry.Value), block.GetCategory(entry.Value)))
            {
                m_entries.Add(entry);
            }
        }

        int maxPage = Math.Max(0, (m_entries.Count - 1) / m_cells.Count);
        m_page = MathUtils.Clamp(m_page, 0, maxPage);

        int first = m_page * m_cells.Count;
        for (int i = 0; i < m_cells.Count; i++)
        {
            int index = first + i;
            if (index < m_entries.Count)
            {
                m_cells[i].Show(m_entries[index].Value, m_entries[index].Count);
            }
            else
            {
                m_cells[i].Clear();
            }
        }

        m_statusLabel.Text = StashText.TerminalStatus(m_containers.Count, m_entries.Count, m_page + 1, maxPage + 1);
    }

    private static List<SlotSnapshot> ReadSlots(IInventory inventory)
    {
        var slots = new List<SlotSnapshot>(inventory.SlotsCount);
        for (int i = 0; i < inventory.SlotsCount; i++)
        {
            int count = inventory.GetSlotCount(i);
            slots.Add(count > 0 ? new SlotSnapshot(inventory.GetSlotValue(i), count) : SlotSnapshot.Empty);
        }

        return slots;
    }

    private (List<InventorySnapshot> Containers, InventorySnapshot Player) Snapshot()
    {
        var containers = new List<InventorySnapshot>(m_containers.Count);
        foreach (IInventory inventory in m_containers)
        {
            containers.Add(GameInventory.Snapshot(inventory, GameInventory.Range(0, inventory.SlotsCount), StashOperations.Catalog));
        }

        IInventory playerInventory = m_player.ComponentMiner.Inventory;
        InventorySnapshot player = GameInventory.Snapshot(
            playerInventory, GameInventory.Range(0, playerInventory.SlotsCount), StashOperations.Catalog);

        return (containers, player);
    }

    private void Extract(int value)
    {
        (List<InventorySnapshot> containers, InventorySnapshot player) = Snapshot();
        int stackSize = Math.Max(1, StashOperations.Catalog.GetMaxStacking(value));

        NetworkTransferPlan transfer = NetworkTransfer.PlanExtract(containers, player, value, stackSize);
        Execute(transfer, StashText.TerminalTaken, StashText.TerminalNoRoom);
    }

    private void Deposit()
    {
        (List<InventorySnapshot> containers, InventorySnapshot player) = Snapshot();
        PlayerStashData settings = StashStore.ForCurrentPlayer();

        NetworkTransferPlan transfer = NetworkTransfer.PlanDeposit(containers, player, settings.LockedSlotSet());
        Execute(transfer, StashText.TerminalStored, StashText.TerminalFull);
    }

    private void Execute(NetworkTransferPlan transfer, Func<int, string> success, string failure)
    {
        if (transfer.IsEmpty)
        {
            Notify(failure);
            return;
        }

        var plan = new StashPlan();
        foreach ((int containerIndex, IReadOnlyList<SlotAssignment> assignments) in transfer.ContainerAssignments)
        {
            if (containerIndex >= 0 && containerIndex < m_containers.Count)
            {
                plan.Add(m_containers[containerIndex], assignments);
            }
        }

        plan.Add(m_player.ComponentMiner.Inventory, transfer.PlayerAssignments);
        StashPlatform.Current.Execute(plan);

        Notify(success(transfer.MovedCount));
        m_nextRefresh = 0;
    }

    private void Notify(string message)
    {
        m_player.ComponentGui.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
    }

    /// <summary>终端里的一格：图标 + 数量 + 点击区域。</summary>
    private sealed class Cell : CanvasWidget
    {
        private readonly BevelledRectangleWidget m_background;
        private readonly BlockIconWidget m_icon;
        private readonly LabelWidget m_count;
        private readonly ClickableWidget m_clickable;

        public Cell()
        {
            Size = new Vector2(CellSize, CellSize);

            m_background = new BevelledRectangleWidget
            {
                Size = new Vector2(CellSize - 4f, CellSize - 4f),
                BevelSize = 1f,
                CenterColor = new Color(0, 0, 0, 64),
            };
            Children.Add(m_background);

            m_icon = new BlockIconWidget { Size = new Vector2(CellSize - 16f, CellSize - 16f) };
            Children.Add(m_icon);

            m_count = new LabelWidget
            {
                Color = Color.White,
                HorizontalAlignment = WidgetAlignment.Far,
                VerticalAlignment = WidgetAlignment.Far,
            };
            Children.Add(m_count);

            m_clickable = new ClickableWidget
            {
                HorizontalAlignment = WidgetAlignment.Stretch,
                VerticalAlignment = WidgetAlignment.Stretch,
            };
            Children.Add(m_clickable);
        }

        public int Value { get; private set; }

        public bool TakeClicked => m_clickable.IsClicked;

        public void Show(int value, long count)
        {
            Value = value;
            m_icon.Value = value;
            m_icon.IsVisible = true;
            m_count.Text = StashAimPreview.FormatCount((int)Math.Min(count, int.MaxValue));
            m_count.IsVisible = true;
            m_background.IsVisible = true;
        }

        public void Clear()
        {
            Value = 0;
            m_icon.IsVisible = false;
            m_count.IsVisible = false;
            m_background.IsVisible = true;
        }
    }
}
