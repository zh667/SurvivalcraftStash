using Stash.Shared.Inventory;
using Stash.Shared.Items;

namespace Stash.Shared.Network;

/// <summary>网络里一次搬运的结果：每个容器要改哪些槽位，以及玩家那边要改哪些。</summary>
public sealed record NetworkTransferPlan(
    IReadOnlyDictionary<int, IReadOnlyList<SlotAssignment>> ContainerAssignments,
    IReadOnlyList<SlotAssignment> PlayerAssignments,
    int MovedCount)
{
    public bool IsEmpty => MovedCount == 0;

    public static readonly NetworkTransferPlan Empty = new(
        new Dictionary<int, IReadOnlyList<SlotAssignment>>(),
        Array.Empty<SlotAssignment>(),
        0);
}

public static class NetworkTransfer
{
    /// <summary>
    /// 从网络里取出某种物品到玩家背包，最多取 <paramref name="wanted"/> 个。
    /// 从容器里按顺序凑数——先掏空零散的，再动整栈，这样网络里不容易留下一堆碎栈。
    /// </summary>
    public static NetworkTransferPlan PlanExtract(
        IReadOnlyList<InventorySnapshot> containers,
        InventorySnapshot player,
        int value,
        int wanted)
    {
        if (wanted <= 0)
        {
            return NetworkTransferPlan.Empty;
        }

        // 先算玩家那边能收多少，免得从容器掏出来却装不下。
        SlotSnapshot[] playerSlots = player.CopySlots();
        int capacity = player.CapacityOf(value);
        int room = 0;
        foreach (SlotSnapshot slot in playerSlots)
        {
            if (slot.IsEmpty)
            {
                room += capacity;
            }
            else if (ItemValue.SameItem(slot.Value, value))
            {
                room += Math.Max(0, capacity - slot.Count);
            }
        }

        int target = Math.Min(wanted, room);
        if (target <= 0)
        {
            return NetworkTransferPlan.Empty;
        }

        var containerAssignments = new Dictionary<int, IReadOnlyList<SlotAssignment>>();
        int collected = 0;

        for (int i = 0; i < containers.Count && collected < target; i++)
        {
            InventorySnapshot container = containers[i];
            SlotSnapshot[] slots = container.CopySlots();
            bool touched = false;

            for (int s = 0; s < slots.Length && collected < target; s++)
            {
                if (slots[s].IsEmpty || !ItemValue.SameItem(slots[s].Value, value))
                {
                    continue;
                }

                int take = Math.Min(slots[s].Count, target - collected);
                slots[s] = slots[s].Count - take > 0
                    ? slots[s] with { Count = slots[s].Count - take }
                    : SlotSnapshot.Empty;

                collected += take;
                touched = true;
            }

            if (touched)
            {
                containerAssignments[i] = container.DiffTo(slots);
            }
        }

        if (collected == 0)
        {
            return NetworkTransferPlan.Empty;
        }

        // 再把取到的东西塞进玩家背包：先补已有的栈，再占空格。
        int remaining = collected;
        for (int s = 0; s < playerSlots.Length && remaining > 0; s++)
        {
            if (playerSlots[s].IsEmpty || !ItemValue.SameItem(playerSlots[s].Value, value))
            {
                continue;
            }

            int add = Math.Min(capacity - playerSlots[s].Count, remaining);
            if (add <= 0)
            {
                continue;
            }

            playerSlots[s] = playerSlots[s] with { Count = playerSlots[s].Count + add };
            remaining -= add;
        }

        for (int s = 0; s < playerSlots.Length && remaining > 0; s++)
        {
            if (!playerSlots[s].IsEmpty)
            {
                continue;
            }

            int add = Math.Min(capacity, remaining);
            playerSlots[s] = new SlotSnapshot(value, add);
            remaining -= add;
        }

        return new NetworkTransferPlan(containerAssignments, player.DiffTo(playerSlots), collected - remaining);
    }

    /// <summary>把玩家背包里的东西存进网络。锁定槽位不动。</summary>
    public static NetworkTransferPlan PlanDeposit(
        IReadOnlyList<InventorySnapshot> containers,
        InventorySnapshot player,
        IReadOnlySet<int>? lockedPlayerSlots = null)
    {
        SlotSnapshot[] playerSlots = player.CopySlots();
        var working = new SlotSnapshot[containers.Count][];
        for (int i = 0; i < containers.Count; i++)
        {
            working[i] = containers[i].CopySlots();
        }

        int moved = 0;

        for (int s = 0; s < playerSlots.Length; s++)
        {
            if (playerSlots[s].IsEmpty)
            {
                continue;
            }

            if (lockedPlayerSlots is not null && lockedPlayerSlots.Contains(player.SlotIndexAt(s)))
            {
                continue;
            }

            int value = playerSlots[s].Value;
            int remaining = playerSlots[s].Count;

            for (int c = 0; c < containers.Count && remaining > 0; c++)
            {
                remaining -= Insert(working[c], containers[c], value, remaining);
            }

            int placed = playerSlots[s].Count - remaining;
            if (placed > 0)
            {
                moved += placed;
                playerSlots[s] = remaining > 0 ? playerSlots[s] with { Count = remaining } : SlotSnapshot.Empty;
            }
        }

        if (moved == 0)
        {
            return NetworkTransferPlan.Empty;
        }

        var containerAssignments = new Dictionary<int, IReadOnlyList<SlotAssignment>>();
        for (int i = 0; i < containers.Count; i++)
        {
            IReadOnlyList<SlotAssignment> diff = containers[i].DiffTo(working[i]);
            if (diff.Count > 0)
            {
                containerAssignments[i] = diff;
            }
        }

        return new NetworkTransferPlan(containerAssignments, player.DiffTo(playerSlots), moved);
    }

    private static int Insert(SlotSnapshot[] slots, InventorySnapshot container, int value, int amount)
    {
        int capacity = container.CapacityOf(value);
        if (capacity <= 0)
        {
            return 0;
        }

        int placed = 0;

        for (int i = 0; i < slots.Length && placed < amount; i++)
        {
            if (slots[i].IsEmpty || !ItemValue.SameItem(slots[i].Value, value))
            {
                continue;
            }

            int add = Math.Min(capacity - slots[i].Count, amount - placed);
            if (add <= 0)
            {
                continue;
            }

            slots[i] = slots[i] with { Count = slots[i].Count + add };
            placed += add;
        }

        for (int i = 0; i < slots.Length && placed < amount; i++)
        {
            if (!slots[i].IsEmpty)
            {
                continue;
            }

            int add = Math.Min(capacity, amount - placed);
            slots[i] = new SlotSnapshot(value, add);
            placed += add;
        }

        return placed;
    }
}
