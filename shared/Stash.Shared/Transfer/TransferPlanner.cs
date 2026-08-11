using Stash.Shared.Inventory;
using Stash.Shared.Items;

namespace Stash.Shared.Transfer;

/// <summary>一键存入 / 一键取出的判据。</summary>
public enum TransferMode
{
    /// <summary>
    /// 智能：只送目标里已经有的物品，或命中目标"记忆槽位"的物品。
    /// 抄自 Sophisticated 的 <c>getItemStashable</c>——这样一键存入不会把你的工具和食物一起丢进箱子。
    /// </summary>
    Smart,

    /// <summary>全部：能塞多少塞多少。</summary>
    All,
}

/// <summary>两个容器之间搬运的计划结果。</summary>
public sealed record TransferPlan(
    IReadOnlyList<SlotAssignment> SourceAssignments,
    IReadOnlyList<SlotAssignment> TargetAssignments,
    int MovedCount)
{
    public bool IsEmpty => MovedCount == 0;

    public static readonly TransferPlan Empty =
        new(Array.Empty<SlotAssignment>(), Array.Empty<SlotAssignment>(), 0);
}

public static class TransferPlanner
{
    /// <summary>
    /// 把 source 里的东西尽量搬进 target。取出（从箱子拿回身上）就是把两边对调着调用。
    /// </summary>
    /// <param name="lockedSourceSlots">源容器里不参与搬运的**真实槽位下标**（锁定槽位）。</param>
    /// <param name="targetMemory">目标容器的记忆槽位：真实槽位下标 → 记住的物品值。</param>
    public static TransferPlan Plan(
        InventorySnapshot source,
        InventorySnapshot target,
        TransferMode mode,
        IReadOnlySet<int>? lockedSourceSlots = null,
        IReadOnlyDictionary<int, int>? targetMemory = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        SlotSnapshot[] src = source.CopySlots();
        SlotSnapshot[] dst = target.CopySlots();
        int moved = 0;

        HashSet<int>? memoryValues = null;
        if (targetMemory is { Count: > 0 })
        {
            memoryValues = new HashSet<int>();
            foreach (int value in targetMemory.Values)
            {
                memoryValues.Add(ItemValue.ReplaceLight(value, 0));
            }
        }

        for (int i = 0; i < src.Length; i++)
        {
            if (src[i].IsEmpty)
            {
                continue;
            }

            if (lockedSourceSlots is not null && lockedSourceSlots.Contains(source.SlotIndexAt(i)))
            {
                continue;
            }

            int value = src[i].Value;
            if (mode == TransferMode.Smart && !IsStashable(target, dst, value, memoryValues))
            {
                continue;
            }

            int remaining = src[i].Count;
            remaining -= FillExistingStacks(dst, target, value, remaining, ref moved);
            remaining -= FillEmptySlots(dst, target, targetMemory, value, remaining, ref moved);

            src[i] = remaining > 0 ? src[i] with { Count = remaining } : SlotSnapshot.Empty;
        }

        if (moved == 0)
        {
            return TransferPlan.Empty;
        }

        return new TransferPlan(source.DiffTo(src), target.DiffTo(dst), moved);
    }

    private static bool IsStashable(InventorySnapshot target, SlotSnapshot[] dst, int value, HashSet<int>? memoryValues)
    {
        if (memoryValues is not null && memoryValues.Contains(ItemValue.ReplaceLight(value, 0)))
        {
            return true;
        }

        _ = target;
        for (int i = 0; i < dst.Length; i++)
        {
            if (!dst[i].IsEmpty && ItemValue.SameItem(dst[i].Value, value))
            {
                return true;
            }
        }

        return false;
    }

    private static int FillExistingStacks(SlotSnapshot[] dst, InventorySnapshot target, int value, int amount, ref int moved)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int capacity = target.CapacityOf(value);
        int placed = 0;

        for (int i = 0; i < dst.Length && placed < amount; i++)
        {
            if (dst[i].IsEmpty || !ItemValue.SameItem(dst[i].Value, value))
            {
                continue;
            }

            int room = capacity - dst[i].Count;
            if (room <= 0)
            {
                continue;
            }

            int take = Math.Min(room, amount - placed);
            dst[i] = dst[i] with { Count = dst[i].Count + take };
            placed += take;
        }

        moved += placed;
        return placed;
    }

    private static int FillEmptySlots(
        SlotSnapshot[] dst,
        InventorySnapshot target,
        IReadOnlyDictionary<int, int>? targetMemory,
        int value,
        int amount,
        ref int moved)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int capacity = target.CapacityOf(value);
        int placed = 0;

        for (int i = 0; i < dst.Length && placed < amount; i++)
        {
            if (!dst[i].IsEmpty)
            {
                continue;
            }

            // 空的记忆槽位是给它记住的那种物品留的，别的东西不许占。
            if (targetMemory is not null
                && targetMemory.TryGetValue(target.SlotIndexAt(i), out int remembered)
                && !ItemValue.SameItem(remembered, value))
            {
                continue;
            }

            int take = Math.Min(capacity, amount - placed);
            dst[i] = new SlotSnapshot(value, take);
            placed += take;
        }

        moved += placed;
        return placed;
    }
}
