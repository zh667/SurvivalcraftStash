using Stash.Shared.Inventory;
using Stash.Shared.Items;

namespace Stash.Shared.Sorting;

/// <summary>
/// 整理的核心：纯函数。输入一份槽位快照，输出**最小的**槽位赋值序列。
///
/// 为什么是"先算目标布局，再算差分"而不是逐步搬运：
/// 联机版每次槽位变更都会产生一次同步推送，逐步搬运既慢又容易被反作弊拦；
/// 算好目标布局后只发差分，包小、可回滚、可撤销。
/// （IPN 因为是纯客户端模组，必须把布局翻译成合法点击序列；我们不需要那一层。）
/// </summary>
public static class StashSorter
{
    /// <param name="slots">当前槽位快照。</param>
    /// <param name="catalog">物品元数据来源。</param>
    /// <param name="method">排序方式。</param>
    /// <param name="lockedSlots">不参与整理的槽位（玩家锁定 / nosort）。</param>
    /// <param name="memorySlots">记忆槽位：槽位 → 记住的物品值。这些槽位固定不动，并优先吸收同种物品。</param>
    /// <returns>只包含"结果与原状态不同"的赋值指令。</returns>
    public static IReadOnlyList<SlotAssignment> Plan(
        IReadOnlyList<SlotSnapshot> slots,
        IItemCatalog catalog,
        SortMethod method,
        IReadOnlySet<int>? lockedSlots = null,
        IReadOnlyDictionary<int, int>? memorySlots = null)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(catalog);

        var target = new SlotSnapshot[slots.Count];
        var movable = new List<int>(slots.Count);
        var pool = new List<SlotSnapshot>();

        // 第一趟：分出"固定槽位"和"可动槽位"，并把可动槽位的物品收进池子。
        for (int i = 0; i < slots.Count; i++)
        {
            bool locked = lockedSlots is not null && lockedSlots.Contains(i);
            bool remembered = memorySlots is not null && memorySlots.ContainsKey(i);

            if (locked || remembered)
            {
                target[i] = slots[i];
                continue;
            }

            if (!slots[i].IsEmpty)
            {
                pool.Add(slots[i]);
            }

            target[i] = SlotSnapshot.Empty;
            movable.Add(i);
        }

        // 第二趟：记忆槽位优先吸收同种物品（空着也认它记住的那种）。
        if (memorySlots is not null)
        {
            foreach ((int slotIndex, int rememberedValue) in memorySlots)
            {
                if (slotIndex < 0 || slotIndex >= slots.Count)
                {
                    continue;
                }

                SlotSnapshot current = target[slotIndex];
                if (!current.IsEmpty && !ItemValue.SameItem(current.Value, rememberedValue))
                {
                    // 记忆槽位里放着别的东西：当作锁定，不动它。
                    continue;
                }

                int capacity = catalog.GetMaxStacking(rememberedValue);
                int have = current.IsEmpty ? 0 : current.Count;
                int room = capacity - have;
                if (room <= 0)
                {
                    continue;
                }

                int taken = TakeFromPool(pool, rememberedValue, room);
                if (taken > 0)
                {
                    target[slotIndex] = new SlotSnapshot(rememberedValue, have + taken);
                }
            }
        }

        // 第三趟：池子里的东西合并成整栈，再排序。
        List<SlotSnapshot> stacks = MergeIntoStacks(pool, catalog);
        stacks.Sort(CreateComparer(catalog, method));

        // 第四趟：按可动槽位的自然顺序铺开。
        int slotCursor = 0;
        foreach (SlotSnapshot stack in stacks)
        {
            if (slotCursor >= movable.Count)
            {
                // 理论上不会发生：合并只会让占用格数变少或持平。
                // 真发生了说明容量数据不一致（例如 catalog 返回的堆叠上限比实际小），
                // 这时宁可什么都不做，也不能把物品挤掉。
                return Array.Empty<SlotAssignment>();
            }

            target[movable[slotCursor++]] = stack;
        }

