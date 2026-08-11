namespace Stash.Shared.Inventory;

/// <summary>一个槽位的只读快照。Count 为 0 表示空槽（此时 Value 无意义，统一记 0）。</summary>
public readonly record struct SlotSnapshot(int Value, int Count)
{
    public static readonly SlotSnapshot Empty = new(0, 0);

    public bool IsEmpty => Count <= 0;
}

/// <summary>
/// 一次整理产生的"把 SlotIndex 改成 (Value, Count)"指令。
/// 联机版把这一串发给服务端执行；单人版直接落地。
/// </summary>
public readonly record struct SlotAssignment(int SlotIndex, int Value, int Count);
