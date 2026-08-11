using Engine;
using Game.NetWork;
using Stash.Game;
using Stash.Shared.Inventory;

namespace Game;

/// <summary>
/// 联机版的槽位变更包：客户端把**算好的目标布局差分**发给服务端，服务端校验后落地。
///
/// 为什么不让客户端直接改：`ComponentInventoryPackage` 那套原版流程里，
/// 服务端才是权威方，客户端本地改槽位不会被承认，而且服务端对越权操作会直接踢人。
///
/// 包号 219：原版占用 0-40 / 56-59 / 250-253，SCTM 用了 41 和 217，这里挑一个离得远的。
/// 与其它 Mod 撞号时 PackageManager.RegisterPackage 会抛异常，我们捕获后降级为"只在本地生效"。
/// </summary>
public sealed class StashOpPackage : IPackage
{
    public const byte PackageId = 219;

    /// <summary>单个包最多携带的赋值条数，防止被构造成超大包打服务端。</summary>
    public const int MaxAssignments = 512;

    /// <summary>一个包最多涉及多少个容器。终端的一键存入会横跨整片网络，所以要留够。</summary>
    public const int MaxParts = StashNetworkScanner.MaxContainers + 2;

    public byte ID => PackageId;

    public Client To { get; set; } = null!;

    public Client Except { get; set; } = null!;

    public Client From { get; set; } = null!;

    public ClientState MinNeedState => ClientState.Playing;

    /// <summary>库存 Id → 该库存的赋值列表。</summary>
    public List<(int InventoryId, List<SlotAssignment> Assignments)> Parts { get; private set; } = new();

    public StashOpPackage()
    {
    }

    public StashOpPackage(StashPlan plan)
    {
        foreach ((IInventory inventory, IReadOnlyList<SlotAssignment> assignments) in plan.Parts)
        {
            Parts.Add((inventory.Id, new List<SlotAssignment>(assignments)));
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Parts.Count);
        foreach ((int inventoryId, List<SlotAssignment> assignments) in Parts)
        {
            writer.Write(inventoryId);
            writer.Write(assignments.Count);
            foreach (SlotAssignment assignment in assignments)
            {
                writer.Write(assignment.SlotIndex);
                writer.Write(assignment.Value);
                writer.Write(assignment.Count);
            }
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Parts = new List<(int, List<SlotAssignment>)>();
        int partCount = reader.ReadInt32();
        if (partCount is < 0 or > MaxParts)
        {
            throw new InvalidDataException($"Stash 包的容器数量非法：{partCount}");
        }

        for (int i = 0; i < partCount; i++)
        {
            int inventoryId = reader.ReadInt32();
            int count = reader.ReadInt32();
            if (count is < 0 or > MaxAssignments)
            {
                throw new InvalidDataException($"Stash 包的赋值数量非法：{count}");
            }

            var assignments = new List<SlotAssignment>(count);
            for (int j = 0; j < count; j++)
            {
                int slotIndex = reader.ReadInt32();
                int value = reader.ReadInt32();
                int itemCount = reader.ReadInt32();
                assignments.Add(new SlotAssignment(slotIndex, value, itemCount));
            }

            Parts.Add((inventoryId, assignments));
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        if (!isServer)
        {
            // 服务端落地后会通过原版的 InventorySync 把结果推回来，客户端不需要自己动。
            return;
        }

        SubsystemInventories inventories = projectNet.FindSubsystem<SubsystemInventories>();
        if (inventories == null)
        {
            return;
        }

        // 整包一起校验：跨两个容器的搬运只有合起来看才守恒。
        if (!StashServerGuard.Validate(inventories, Parts, From, out var resolved, out string reason))
        {
            Log.Warning($"[Stash] 拒绝一次整理请求：{reason}");
            return;
        }

        foreach (StashServerGuard.ResolvedPart part in resolved)
        {
            GameInventory.Apply(part.Inventory, part.Assignments);
        }
    }
}
