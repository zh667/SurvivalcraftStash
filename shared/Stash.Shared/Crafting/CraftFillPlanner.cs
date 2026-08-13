namespace Stash.Shared.Crafting;

/// <summary>一份可用的物品：来自第几个来源库存的第几格。</summary>
public readonly record struct AvailableStack(int Source, int Slot, string CraftingId, int Data, int Count);

/// <summary>一次搬运：把来源某格的若干个搬到合成格的某个槽位。</summary>
public readonly record struct CraftMove(int Source, int SourceSlot, int TargetSlot, int Count);

public enum CraftFillFailure
{
    None,

    /// <summary>包围盒超出了合成格。2×2 的自带合成格装不下 3 格宽的配方。</summary>
    RecipeTooLarge,

    /// <summary>东西不够，一份都凑不齐。</summary>
    MissingIngredients,
}

public sealed class CraftFillPlan
{
    public required IReadOnlyList<CraftMove> Moves { get; init; }

    /// <summary>实际能凑出几份。</summary>
    public required int Sets { get; init; }

    public required CraftFillFailure Failure { get; init; }

    /// <summary>凑不齐时缺的是哪几样（配料串原文，给界面拿去显示名字）。</summary>
    public required IReadOnlyList<string> Missing { get; init; }

    public bool Ok => Failure == CraftFillFailure.None && Sets > 0;

    public static CraftFillPlan Failed(CraftFillFailure failure, IReadOnlyList<string>? missing = null) =>
        new()
        {
            Moves = Array.Empty<CraftMove>(),
            Sets = 0,
            Failure = failure,
            Missing = missing ?? Array.Empty<string>(),
        };
}

/// <summary>
/// "一键填料"的规划器：给定一个配方和手上有什么，算出要把哪些东西搬到合成格的哪些槽位。
///
/// 纯逻辑，不碰游戏对象——因为这是整个 JEI 里唯一能写单元测试的部分，
/// 而实机验证要等到打包丢进 Windows 才做得了。
///
/// **为什么一份一份地凑**：配料可能"通配"也可能"挑 data"。
/// 比如某配方要 <c>planks</c>（任意木板）和 <c>planks:2</c>（只要桦木板），
/// 而背包里只有两块桦木板——先来先得的话通配那格会把桦木板抢走，
/// 结果挑剔的那格反而没得用，明明能合成却报"缺料"。
/// 所以每凑一份都**先满足写了 data 的配料**，再喂通配的。
/// </summary>
public static class CraftFillPlanner
{
    /// <summary>一次最多凑多少份，防止调用方传了个离谱的上限把循环拖住。</summary>
    public const int MaxSetsCap = 999;

    public static CraftFillPlan Plan(
        IReadOnlyList<string?> ingredients,
        int gridSize,
        IReadOnlyList<AvailableStack> available,
        int maxSets) =>
        Plan(ingredients, gridSize, gridSize, available, maxSets);

