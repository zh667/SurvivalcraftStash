using Stash.Shared.Crafting;
using Xunit;

namespace SurvivalcraftStash.Tests;

public class RecipeShapeTests
{
    /// <summary>按原版的规范布局造一个 3×3 配方：9 个格子，null = 空。</summary>
    private static string?[] Grid(params string?[] cells)
    {
        var grid = new string?[9];
        for (int i = 0; i < cells.Length && i < 9; i++)
        {
            grid[i] = string.IsNullOrEmpty(cells[i]) ? null : cells[i];
        }

        return grid;
    }

    [Fact]
    public void 一格的配方包围盒是一乘一()
    {
        Assert.Equal((1, 1), RecipeShape.Extent(Grid("a")));
    }

    [Fact]
    public void 横排两格的包围盒是二乘一()
    {
        Assert.Equal((2, 1), RecipeShape.Extent(Grid("a", "a")));
    }

    [Fact]
    public void 一圈围一个的包围盒是三乘三()
    {
        // 我们的升级件就是这个形状："aaa" / "aba" / "aaa"
        Assert.Equal((3, 3), RecipeShape.Extent(Grid("a", "a", "a", "a", "b", "a", "a", "a", "a")));
    }

    [Fact]
    public void 空配方的包围盒是零()
    {
        Assert.Equal((0, 0), RecipeShape.Extent(Grid()));
    }

    [Fact]
    public void 两格宽的配方塞得进自带的二乘二合成格()
    {
        Assert.True(RecipeShape.Fits(Grid("a", "a"), gridSize: 2));
    }

    [Fact]
    public void 三格宽的配方塞不进二乘二只能上工作台()
    {
        string?[] recipe = Grid("a", "a", "a");
        Assert.False(RecipeShape.Fits(recipe, gridSize: 2));
        Assert.True(RecipeShape.Fits(recipe, gridSize: 3));
    }

    [Fact]
    public void 摆放会把配方推到左上角()
    {
        // 只占右下角那一格（下标 8 = 列2 行2）。原版匹配允许任意平移，
        // 所以它在 2×2 里应该落到槽位 0，而不是报"放不下"。
        string?[] recipe = Grid(null, null, null, null, null, null, null, null, "a");

        Assert.True(RecipeShape.TryPlace(recipe, gridSize: 2, out var placements));
        Assert.Single(placements);
        Assert.Equal(0, placements[0].Slot);
    }

    [Fact]
    public void 二乘二的配方在三乘三里按行宽三重新编号()
    {
        // "ab" / "cd" —— 规范下标 0,1,3,4；摆进 3×3 应该还是 0,1,3,4。
        string?[] recipe = Grid("a", "b", null, "c", "d");

        Assert.True(RecipeShape.TryPlace(recipe, gridSize: 3, out var placements));
        Assert.Equal(new[] { 0, 1, 3, 4 }, placements.ConvertAll(p => p.Slot).ToArray());
    }

    [Fact]
    public void 同样的二乘二配方在二乘二里编号变成零一二三()
    {
        string?[] recipe = Grid("a", "b", null, "c", "d");

        Assert.True(RecipeShape.TryPlace(recipe, gridSize: 2, out var placements));
        Assert.Equal(new[] { 0, 1, 2, 3 }, placements.ConvertAll(p => p.Slot).ToArray());
    }
}

public class CraftIngredientTests
{
    [Fact]
    public void 不带data的配料通配所有data()
    {
        CraftIngredient planks = CraftIngredient.Parse("planks");

        Assert.False(planks.IsSpecific);
        Assert.True(planks.Matches("planks", 0));
        Assert.True(planks.Matches("planks", 7));
        Assert.False(planks.Matches("stone", 0));
    }

    [Fact]
    public void 带data的配料只认那一个data()
    {
        CraftIngredient birch = CraftIngredient.Parse("planks:2");

        Assert.True(birch.IsSpecific);
        Assert.True(birch.Matches("planks", 2));
        Assert.False(birch.Matches("planks", 0));
    }

