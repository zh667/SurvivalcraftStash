using Game;
using Stash.Shared.Inventory;
using Stash.Shared.Items;
using Stash.Shared.Sorting;
using Stash.Shared.Storage;
using Stash.Shared.Transfer;

namespace Stash.Game;

/// <summary>
/// 整理层的四个动作：整理、一键存入、一键取出、撤销。
///
/// 全部走"先算目标布局 → 再算最小差分 → 交给平台落地"这一条路径，
/// 这样联机版只需要把差分发给服务端，而不是让客户端逐格搬（那样既慢又会被反作弊拦）。
/// </summary>
public static class StashOperations
{
    private static readonly BlocksManagerCatalog s_catalog = new();

    /// <summary>上一次操作的逆向计划，用于撤销。只保留一层。</summary>
    private static StashPlan? s_undo;

    public static bool CanUndo => s_undo is { IsEmpty: false };

    public static IItemCatalog Catalog => s_catalog;

    public static int Sort(PanelContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        PlayerStashData player = StashStore.ForCurrentPlayer();
        InventorySnapshot snapshot = GameInventory.Snapshot(container.Inventory, container.SlotIndexes, s_catalog);

        IReadOnlySet<int>? locked = null;
        IReadOnlyDictionary<int, int>? memory = null;
        if (container.IsPlayerInventory)
        {
            locked = player.LockedSlotSet();
            memory = player.MemorySlotMap();
        }

        IReadOnlyList<SlotAssignment> assignments = StashSorter.Plan(
            snapshot.Slots,
            s_catalog,
            player.SortMethod,
            locked is null ? null : MapToViewIndexes(locked, container),
            memory is null ? null : MapMemoryToViewIndexes(memory, container));

        if (assignments.Count == 0)
        {
            return 0;
        }

        // StashSorter 工作在"视图下标"上，这里翻译回真实槽位下标。
        var translated = new List<SlotAssignment>(assignments.Count);
        foreach (SlotAssignment assignment in assignments)
        {
            translated.Add(assignment with { SlotIndex = container.SlotIndexes[assignment.SlotIndex] });
        }

        var plan = new StashPlan();
        plan.Add(container.Inventory, translated);
        if (!Execute(plan))
        {
            return 0;
        }

        // 只数**最后装着东西**的格子。
        // translated 里同时包含"东西搬进来"和"原地方腾空"两种赋值，
        // 两边都算就会差不多翻一倍（实机反馈"整理好像会翻倍计算"）。
        int filled = 0;
        foreach (SlotAssignment assignment in translated)
        {
            if (assignment.Value != 0 && assignment.Count > 0)
            {
                filled++;
            }
        }

        return filled;
    }

    public static int Deposit(PanelContainer source, PanelContainer target, TransferMode mode)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        PlayerStashData player = StashStore.ForCurrentPlayer();
        InventorySnapshot from = GameInventory.Snapshot(source.Inventory, source.SlotIndexes, s_catalog);
        InventorySnapshot to = GameInventory.Snapshot(target.Inventory, target.SlotIndexes, s_catalog);

        TransferPlan transfer = TransferPlanner.Plan(
            from,
            to,
            mode,
            source.IsPlayerInventory ? player.LockedSlotSet() : null,
            target.IsPlayerInventory ? player.MemorySlotMap() : null);

        if (transfer.IsEmpty)
        {
            return 0;
        }

        var plan = new StashPlan();
        plan.Add(source.Inventory, transfer.SourceAssignments);
        plan.Add(target.Inventory, transfer.TargetAssignments);
        return Execute(plan) ? transfer.MovedCount : 0;
    }

    public static bool Undo()
    {
        if (s_undo is null || s_undo.IsEmpty)
        {
            return false;
        }

        StashPlan plan = s_undo;
        s_undo = null;
        StashPlatform.Current.Execute(plan);
        return true;
    }

    /// <summary>执行计划，并把执行前的状态记下来当作撤销点。</summary>
    private static bool Execute(StashPlan plan)
    {
        if (plan.IsEmpty)
        {
            return false;
        }

        s_undo = BuildInverse(plan);
        StashPlatform.Current.Execute(plan);
        return true;
    }

    private static StashPlan BuildInverse(StashPlan plan)
    {
        var inverse = new StashPlan();
        foreach ((IInventory inventory, IReadOnlyList<SlotAssignment> assignments) in plan.Parts)
        {
            var before = new List<SlotAssignment>(assignments.Count);
            foreach (SlotAssignment assignment in assignments)
            {
                if (!GameInventory.IsSlotValid(inventory, assignment.SlotIndex))
                {
                    continue;
                }

                int count = inventory.GetSlotCount(assignment.SlotIndex);
                before.Add(new SlotAssignment(
                    assignment.SlotIndex,
                    count > 0 ? inventory.GetSlotValue(assignment.SlotIndex) : 0,
                    count));
            }

            inverse.Add(inventory, before);
        }

        return inverse;
    }

    private static IReadOnlySet<int> MapToViewIndexes(IReadOnlySet<int> realSlots, PanelContainer container)
    {
        var mapped = new HashSet<int>();
        for (int i = 0; i < container.SlotIndexes.Count; i++)
        {
            if (realSlots.Contains(container.SlotIndexes[i]))
            {
                mapped.Add(i);
            }
        }

        return mapped;
    }

    private static IReadOnlyDictionary<int, int> MapMemoryToViewIndexes(
        IReadOnlyDictionary<int, int> realMemory,
        PanelContainer container)
    {
        var mapped = new Dictionary<int, int>();
        for (int i = 0; i < container.SlotIndexes.Count; i++)
        {
            if (realMemory.TryGetValue(container.SlotIndexes[i], out int value))
            {
                mapped[i] = value;
            }
        }

        return mapped;
    }
}
