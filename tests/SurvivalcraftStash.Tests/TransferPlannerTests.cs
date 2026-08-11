using Stash.Shared.Inventory;
using Stash.Shared.Items;
using Stash.Shared.Transfer;
using Xunit;

namespace SurvivalcraftStash.Tests;

public class TransferPlannerTests
{
    private static readonly FakeCatalog Catalog = new FakeCatalog()
        .Add(1, stack: 40, name: "Dirt")
        .Add(2, stack: 40, name: "Stone")
        .Add(3, stack: 1, name: "Axe");

    private static InventorySnapshot View(IEnumerable<SlotSnapshot> slots, int firstSlotIndex = 0)
    {
        var list = slots.ToList();
        var indexes = Enumerable.Range(firstSlotIndex, list.Count).ToList();
        return new InventorySnapshot(list, indexes, Catalog);
    }

    private static List<SlotSnapshot> Apply(IReadOnlyList<SlotSnapshot> before, IReadOnlyList<SlotAssignment> plan, int firstSlotIndex = 0)
    {
        var after = before.ToList();
        foreach (SlotAssignment a in plan)
        {
            after[a.SlotIndex - firstSlotIndex] = a.Count > 0 ? new SlotSnapshot(a.Value, a.Count) : SlotSnapshot.Empty;
        }

        return after;
    }

    private static int Total(IEnumerable<SlotSnapshot> slots, int contents) =>
        slots.Where(s => !s.IsEmpty && ItemValue.Contents(s.Value) == contents).Sum(s => s.Count);

    [Fact]
    public void 智能存入只送目标已有的物品()
    {
        var src = new List<SlotSnapshot> { new(1, 10), new(3, 1) };   // 泥土 + 斧头
        var dst = new List<SlotSnapshot> { new(1, 5), SlotSnapshot.Empty };  // 目标只有泥土

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.Smart);
        var after = Apply(dst, plan.TargetAssignments, 100);
        var left = Apply(src, plan.SourceAssignments);

        Assert.Equal(15, Total(after, 1));
        Assert.Equal(0, Total(after, 3));      // 斧头没被送走
        Assert.Equal(1, Total(left, 3));
        Assert.Equal(10, plan.MovedCount);
    }

    [Fact]
    public void 全部存入会送走所有能塞下的东西()
    {
        var src = new List<SlotSnapshot> { new(1, 10), new(3, 1) };
        var dst = new List<SlotSnapshot> { SlotSnapshot.Empty, SlotSnapshot.Empty };

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.All);
        var after = Apply(dst, plan.TargetAssignments, 100);

        Assert.Equal(10, Total(after, 1));
        Assert.Equal(1, Total(after, 3));
        Assert.Equal(11, plan.MovedCount);
    }

    [Fact]
    public void 目标装不下时只搬能装下的部分()
    {
        var src = new List<SlotSnapshot> { new(1, 30) };
        var dst = new List<SlotSnapshot> { new(1, 35) };   // 只剩 5 格空间

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.Smart);
        var after = Apply(dst, plan.TargetAssignments, 100);
        var left = Apply(src, plan.SourceAssignments);

        Assert.Equal(40, Total(after, 1));
        Assert.Equal(25, Total(left, 1));
        Assert.Equal(5, plan.MovedCount);
    }

    [Fact]
    public void 锁定的槽位不会被一键存入带走()
    {
        var src = new List<SlotSnapshot> { new(1, 10), new(1, 10) };
        var dst = new List<SlotSnapshot> { new(1, 1) };
        var locked = new HashSet<int> { 0 };

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.Smart, locked);
        var left = Apply(src, plan.SourceAssignments);

        Assert.Equal(10, left[0].Count);
        Assert.True(left[1].IsEmpty);
        Assert.Equal(10, plan.MovedCount);
    }

    [Fact]
    public void 空的记忆槽位只给它记住的物品留着()
    {
        var src = new List<SlotSnapshot> { new(1, 10) };
        var dst = new List<SlotSnapshot> { SlotSnapshot.Empty, SlotSnapshot.Empty };
        // 槽位 100 记住石头，不该被泥土占用；泥土只能进 101
        var memory = new Dictionary<int, int> { [100] = ItemValue.Make(2) };

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.All, null, memory);
        var after = Apply(dst, plan.TargetAssignments, 100);

        Assert.True(after[0].IsEmpty);
        Assert.Equal(10, after[1].Count);
    }

    [Fact]
    public void 记忆槽位让智能存入认下它记住的物品()
    {
        var src = new List<SlotSnapshot> { new(2, 7) };
        var dst = new List<SlotSnapshot> { SlotSnapshot.Empty };
        var memory = new Dictionary<int, int> { [100] = ItemValue.Make(2) };

        // 目标里一件石头都没有，但槽位记住了石头 → 智能存入应当接收
        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.Smart, null, memory);

        Assert.Equal(7, plan.MovedCount);
    }

    [Fact]
    public void 没有可搬的东西时不产生任何指令()
    {
        var src = new List<SlotSnapshot> { new(3, 1) };
        var dst = new List<SlotSnapshot> { new(1, 5) };

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.Smart);

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.SourceAssignments);
        Assert.Empty(plan.TargetAssignments);
    }

    [Fact]
    public void 搬运前后物品总数守恒()
    {
        var src = new List<SlotSnapshot> { new(1, 33), new(2, 12), new(3, 1) };
        var dst = new List<SlotSnapshot> { new(1, 20), SlotSnapshot.Empty, SlotSnapshot.Empty };

        TransferPlan plan = TransferPlanner.Plan(View(src), View(dst, 100), TransferMode.All);
        var afterSrc = Apply(src, plan.SourceAssignments);
        var afterDst = Apply(dst, plan.TargetAssignments, 100);

        foreach (int contents in new[] { 1, 2, 3 })
        {
            int before = Total(src, contents) + Total(dst, contents);
            int after = Total(afterSrc, contents) + Total(afterDst, contents);
            Assert.Equal(before, after);
        }
    }
}