    [Fact]
    public void data写得不是数字就当没写()
    {
        // 别的 Mod 可能写出奇怪的配料串，别为此崩掉——退化成通配即可。
        CraftIngredient weird = CraftIngredient.Parse("planks:abc");

        Assert.False(weird.IsSpecific);
        Assert.True(weird.Matches("planks", 5));
    }
}

public class CraftFillPlannerTests
{
    private static string?[] Grid(params string?[] cells)
    {
        var grid = new string?[9];
        for (int i = 0; i < cells.Length && i < 9; i++)
        {
            grid[i] = string.IsNullOrEmpty(cells[i]) ? null : cells[i];
        }

        return grid;
    }

    [Fact]
    public void 配方太大时明确报太大而不是缺料()
    {
        // 玩家按 E 开的自带合成格只有 2×2，三格宽的配方要提示"配方过大"，
        // 而不是含糊地说缺东西——那样玩家会去找材料，白折腾。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "a", "a"),
            gridSize: 2,
            new[] { new AvailableStack(0, 0, "a", 0, 64) },
            maxSets: 1);

        Assert.Equal(CraftFillFailure.RecipeTooLarge, plan.Failure);
        Assert.Empty(plan.Moves);
    }

    [Fact]
    public void 一份材料够就搬一份()
    {
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "a"),
            gridSize: 2,
            new[] { new AvailableStack(Source: 0, Slot: 12, "a", 0, 5) },
            maxSets: 1);

        Assert.True(plan.Ok);
        Assert.Equal(1, plan.Sets);
        Assert.Equal(2, plan.Moves.Count);
        Assert.All(plan.Moves, m => Assert.Equal(1, m.Count));
        Assert.Equal(new[] { 0, 1 }, plan.Moves.Select(m => m.TargetSlot).ToArray());
    }

    [Fact]
    public void 凑满会按最少的那样配料封顶()
    {
        // a 有 10 个、b 只有 3 个，配方一份各要一个 → 最多 3 份。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "b"),
            gridSize: 2,
            new[]
            {
                new AvailableStack(0, 0, "a", 0, 10),
                new AvailableStack(0, 1, "b", 0, 3),
            },
            maxSets: 64);

        Assert.Equal(3, plan.Sets);
        Assert.Equal(2, plan.Moves.Count);
        Assert.All(plan.Moves, m => Assert.Equal(3, m.Count));
    }

    [Fact]
    public void 同一样配料占两格时每份要两个()
    {
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "a"),
            gridSize: 2,
            new[] { new AvailableStack(0, 0, "a", 0, 5) },
            maxSets: 64);

        // 5 个只够凑 2 份（每份 2 个），剩 1 个不成份。
        Assert.Equal(2, plan.Sets);
        Assert.Equal(2, plan.Moves.Count);
        Assert.All(plan.Moves, m => Assert.Equal(2, m.Count));
    }

    [Fact]
    public void 挑data的配料优先拿走匹配的那份()
    {
        // 配方一格要"任意木板"，一格要"桦木板(data=2)"，而手上只有一块桦木板和一块橡木板。
        // 先来先得的话通配那格会把桦木板抢走 → 明明能合成却报缺料。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("planks", "planks:2"),
            gridSize: 2,
            new[]
            {
                new AvailableStack(0, 0, "planks", 2, 1),   // 桦木板
                new AvailableStack(0, 1, "planks", 0, 1),   // 橡木板
            },
            maxSets: 1);

        Assert.True(plan.Ok);
        Assert.Equal(1, plan.Sets);

        // 挑剔的那格（槽位 1）必须拿到桦木板。
        CraftMove specific = plan.Moves.Single(m => m.TargetSlot == 1);
        Assert.Equal(0, specific.SourceSlot);
    }

    [Fact]
    public void 通配配料优先拿原色的那份不动染色的()
    {
        // 实机日志里出现过 `0←白色 木板×1`：配方要的是"任意木板"（通配），
        // 而玩家的白色木板恰好排在前面，就被当成普通木板消耗掉了。
        // 染色/特殊变体是玩家专门做出来的，通配配料应该优先吃原色那份。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            // 规范 3×3 里的 2×2 是 0,1,3,4——写成前四个会变成 3 格宽，塞不进 2×2。
            Grid("planks", "planks", null, "planks", "planks"),
            gridSize: 2,
            new[]
            {
                new AvailableStack(0, 0, "planks", 4, 10),   // 白色木板，排在前面
                new AvailableStack(0, 1, "planks", 0, 10),   // 原色木板
            },
            maxSets: 1);

        Assert.True(plan.Ok);
        Assert.All(plan.Moves, m => Assert.Equal(1, m.SourceSlot));
    }

    [Fact]
    public void 原色的不够时通配配料才动染色的()
    {
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("planks", "planks"),
            gridSize: 2,
            new[]
            {
                new AvailableStack(0, 0, "planks", 4, 10),   // 白色木板
                new AvailableStack(0, 1, "planks", 0, 1),    // 原色木板只有一块
            },
            maxSets: 1);

        Assert.True(plan.Ok);
        Assert.Equal(2, plan.Moves.Count);
        Assert.Contains(plan.Moves, m => m.SourceSlot == 1);
        Assert.Contains(plan.Moves, m => m.SourceSlot == 0);
    }

    [Fact]
    public void 材料不够时报缺了什么()
    {
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "b"),
            gridSize: 2,
            new[] { new AvailableStack(0, 0, "a", 0, 10) },
            maxSets: 1);

        Assert.Equal(CraftFillFailure.MissingIngredients, plan.Failure);
        Assert.Contains("b", plan.Missing);
        Assert.Empty(plan.Moves);
    }

    [Fact]
    public void 凑不齐的半份不会扣掉材料()
    {
        // a 够 3 份、b 只够 1 份 → 只能成 1 份，而且不能留下"第二份已经搬了 a"的残局。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "b"),
            gridSize: 2,
            new[]
            {
                new AvailableStack(0, 0, "a", 0, 3),
                new AvailableStack(0, 1, "b", 0, 1),
            },
            maxSets: 64);

        Assert.Equal(1, plan.Sets);
        Assert.All(plan.Moves, m => Assert.Equal(1, m.Count));
    }

    [Fact]
    public void 材料可以来自多个来源库存()
    {
        // 来源 0 = 玩家物品栏，来源 1 = 行囊，来源 2 = 存储网络里的箱子。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "b"),
            gridSize: 2,
            new[]
            {
                new AvailableStack(Source: 0, Slot: 3, "a", 0, 1),
                new AvailableStack(Source: 2, Slot: 7, "b", 0, 1),
            },
            maxSets: 1);

        Assert.True(plan.Ok);
        Assert.Contains(plan.Moves, m => m.Source == 0 && m.SourceSlot == 3);
        Assert.Contains(plan.Moves, m => m.Source == 2 && m.SourceSlot == 7);
    }

    [Fact]
    public void 一格的配方也能填()
    {
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a"),
            gridSize: 2,
            new[] { new AvailableStack(0, 0, "a", 0, 1) },
            maxSets: 1);

        Assert.True(plan.Ok);
        Assert.Single(plan.Moves);
        Assert.Equal(0, plan.Moves[0].TargetSlot);
    }

    [Fact]
    public void 一圈围一个的九格配方在工作台上能填满()
    {
        // 我们自己的升级件配方形状。
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a", "a", "a", "a", "b", "a", "a", "a", "a"),
            gridSize: 3,
            new[]
            {
                new AvailableStack(0, 0, "a", 0, 8),
                new AvailableStack(0, 1, "b", 0, 1),
            },
            maxSets: 64);

        Assert.Equal(1, plan.Sets);
        Assert.Equal(9, plan.Moves.Count);
        Assert.Equal(8, plan.Moves.Where(m => m.SourceSlot == 0).Sum(m => m.Count));
    }

    [Fact]
    public void 上限传零就什么都不搬()
    {
        CraftFillPlan plan = CraftFillPlanner.Plan(
            Grid("a"),
            gridSize: 2,
            new[] { new AvailableStack(0, 0, "a", 0, 9) },
            maxSets: 0);

        Assert.False(plan.Ok);
        Assert.Empty(plan.Moves);
    }
}
