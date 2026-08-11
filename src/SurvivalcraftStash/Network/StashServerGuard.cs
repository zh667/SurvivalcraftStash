using Game.NetWork;
using Game.NetWork.Packages;
using Stash.Shared.Inventory;
using Stash.Shared.Items;

namespace Game;

/// <summary>
/// 服务端对整理请求的校验。原则：**只允许重排，不允许凭空产生或销毁物品**。
///
/// 这条守恒检查是整个联机方案的安全底线——包里带的是"目标布局"，
/// 没有它，改包的客户端就能把任意物品刷出来。
/// </summary>
public static class StashServerGuard
{
    public sealed record ResolvedPart(IInventory Inventory, List<SlotAssignment> Assignments);

    public static bool Validate(
        SubsystemInventories inventories,
        IReadOnlyList<(int InventoryId, List<SlotAssignment> Assignments)> parts,
        Client? from,
        out List<ResolvedPart> resolved,
        out string reason)
    {
        resolved = new List<ResolvedPart>();
        reason = string.Empty;

        if (parts.Count == 0)
        {
            reason = "空请求";
            return false;
        }

        var before = new Dictionary<int, long>();
        var after = new Dictionary<int, long>();

        foreach ((int inventoryId, List<SlotAssignment> assignments) in parts)
        {
            IInventory inventory = inventories.GetInventoryById(inventoryId);
            if (inventory == null)
            {
                reason = $"库存 {inventoryId} 不存在";
                return false;
            }

            if (inventory is ComponentCreativeInventory)
            {
                reason = "不允许对创造物品栏执行整理";
                return false;
            }

            if (from != null && ComponentInventoryPackage.IsOwnedByOtherPlayer(inventory, from, out _))
            {
                reason = "不允许操作其他玩家的库存";
                return false;
            }

            var seen = new HashSet<int>();
            foreach (SlotAssignment assignment in assignments)
            {
                if (!ComponentInventoryPackage.IsSlotIndexValid(inventory, assignment.SlotIndex))
                {
                    reason = $"槽位下标越界：{assignment.SlotIndex}";
                    return false;
                }

                if (!seen.Add(assignment.SlotIndex))
                {
                    reason = $"同一槽位被赋值多次：{assignment.SlotIndex}";
                    return false;
                }

                if (assignment.Count < 0)
                {
                    reason = "数量为负";
                    return false;
                }

                if (assignment.Count > 0)
                {
                    int capacity = inventory.GetSlotCapacity(assignment.SlotIndex, assignment.Value);
                    if (capacity <= 0 || assignment.Count > capacity)
                    {
                        reason = $"超过槽位容量：{assignment.Count} > {capacity}";
                        return false;
                    }

                    Accumulate(after, assignment.Value, assignment.Count);
                }

                int currentCount = inventory.GetSlotCount(assignment.SlotIndex);
                if (currentCount > 0)
                {
                    Accumulate(before, inventory.GetSlotValue(assignment.SlotIndex), currentCount);
                }
            }

            resolved.Add(new ResolvedPart(inventory, assignments));
        }

        if (!SameTotals(before, after, out string diff))
        {
            reason = $"物品不守恒：{diff}";
            return false;
        }

        return true;
    }

    private static void Accumulate(Dictionary<int, long> totals, int value, int count)
    {
        // 光照位不是物品属性，归一化后再计数，免得同一种物品被当成两种。
        int key = ItemValue.ReplaceLight(value, 0);
        totals[key] = totals.GetValueOrDefault(key) + count;
    }

    private static bool SameTotals(Dictionary<int, long> before, Dictionary<int, long> after, out string diff)
    {
        foreach ((int value, long count) in before)
        {
            if (after.GetValueOrDefault(value) != count)
            {
                diff = $"物品 {value} 由 {count} 变为 {after.GetValueOrDefault(value)}";
                return false;
            }
        }

        foreach ((int value, long count) in after)
        {
            if (!before.ContainsKey(value))
            {
                diff = $"凭空出现物品 {value} × {count}";
                return false;
            }
        }

        diff = string.Empty;
        return true;
    }
}
