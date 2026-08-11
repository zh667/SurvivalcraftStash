using Stash.Shared.Items;

namespace Stash.Shared.Inventory;

/// <summary>
/// 一个"容器视图"的只读快照：槽位内容 + 每槽容量。
///
/// 为什么要 SlotIndexes：一个界面里的一块网格往往只覆盖某个库存的一段槽位
/// （例如箱子界面里玩家那块网格是 10~25，快捷栏 0~9 不在里面）。
/// 整理和存取都只应该动这一段，所以视图带着"我管哪些槽"。
/// </summary>
public sealed class InventorySnapshot
{
    private readonly SlotSnapshot[] m_slots;
    private readonly int[] m_slotIndexes;
    private readonly IItemCatalog m_catalog;

    public InventorySnapshot(IReadOnlyList<SlotSnapshot> slots, IReadOnlyList<int> slotIndexes, IItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(slotIndexes);
        ArgumentNullException.ThrowIfNull(catalog);

        if (slots.Count != slotIndexes.Count)
        {
            throw new ArgumentException("槽位内容与槽位下标数量不一致。", nameof(slotIndexes));
        }

        m_slots = slots.ToArray();
        m_slotIndexes = slotIndexes.ToArray();
        m_catalog = catalog;
    }

    public int Count => m_slots.Length;

    public IReadOnlyList<SlotSnapshot> Slots => m_slots;

    /// <summary>视图内第 i 个位置对应的真实槽位下标。</summary>
    public int SlotIndexAt(int viewIndex) => m_slotIndexes[viewIndex];

    public IItemCatalog Catalog => m_catalog;

    public SlotSnapshot this[int viewIndex] => m_slots[viewIndex];

    public int CapacityOf(int value) => Math.Max(1, m_catalog.GetMaxStacking(value));

    /// <summary>视图里是否已经有这种物品（"智能存入"的判据之一）。</summary>
    public bool Contains(int value)
    {
        for (int i = 0; i < m_slots.Length; i++)
        {
            if (!m_slots[i].IsEmpty && ItemValue.SameItem(m_slots[i].Value, value))
            {
                return true;
            }
        }

        return false;
    }

    public SlotSnapshot[] CopySlots() => (SlotSnapshot[])m_slots.Clone();

    /// <summary>把工作数组与原始快照比对，产出"真实槽位下标"的赋值指令。</summary>
    public IReadOnlyList<SlotAssignment> DiffTo(IReadOnlyList<SlotSnapshot> after)
    {
        var changes = new List<SlotAssignment>();
        for (int i = 0; i < m_slots.Length; i++)
        {
            SlotSnapshot from = m_slots[i];
            SlotSnapshot to = after[i];

            bool same = from.IsEmpty && to.IsEmpty
                || !from.IsEmpty && !to.IsEmpty && ItemValue.SameItem(from.Value, to.Value) && from.Count == to.Count;

            if (!same)
            {
                changes.Add(new SlotAssignment(m_slotIndexes[i], to.IsEmpty ? 0 : to.Value, to.Count));
            }
        }

        return changes;
    }
}
