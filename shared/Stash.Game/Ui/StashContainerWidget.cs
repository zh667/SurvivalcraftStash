using Engine;
using Game;
using GameEntitySystem;

namespace Stash.Game;

/// <summary>
/// 通用容器界面：左边容器网格，右边玩家背包 4×4，行列与格子尺寸都可变。
///
/// 布局思路照着原版 <c>ChestWidget.xml</c>（614×382、两块网格并排），
/// 但原版那份把 4×4 写死在 XML 里，装不下分级箱子和随身背包，所以这里用代码搭。
/// 分级箱子和随身背包共用这一个界面。
/// </summary>
public class StashContainerWidget : CanvasWidget
{
    private const float Padding = 12f;
    private const float LabelHeight = 40f;
    private const float PlayerSlotSize = 60f;
    private const int PlayerColumns = 4;
    private const int PlayerRows = 4;

    /// <summary>玩家背包在容器界面里从第 10 格开始显示（0~9 是快捷栏，原版也是这么做的）。</summary>
    private const int PlayerFirstSlot = 10;

    private readonly IInventory m_container;
    private readonly ComponentPlayer? m_player;
    private readonly IInventory m_playerInventory;
    private readonly ButtonWidget? m_sideToggleButton;
    private readonly List<InventorySlotWidget> m_sideSlots = new();
    private bool m_showBackpack;

    public StashContainerWidget(
        string title,
        IInventory container,
        int firstSlot,
        int slots,
        int columns,
        float slotSize,
        IInventory playerInventory,
        ComponentPlayer? player = null)
    {
        m_container = container;
        m_player = player;
        m_playerInventory = playerInventory;

        int rows = (slots + columns - 1) / columns;
        float containerWidth = columns * slotSize;
        float containerHeight = rows * slotSize;
        float playerWidth = PlayerColumns * PlayerSlotSize;
        float playerHeight = PlayerRows * PlayerSlotSize;

        float width = Padding * 3f + containerWidth + playerWidth;
        float height = Padding * 2f + LabelHeight + MathUtils.Max(containerHeight, playerHeight);
        Size = new Vector2(width, height);

        Children.Add(new BevelledRectangleWidget
        {
            Size = new Vector2(width, height),
            BevelSize = 3f,
        });

        AddLabel(title, new Vector2(Padding, 14f));

        // 右侧可以在"物品栏 / 背包"之间切换：背着背包时，背包里的东西也要能直接倒进箱子。
        if (player != null && StashBackpack.GetWornTier(player) != null)
        {
            m_sideToggleButton = new BevelledButtonWidget
            {
                Text = StashText.PlayerInventory,
                Size = new Vector2(playerWidth, 28f),
            };
            Children.Add(m_sideToggleButton);
            SetWidgetPosition(m_sideToggleButton, new Vector2(Padding * 2f + containerWidth, 8f));
        }
        else
        {
            AddLabel(StashText.PlayerInventory, new Vector2(Padding * 2f + containerWidth, 14f));
        }

        AddGrid(container, firstSlot, slots, columns, rows, slotSize,
            new Vector2(Padding, Padding + LabelHeight));

        AddGrid(playerInventory, PlayerFirstSlot, PlayerColumns * PlayerRows, PlayerColumns, PlayerRows, PlayerSlotSize,
            new Vector2(Padding * 2f + containerWidth, Padding + LabelHeight), m_sideSlots);
    }

    private void AddLabel(string text, Vector2 position)
    {
        var label = new LabelWidget
        {
            Text = text,
            Color = new Color(255, 255, 255, 192),
        };

        Children.Add(label);
        SetWidgetPosition(label, position);
    }

    private void AddGrid(
        IInventory inventory,
        int firstSlot,
        int slots,
        int columns,
        int rows,
        float slotSize,
        Vector2 position,
        List<InventorySlotWidget>? collect = null)
    {
        var grid = new GridPanelWidget
        {
            ColumnsCount = columns,
            RowsCount = rows,
        };

        for (int i = 0; i < slots; i++)
        {
            var slotWidget = new InventorySlotWidget
            {
                Size = new Vector2(slotSize, slotSize),
            };

            slotWidget.AssignInventorySlot(inventory, firstSlot + i);
            collect?.Add(slotWidget);
            grid.Children.Add(slotWidget);
            grid.SetWidgetCell(slotWidget, new Point2(i % columns, i / columns));
        }

        Children.Add(grid);
        SetWidgetPosition(grid, position);
    }

    public override void Update()
    {
        if (m_sideToggleButton is { IsClicked: true } && m_player != null)
        {
            m_showBackpack = !m_showBackpack;
            m_sideToggleButton.Text = m_showBackpack ? StashText.OpenBackpack : StashText.PlayerInventory;
            BindSide();
        }

        // 容器被挖掉（或玩家实体没了）时关掉界面，和原版 ChestWidget 一样。
        if (m_container is Component component && !component.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }

    private void BindSide()
    {
        IInventory inventory = m_playerInventory;
        int firstSlot = PlayerFirstSlot;

        if (m_showBackpack && m_player != null && StashBackpack.GetInventory(m_player) is { } backpack)
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
}
