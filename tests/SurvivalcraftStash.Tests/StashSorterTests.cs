using Stash.Shared.Inventory;
using Stash.Shared.Items;
using Stash.Shared.Sorting;
using Xunit;

namespace SurvivalcraftStash.Tests;

public class StashSorterTests
{
    private static readonly FakeCatalog Catalog = new FakeCatalog()
        .Add(1, stack: 40, name: "Dirt", category: "Terrain", order: 10)
        .Add(2, stack: 40, name: "Stone", category: "Terrain", order: 20)
        .Add(3, stack: 1, name: "Axe", category: "Tools", order: 5);

    private static List<SlotSnapshot> Apply(IReadOnlyList<SlotSnapshot> before, IReadOnlyList<SlotAssignment> plan)
    {
        var after = before.ToList();
        foreach (SlotAssignment a in plan)
        {
            after[a.SlotIndex] = a.Count > 0 ? new SlotSnapshot(a.Value, a.Count) : SlotSnapshot.Empty;
        }

        return after;
    }

    private static int TotalOf(IEnumerable<SlotSnapshot> slots, int contents) =>
        slots.Where(s => !s.IsEmpty && ItemValue.Contents(s.Value) == contents).Sum(s => s.Count);

    [Fact]
    public void 合并零散同种物品成整栈()
    {
        var before = new List<SlotSnapshot>
        {
            new(1, 10), SlotSnapshot.Empty, new(1, 25), new(1, 30), SlotSnapshot.Empty,
        };

        var after = Apply(before, StashSorter.Plan(before, Catalog, SortMethod.RawValue));

        Assert.Equal(65, TotalOf(after, 1));
        Assert.Equal(new[] { 40, 25 }, after.Where(s => !s.IsEmpty).Select(s => s.Count).ToArray());
    }

    [Fact]
    public void 按类别与创造栏顺序排列()
    {
        var before = new List<SlotSnapshot> { new(2, 5), new(3, 1), new(1, 5), SlotSnapshot.Empty };

        var after = Apply(before, StashSorter.Plan(before, Catalog, SortMethod.CategoryThenDisplayOrder));

        // Terrain 在 Tools 之前；Terrain 内部按 DisplayOrder：Dirt(10) < Stone(20)
        Assert.Equal(1, ItemValue.Contents(after[0].Value));
        Assert.Equal(2, ItemValue.Contents(after[1].Value));
        Assert.Equal(3, ItemValue.Contents(after[2].Value));
    }

    [Fact]
    public void 锁定的槽位原样不动()
    {
        var before = new List<SlotSnapshot> { new(2, 5), new(3, 1), new(1, 5), SlotSnapshot.Empty };
        var locked = new HashSet<int> { 1 };

        var plan = StashSorter.Plan(before, Catalog, SortMethod.CategoryThenDisplayOrder, locked);
        var after = Apply(before, plan);

        Assert.DoesNotContain(plan, a => a.SlotIndex == 1);
        Assert.Equal(before[1], after[1]);
        Assert.Equal(5, TotalOf(after, 1));
        Assert.Equal(5, TotalOf(after, 2));
    }

    [Fact]
    public void 记忆槽位优先吸收同种物品且位置固定()
    {
        // 槽位 3 记住了 Stone，当前空着；散落的 Stone 应该被吸进来。
        var before = new List<SlotSnapshot> { new(2, 5), new(1, 5), new(2, 7), SlotSnapshot.Empty };
        var memory = new Dictionary<int, int> { [3] = ItemValue.Make(2) };

        var after = Apply(before, StashSorter.Plan(before, Catalog, SortMethod.RawValue, null, memory));

        Assert.Equal(2, ItemValue.Contents(after[3].Value));
        Assert.Equal(12, after[3].Count);
        Assert.Equal(5, TotalOf(after, 1));
    }

    [Fact]
    public void 记忆槽位里放着别的东西时按锁定处理()
    {
        var before = new List<SlotSnapshot> { new(1, 5), SlotSnapshot.Empty, new(3, 1) };
        var memory = new Dictionary<int, int> { [2] = ItemValue.Make(2) };

        var plan = StashSorter.Plan(before, Catalog, SortMethod.RawValue, null, memory);
        var after = Apply(before, plan);

        Assert.DoesNotContain(plan, a => a.SlotIndex == 2);
        Assert.Equal(3, ItemValue.Contents(after[2].Value));
    }

    [Fact]
    public void 已经整齐时不产生任何指令()
    {
        var before = new List<SlotSnapshot> { new(1, 40), new(1, 5), SlotSnapshot.Empty };

        var plan = StashSorter.Plan(before, Catalog, SortMethod.RawValue);

        Assert.Empty(plan);
    }

    [Fact]
    public void 不可堆叠物品各占一格()
    {
        var before = new List<SlotSnapshot> { new(3, 1), SlotSnapshot.Empty, new(3, 1), new(3, 1) };

        var after = Apply(before, StashSorter.Plan(before, Catalog, SortMethod.RawValue));

        Assert.Equal(3, after.Count(s => !s.IsEmpty));
        Assert.All(after.Where(s => !s.IsEmpty), s => Assert.Equal(1, s.Count));
    }

    [Fact]
    public void data位不同的物品不会被合并()
    {
        int plain = ItemValue.Make(1);
        int damaged = ItemValue.Make(1, data: 7);
        var before = new List<SlotSnapshot> { new(plain, 3), new(damaged, 4), SlotSnapshot.Empty };

        var after = Apply(before, StashSorter.Plan(before, Catalog, SortMethod.RawValue));

        Assert.Equal(2, after.Count(s => !s.IsEmpty));
        Assert.Contains(after, s => s.Value == plain && s.Count == 3);
        Assert.Contains(after, s => s.Value == damaged && s.Count == 4);
    }

    [Fact]
    public void 光照位不影响归类()
    {
        int lit = ItemValue.Make(1, data: 0, light: 9);
        var before = new List<SlotSnapshot> { new(ItemValue.Make(1), 3), new(lit, 4), SlotSnapshot.Empty };

        var after = Apply(before, StashSorter.Plan(before, Catalog, SortMethod.RawValue));

        Assert.Single(after, s => !s.IsEmpty);
        Assert.Equal(7, TotalOf(after, 1));
    }
}

public class ItemValueTests
{
    [Theory]
    [InlineData(45, 0)]
    [InlineData(45, 3)]
    [InlineData(1023, ItemValue.MaxData)]
    public void 位编码可往返(int contents, int data)
    {
        int value = ItemValue.Make(contents, data);

        Assert.Equal(contents, ItemValue.Contents(value));
        Assert.Equal(data, ItemValue.Data(value));
    }

    [Fact]
    public void 替换data不动contents()
    {
        int value = ItemValue.Make(45, 3);
        int replaced = ItemValue.ReplaceData(value, 12345);

        Assert.Equal(45, ItemValue.Contents(replaced));
        Assert.Equal(12345, ItemValue.Data(replaced));
    }

    [Fact]
    public void 同种物品判定忽略光照位()
    {
        Assert.True(ItemValue.SameItem(ItemValue.Make(45, 3, light: 0), ItemValue.Make(45, 3, light: 15)));
        Assert.False(ItemValue.SameItem(ItemValue.Make(45, 3), ItemValue.Make(45, 4)));
    }
}
