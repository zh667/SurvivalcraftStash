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

    public StashContainerWidget(
        string title,
        IInventory container,
        int firstSlot,
        int slots,
        int columns,
        float slotSize,
        IInventory playerInventory)
    {
        m_container = container;

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
        AddLabel(StashText.PlayerInventory, new Vector2(Padding * 2f + containerWidth, 14f));

        AddGrid(container, firstSlot, slots, columns, rows, slotSize,
            new Vector2(Padding, Padding + LabelHeight));

        AddGrid(playerInventory, PlayerFirstSlot, PlayerColumns * PlayerRows, PlayerColumns, PlayerRows, PlayerSlotSize,
            new Vector2(Padding * 2f + containerWidth, Padding + LabelHeight));
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

    private void AddGrid(IInventory inventory, int firstSlot, int slots, int columns, int rows, float slotSize, Vector2 position)
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
            grid.Children.Add(slotWidget);
            grid.SetWidgetCell(slotWidget, new Point2(i % columns, i / columns));
        }

        Children.Add(grid);
        SetWidgetPosition(grid, position);
    }

    public override void Update()
    {
        // 容器被挖掉（或玩家实体没了）时关掉界面，和原版 ChestWidget 一样。
        if (m_container is Component component && !component.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