        return Diff(slots, target);
    }

    private static int TakeFromPool(List<SlotSnapshot> pool, int value, int wanted)
    {
        int taken = 0;
        for (int i = pool.Count - 1; i >= 0 && taken < wanted; i--)
        {
            if (!ItemValue.SameItem(pool[i].Value, value))
            {
                continue;
            }

            int move = Math.Min(wanted - taken, pool[i].Count);
            taken += move;

            int left = pool[i].Count - move;
            if (left == 0)
            {
                pool.RemoveAt(i);
            }
            else
            {
                pool[i] = pool[i] with { Count = left };
            }
        }

        return taken;
    }

    private static List<SlotSnapshot> MergeIntoStacks(List<SlotSnapshot> pool, IItemCatalog catalog)
    {
        var totals = new Dictionary<int, long>();
        var order = new List<int>();

        foreach (SlotSnapshot slot in pool)
        {
            // 光照位不是物品属性，归一化后再聚合，避免同物品分成两堆。
            int key = ItemValue.ReplaceLight(slot.Value, 0);
            if (!totals.TryGetValue(key, out long sum))
            {
                order.Add(key);
                sum = 0;
            }

            totals[key] = sum + slot.Count;
        }

        var stacks = new List<SlotSnapshot>();
        foreach (int value in order)
        {
            long remaining = totals[value];
            int capacity = Math.Max(1, catalog.GetMaxStacking(value));

            while (remaining > 0)
            {
                int take = (int)Math.Min(capacity, remaining);
                stacks.Add(new SlotSnapshot(value, take));
                remaining -= take;
            }
        }

        return stacks;
    }

    private static Comparison<SlotSnapshot> CreateComparer(IItemCatalog catalog, SortMethod method) =>
        method switch
        {
            SortMethod.Name => (a, b) =>
            {
                int c = string.Compare(catalog.GetDisplayName(a.Value), catalog.GetDisplayName(b.Value), StringComparison.CurrentCultureIgnoreCase);
                return c != 0 ? c : CompareFallback(a, b);
            },
            SortMethod.RawValue => CompareFallback,
            SortMethod.CountDescending => (a, b) =>
            {
                int c = b.Count.CompareTo(a.Count);
                return c != 0 ? c : CompareFallback(a, b);
            },
            SortMethod.CountAscending => (a, b) =>
            {
                int c = a.Count.CompareTo(b.Count);
                return c != 0 ? c : CompareFallback(a, b);
            },
            _ => (a, b) =>
            {
                int c = string.Compare(catalog.GetCategory(a.Value), catalog.GetCategory(b.Value), StringComparison.CurrentCultureIgnoreCase);
                if (c != 0)
                {
                    return c;
                }

                c = catalog.GetDisplayOrder(a.Value).CompareTo(catalog.GetDisplayOrder(b.Value));
                if (c != 0)
                {
                    return c;
                }

                c = string.Compare(catalog.GetDisplayName(a.Value), catalog.GetDisplayName(b.Value), StringComparison.CurrentCultureIgnoreCase);
                return c != 0 ? c : CompareFallback(a, b);
            },
        };

    /// <summary>同名/同类时的稳定兜底：先按方块索引，再按 data，再按数量降序。</summary>
    private static int CompareFallback(SlotSnapshot a, SlotSnapshot b)
    {
        int c = ItemValue.Contents(a.Value).CompareTo(ItemValue.Contents(b.Value));
        if (c != 0)
        {
            return c;
        }

        c = ItemValue.Data(a.Value).CompareTo(ItemValue.Data(b.Value));
        return c != 0 ? c : b.Count.CompareTo(a.Count);
    }

    private static IReadOnlyList<SlotAssignment> Diff(IReadOnlyList<SlotSnapshot> before, SlotSnapshot[] after)
    {
        var changes = new List<SlotAssignment>();
        for (int i = 0; i < after.Length; i++)
        {
            SlotSnapshot from = before[i];
            SlotSnapshot to = after[i];

            bool same = from.IsEmpty && to.IsEmpty
                || !from.IsEmpty && !to.IsEmpty && ItemValue.SameItem(from.Value, to.Value) && from.Count == to.Count;

            if (!same)
            {
                changes.Add(new SlotAssignment(i, to.IsEmpty ? 0 : to.Value, to.Count));
            }
        }

        return changes;
    }
}