    /// <param name="columns">格子列数。工作台 3、自带合成格 2、熔炉 = 原料槽数。</param>
    /// <param name="rows">格子行数。熔炉是 1。</param>
    public static CraftFillPlan Plan(
        IReadOnlyList<string?> ingredients,
        int columns,
        int rows,
        IReadOnlyList<AvailableStack> available,
        int maxSets)
    {
        if (!RecipeShape.TryPlace(ingredients, columns, rows, out List<(int Slot, string Ingredient)> placements))
        {
            return CraftFillPlan.Failed(CraftFillFailure.RecipeTooLarge);
        }

        // 挑剔的（写了 data 的）排前面，理由见类注释。
        placements.Sort((a, b) =>
        {
            bool sa = CraftIngredient.Parse(a.Ingredient).IsSpecific;
            bool sb = CraftIngredient.Parse(b.Ingredient).IsSpecific;
            return sa == sb ? a.Slot.CompareTo(b.Slot) : sb.CompareTo(sa);
        });

        int[] remaining = new int[available.Count];
        for (int i = 0; i < available.Count; i++)
        {
            remaining[i] = available[i].Count;
        }

        var moves = new Dictionary<(int Source, int SourceSlot, int TargetSlot), int>();
        var missing = new List<string>();
        int sets = 0;
        int cap = Math.Min(Math.Max(maxSets, 0), MaxSetsCap);

        while (sets < cap)
        {
            if (!TryTakeOneSet(placements, available, remaining, moves, sets == 0 ? missing : null))
            {
                break;
            }

            sets++;
        }

        if (sets == 0)
        {
            return CraftFillPlan.Failed(CraftFillFailure.MissingIngredients, missing);
        }

        var result = new List<CraftMove>(moves.Count);
        foreach (((int source, int sourceSlot, int targetSlot), int count) in moves)
        {
            result.Add(new CraftMove(source, sourceSlot, targetSlot, count));
        }

        // 顺序稳定一点，方便测试断言，也让实机日志好读。
        result.Sort((a, b) => a.TargetSlot != b.TargetSlot
            ? a.TargetSlot.CompareTo(b.TargetSlot)
            : a.Source != b.Source ? a.Source.CompareTo(b.Source) : a.SourceSlot.CompareTo(b.SourceSlot));

        return new CraftFillPlan
        {
            Moves = result,
            Sets = sets,
            Failure = CraftFillFailure.None,
            Missing = Array.Empty<string>(),
        };
    }

    /// <summary>
    /// 给一格配料挑一堆材料。
    ///
    /// **通配配料（没写 data）优先拿 data == 0 的那堆。**
    /// 理由来自实机日志里的一行 <c>0←白色 木板×1</c>：
    /// 工作台配方要的是"任意木板"，而玩家的白色木板恰好排在可用列表前面，
    /// 就被当成普通木板烧掉了。染色件是玩家专门做出来的，
    /// 通配的格子没道理优先吃它——先吃原色，原色不够了再动。
    ///
    /// 挑 data 的配料不走这条：它本来就指定了要哪一种，第一个匹配的就是对的。
    /// </summary>
    private static int FindStack(
        CraftIngredient want, IReadOnlyList<AvailableStack> available, int[] remaining)
    {
        int fallback = -1;

        for (int i = 0; i < available.Count; i++)
        {
            if (remaining[i] <= 0 || !want.Matches(available[i].CraftingId, available[i].Data))
            {
                continue;
            }

            if (want.IsSpecific || available[i].Data == 0)
            {
                return i;
            }

            // 通配 + 非原色：记下来当备胎，先继续找原色的。
            if (fallback < 0)
            {
                fallback = i;
            }
        }

        return fallback;
    }

    /// <summary>
    /// 试着凑一份。凑不齐就把这一份已经扣掉的量**原样退回**，不留半份。
    /// </summary>
    private static bool TryTakeOneSet(
        List<(int Slot, string Ingredient)> placements,
        IReadOnlyList<AvailableStack> available,
        int[] remaining,
        Dictionary<(int, int, int), int> moves,
        List<string>? missingOut)
    {
        var taken = new List<(int Index, int Slot, string Ingredient)>(placements.Count);

        foreach ((int slot, string ingredient) in placements)
        {
            CraftIngredient want = CraftIngredient.Parse(ingredient);
            int found = FindStack(want, available, remaining);

            if (found < 0)
            {
                missingOut?.Add(ingredient);

                // 回滚这一份已经拿掉的，避免留下"半份"的扣减。
                foreach ((int index, int _, string _) in taken)
                {
                    remaining[index]++;
                }

                return false;
            }

            remaining[found]--;
            taken.Add((found, slot, ingredient));
        }

        foreach ((int index, int slot, string _) in taken)
        {
            var key = (available[index].Source, available[index].Slot, slot);
            moves[key] = moves.TryGetValue(key, out int existing) ? existing + 1 : 1;
        }

        return true;
    }
}
