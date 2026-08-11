using Game;
using Stash.Shared.Inventory;
using Stash.Shared.Items;

namespace Stash.Game;

/// <summary>
/// 游戏 <see cref="IInventory"/> 与共享层快照之间的桥。
///
/// 只用两个平台**签名一致**的成员：SlotsCount / GetSlotValue / GetSlotCount /
/// GetSlotCapacity / AddSlotItems / RemoveSlotItems。
/// 联机版特有的 SetSlotValue、AddNetSlotItems 一概不碰，否则插件版编译不过。
/// </summary>
public static class GameInventory
{
    public static InventorySnapshot Snapshot(IInventory inventory, IReadOnlyList<int> slotIndexes, IItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(slotIndexes);

        var slots = new List<SlotSnapshot>(slotIndexes.Count);
        foreach (int slotIndex in slotIndexes)
        {
            int count = inventory.GetSlotCount(slotIndex);
            slots.Add(count > 0 ? new SlotSnapshot(inventory.GetSlotValue(slotIndex), count) : SlotSnapshot.Empty);
        }

        return new InventorySnapshot(slots, slotIndexes, catalog);
    }

    /// <summary>
    /// 落地一组槽位赋值。**必须先清空再填充**：
    /// 原版 <c>AddSlotItems</c> 在"槽里已有别的物品"时会失败，一趟走完会丢东西。
    /// </summary>
    public static void Apply(IInventory inventory, IReadOnlyList<SlotAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(assignments);

        foreach (SlotAssignment assignment in assignments)
        {
            if (!IsSlotValid(inventory, assignment.SlotIndex))
            {
                continue;
            }

            int current = inventory.GetSlotCount(assignment.SlotIndex);
            if (current > 0)
            {
                inventory.RemoveSlotItems(assignment.SlotIndex, current);
            }
        }

        foreach (SlotAssignment assignment in assignments)
        {
            if (assignment.Count <= 0 || !IsSlotValid(inventory, assignment.SlotIndex))
            {
                continue;
            }

            inventory.AddSlotItems(assignment.SlotIndex, assignment.Value, assignment.Count);
        }
    }

    public static bool IsSlotValid(IInventory inventory, int slotIndex) =>
        slotIndex >= 0 && slotIndex < inventory.SlotsCount;

    /// <summary>把一段连续槽位列出来，方便构造视图。</summary>
    public static List<int> Range(int start, int count)
    {
        var list = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(start + i);
        }

        return list;
    }
}
