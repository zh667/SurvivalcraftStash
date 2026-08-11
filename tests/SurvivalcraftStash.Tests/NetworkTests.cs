using Stash.Shared.Inventory;
using Stash.Shared.Items;
using Stash.Shared.Network;
using Stash.Shared.Sorting;
using Xunit;

namespace SurvivalcraftStash.Tests;

public class NetworkSearchTests
{
    [Theory]
    [InlineData("", "圆石", "Terrain", true)]
    [InlineData("圆", "圆石", "Terrain", true)]
    [InlineData("木", "圆石", "Terrain", false)]
    [InlineData("#terrain", "圆石", "Terrain", true)]
    [InlineData("#items", "圆石", "Terrain", false)]
    [InlineData("圆 #terrain", "圆石", "Terrain", true)]
    [InlineData("圆 #items", "圆石", "Terrain", false)]
    [InlineData("木|圆", "圆石", "Terrain", true)]
    public void 搜索语法(string query, string name, string category, bool expected) =>
        Assert.Equal(expected, NetworkSearch.Parse(query).Matches(name, category));

    [Fact]
    public void 大小写不敏感且是包含匹配() =>
        Assert.True(NetworkSearch.Parse("STO").Matches("Cobblestone", "Terrain"));
}

public class NetworkAggregateTests
{
    private static readonly FakeCatalog Catalog = new FakeCatalog()
        .Add(1, 40, "Dirt", "Terrain", 10)
        .Add(2, 40, "Stone", "Terrain", 20)
        .Add(3, 1, "Axe", "Tools", 5);

    [Fact]
    public void 跨容器合并同种物品()
    {
        var containers = new[]
        {
            new NetworkContainer(0, new List<SlotSnapshot> { new(1, 40), new(2, 5) }),
            new NetworkContainer(1, new List<SlotSnapshot> { new(1, 13), SlotSnapshot.Empty }),
        };

        List<NetworkEntry> entries = NetworkAggregate.Build(containers, Catalog, SortMethod.RawValue);

        Assert.Equal(2, entries.Count);
        Assert.Equal(53, entries.Single(e => ItemValue.Contents(e.Value) == 1).Count);
        Assert.Equal(5, entries.Single(e => ItemValue.Contents(e.Value) == 2).Count);
    }

    [Fact]
    public void 按数量降序排列()
    {
        var containers = new[]
        {
            new NetworkContainer(0, new List<SlotSnapshot> { new(1, 3), new(2, 99), new(3, 1) }),
        };

        List<NetworkEntry> entries = NetworkAggregate.Build(containers, Catalog, SortMethod.CountDescending);

        Assert.Equal(new long[] { 99, 3, 1 }, entries.Select(e => e.Count).ToArray());
    }
}

public class NetworkTransferTests
{
    private static readonly FakeCatalog Catalog = new FakeCatalog()
        .Add(1, 40, "Dirt")
        .Add(2, 40, "Stone");

    private static InventorySnapshot View(IEnumerable<SlotSnapshot> slots, int first = 0)
    {
        var list = slots.ToList();
        return new InventorySnapshot(list, Enumerable.Range(first, list.Count).ToList(), Catalog);
    }

    private static List<SlotSnapshot> Apply(IReadOnlyList<SlotSnapshot> before, IReadOnlyList<SlotAssignment> plan, int first = 0)
    {
        var after = before.ToList();
        foreach (SlotAssignment a in plan)
        {
            after[a.SlotIndex - first] = a.Count > 0 ? new SlotSnapshot(a.Value, a.Count) : SlotSnapshot.Empty;
        }

        return after;
    }

    private static int Total(IEnumerable<SlotSnapshot> slots, int contents) =>
        slots.Where(s => !s.IsEmpty && ItemValue.Contents(s.Value) == contents).Sum(s => s.Count);

    [Fact]
    public void 取出会跨容器凑数()
    {
        var c0 = new List<SlotSnapshot> { new(1, 12) };
        var c1 = new List<SlotSnapshot> { new(1, 30) };
        var player = new List<SlotSnapshot> { SlotSnapshot.Empty, SlotSnapshot.Empty };

        NetworkTransferPlan plan = NetworkTransfer.PlanExtract(
            new[] { View(c0), View(c1) }, View(player, 100), ItemValue.Make(1), 40);

        Assert.Equal(40, plan.MovedCount);
        Assert.Equal(40, Total(Apply(player, plan.PlayerAssignments, 100), 1));
        Assert.Equal(0, Total(Apply(c0, plan.ContainerAssignments[0]), 1));
        Assert.Equal(2, Total(Apply(c1, plan.ContainerAssignments[1]), 1));
    }

    [Fact]
    public void 玩家装不下就少取()
    {
        var c0 = new List<SlotSnapshot> { new(1, 200) };
        var player = new List<SlotSnapshot> { new(1, 38) };   // 只剩 2 格空间

        NetworkTransferPlan plan = NetworkTransfer.PlanExtract(
            new[] { View(c0) }, View(player, 100), ItemValue.Make(1), 40);

        Assert.Equal(2, plan.MovedCount);
        Assert.Equal(40, Total(Apply(player, plan.PlayerAssignments, 100), 1));
        Assert.Equal(198, Total(Apply(c0, plan.ContainerAssignments[0]), 1));
    }

    [Fact]
    public void 存入会填满第一个容器再用下一个()
    {
        var c0 = new List<SlotSnapshot> { new(1, 35) };
        var c1 = new List<SlotSnapshot> { SlotSnapshot.Empty };
        var player = new List<SlotSnapshot> { new(1, 40) };

        NetworkTransferPlan plan = NetworkTransfer.PlanDeposit(new[] { View(c0), View(c1) }, View(player, 100));

        Assert.Equal(40, plan.MovedCount);
        Assert.Equal(40, Total(Apply(c0, plan.ContainerAssignments[0]), 1));
        Assert.Equal(35, Total(Apply(c1, plan.ContainerAssignments[1]), 1));
        Assert.DoesNotContain(Apply(player, plan.PlayerAssignments, 100), s => !s.IsEmpty);
    }

    [Fact]
    public void 存入不动锁定的槽位()
    {
        var c0 = new List<SlotSnapshot> { SlotSnapshot.Empty, SlotSnapshot.Empty };
        var player = new List<SlotSnapshot> { new(1, 10), new(2, 10) };
        var locked = new HashSet<int> { 100 };

        NetworkTransferPlan plan = NetworkTransfer.PlanDeposit(new[] { View(c0) }, View(player, 100), locked);
        var left = Apply(player, plan.PlayerAssignments, 100);

        Assert.Equal(10, Total(left, 1));
        Assert.Equal(0, Total(left, 2));
        Assert.Equal(10, plan.MovedCount);
    }

    [Fact]
    public void 搬运前后总数守恒()
    {
        var c0 = new List<SlotSnapshot> { new(1, 17), SlotSnapshot.Empty };
        var player = new List<SlotSnapshot> { new(1, 5), new(2, 7) };

        NetworkTransferPlan plan = NetworkTransfer.PlanDeposit(new[] { View(c0) }, View(player, 100));
        var afterContainer = Apply(c0, plan.ContainerAssignments.TryGetValue(0, out var a) ? a : Array.Empty<SlotAssignment>());
        var afterPlayer = Apply(player, plan.PlayerAssignments, 100);

        foreach (int contents in new[] { 1, 2 })
        {
            Assert.Equal(
                Total(c0, contents) + Total(player, contents),
                Total(afterContainer, contents) + Total(afterPlayer, contents));
        }
    }
}
