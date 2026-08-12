using Game;

namespace Stash.Game;

/// <summary>
/// 把界面上"玩家物品栏"那一块网格在 **物品栏 ↔ 背包** 之间来回切。
///
/// 用途是给**原版**界面（原版箱子、熔炉、物品栏…）补一个进背包的入口——
/// 我们自己的分级箱子界面早就有了，实机反馈说原版箱子也想要。
///
/// 做法是直接改那些 <see cref="InventorySlotWidget"/> 绑的库存和槽位，
/// 不动布局、不加控件。原版那块网格通常只有 16 格，而钻石背包有 32 格，
/// 所以背包按**页**显示：物品栏 → 背包 1/2 → 背包 2/2 → 物品栏，循环。
/// </summary>
public sealed class StashSideToggle
{
    private readonly ComponentPlayer m_player;
    private readonly PanelContainer m_inventory;

    /// <summary>0 = 物品栏；1..N = 背包第几页。</summary>
    private int m_state;

    public StashSideToggle(ComponentPlayer player, PanelContainer inventory)
    {
        m_player = player;
        m_inventory = inventory;
    }

    /// <summary>一屏能放几格。就是原版那块网格的格子数。</summary>
    private int PageSize => m_inventory.Widgets.Count;

    private int BackpackSlots => StashBackpack.GetWornTier(m_player)?.SlotsCount ?? 0;

    private int PageCount => PageSize > 0 ? (BackpackSlots + PageSize - 1) / PageSize : 0;

    public string Label
    {
        get
        {
            if (m_state == 0)
            {
                return StashText.OpenBackpack;
            }

            return PageCount > 1
                ? StashText.BackpackPage(m_state, PageCount)
                : StashText.PlayerInventory;
        }
    }

    /// <summary>点一下：物品栏 → 背包各页 → 回物品栏。</summary>
    public void Advance()
    {
        m_state = m_state >= PageCount ? 0 : m_state + 1;
        Apply();
    }

    private void Apply()
    {
        IInventory? backpack = StashBackpack.GetInventory(m_player);
        if (m_state > 0 && backpack == null)
        {
            m_state = 0;
        }

        for (int i = 0; i < m_inventory.Widgets.Count; i++)
        {
            InventorySlotWidget widget = m_inventory.Widgets[i];

            if (m_state == 0)
            {
                widget.AssignInventorySlot(m_inventory.Inventory, m_inventory.SlotIndexes[i]);
                widget.IsVisible = true;
                continue;
            }

            int slot = (m_state - 1) * PageSize + i;
            if (backpack != null && slot < BackpackSlots && slot < backpack.SlotsCount)
            {
                widget.AssignInventorySlot(backpack, slot);
                widget.IsVisible = true;
            }
            else
            {
                // 这一档背包没有这么多格：藏起来，别让玩家往锁住的格子里塞东西。
                widget.IsVisible = false;
            }
        }
    }
}
