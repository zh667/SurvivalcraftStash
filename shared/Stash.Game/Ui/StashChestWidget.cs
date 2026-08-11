using Engine;
using Game;
using GameEntitySystem;

namespace Stash.Game;

/// <summary>
/// 分级箱子的界面。按档位算出行列与格子大小，用代码搭出来。
///
/// 布局照着原版 <c>ChestWidget.xml</c> 的思路（左边容器、右边玩家背包 4×4、外面一圈斜角矩形），
/// 只是行列数和格子尺寸随档位变化——原版那份是写死 4×4 的，装不下我们的箱子。
/// </summary>
public sealed class StashChestWidget : CanvasWidget
{
    private const float Padding = 12f;
    private const float LabelHeight = 40f;
    private const float PlayerSlotSize = 60f;
    private const int PlayerColumns = 4;
    private const int PlayerRows = 4;

    /// <summary>玩家背包在界面里从第 10 格开始显示（0~9 是快捷栏，原版也是这么做的）。</summary>
    private const int PlayerFirstSlot = 10;

    private readonly IInventory m_chest;

    public StashChestWidget(IInventory playerInventory, IInventory chestInventory, StashChestTier tier)
    {
        m_chest = chestInventory;

        float chestWidth = tier.Columns * tier.SlotSize;
        float chestHeight = tier.Rows * tier.SlotSize;
        float playerWidth = PlayerColumns * PlayerSlotSize;
        float playerHeight = PlayerRows * PlayerSlotSize;

        float width = Padding * 3f + chestWidth + playerWidth;
        float height = Padding * 2f + LabelHeight + MathUtils.Max(chestHeight, playerHeight);
        Size = new Vector2(width, height);

        Children.Add(new BevelledRectangleWidget
        {
            Size = new Vector2(width, height),
            BevelSize = 3f,
        });

        AddLabel(StashText.ChestName(tier), new Vector2(Padding, 14f));
        AddLabel(StashText.PlayerInventory, new Vector2(Padding * 2f + chestWidth, 14f));

        AddGrid(chestInventory, 0, tier.Columns, tier.Rows, tier.SlotSize,
            new Vector2(Padding, Padding + LabelHeight));

        AddGrid(playerInventory, PlayerFirstSlot, PlayerColumns, PlayerRows, PlayerSlotSize,
            new Vector2(Padding * 2f + chestWidth, Padding + LabelHeight));
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

    private void AddGrid(IInventory inventory, int firstSlot, int columns, int rows, float slotSize, Vector2 position)
    {
        var grid = new GridPanelWidget
        {
            ColumnsCount = columns,
            RowsCount = rows,
        };

        int slot = firstSlot;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var slotWidget = new InventorySlotWidget
                {
                    Size = new Vector2(slotSize, slotSize),
                };

                slotWidget.AssignInventorySlot(inventory, slot++);
                grid.Children.Add(slotWidget);
                grid.SetWidgetCell(slotWidget, new Point2(column, row));
            }
        }

        Children.Add(grid);
        SetWidgetPosition(grid, position);
    }

    public override void Update()
    {
        // 箱子被挖掉时关掉界面，和原版 ChestWidget 一样。
        if (m_chest is Component component && !component.IsAddedToProject)
        {
            ParentWidget?.Children.Remove(this);
        }
    }
}
