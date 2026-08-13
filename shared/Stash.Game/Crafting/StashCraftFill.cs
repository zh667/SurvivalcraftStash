using Engine;
using Game;
using Stash.Shared.Crafting;
using Stash.Shared.Inventory;

namespace Stash.Game;

/// <summary>
/// 一个能"填材料"的目标格子。三种形状：
/// <list type="bullet">
/// <item>按 E 的自带合成格：2×2</item>
/// <item>工作台 / 无线合成终端：3×3</item>
/// <item>**熔炉：N×1**——原料槽是一条直线，不是方阵。
/// <c>ComponentFurnace.FindSmeltingRecipe</c> 只填 <c>m_matchedIngredients[i]</c>（i &lt; 原料槽数），
/// 也就是规范 3×3 的第一行；槽位编号就是"第几个原料槽"，正好落在 0..N-1，
/// 不会碰到燃料/产物/残留那三格（它们在 SlotsCount 的末尾）。</item>
/// </list>
/// </summary>
/// <param name="IsFurnace">
/// true = 只吃冶炼配方（RequiredHeatLevel &gt; 0），false = 只吃合成配方。
/// 两者互斥：熔炉做不了合成，合成格也点不着火。
/// </param>
public sealed record StashCraftTarget(IInventory Grid, int Columns, int Rows, bool IsFurnace)
{
    public int SlotCount => Columns * Rows;

    /// <summary>这个目标要不要这条配方。</summary>
    public bool Accepts(CraftingRecipe recipe) =>
        IsFurnace ? recipe.RequiredHeatLevel > 0f : recipe.RequiredHeatLevel <= 0f;
}

/// <summary>一键填料的结果，给界面拿去显示。</summary>
public sealed record StashFillResult(bool Ok, string Message);

/// <summary>
/// "填材料"按钮背后的活：把配方需要的材料从物品栏 / 行囊 / 存储网络
/// 搬到合成格对应的槽位上。
///
/// ─────────────────────────────────────────────────────────────────────────
/// **为什么这里一件东西都不直接搬**（第一版就是直接搬的，是错的）：
///
/// 联机版里客户端**没有权力改槽位**，而原版的 API 在客户端上表现各不相同：
/// <list type="bullet">
/// <item><c>ComponentInventoryBase.AcquireItems</c>：客户端**直接 return 0 什么都不做**。
/// 第一版把返回值 0 读成"全部收下了"，接着就把合成格清空——联机客户端一按填材料，
/// 格子里的东西直接蒸发。</item>
/// <item><c>AddSlotItems</c> / <c>RemoveSlotItems</c>：客户端上**毫无拦截、就地改本地副本**，
/// 服务端完全不知情，下一次 InventorySync 推回来就把改动冲掉。</item>
/// <item><c>AddSlotItems</c> 内部（<c>AddNetSlotItems</c>）装不下时**静默返回 false**，
/// 而 <c>AddSlotItems</c> 把返回值丢了——从来源扣掉之后加不进去，东西就没了。</item>
/// </list>
///
/// 所以改成和"整理"完全一样的路子：**先在内存里把结果算出来，再交给平台落地**。
/// 单人/主机直接写；联机客户端把计划发给服务端，服务端跑一遍守恒校验
/// （<c>StashServerGuard</c>：只许重排，不许凭空增减）再写。
/// 这样上面三个坑一次全绕开，而且反作弊那关也过得去。
/// ─────────────────────────────────────────────────────────────────────────
///
/// 规划部分在 <see cref="CraftFillPlanner"/>（纯逻辑、有单元测试），
/// 这里负责读库存、在内存里模拟、生成计划。
/// </summary>
public static class StashCraftFill
{
    /// <summary>
    /// 把配方填进合成格。
    /// </summary>
    /// <param name="sources">取料来源，按优先级排。第一个还兼作"腾空合成格时东西退回哪里"。</param>
    /// <param name="fillMax">true = 能凑几份凑几份；false = 只凑一份。</param>
    public static StashFillResult Fill(
        CraftingRecipe recipe,
        StashCraftTarget target,
        IReadOnlyList<IInventory> sources,
        bool fillMax)
    {
        if (recipe == null || target.Grid == null || sources.Count == 0)
        {
            return new StashFillResult(false, StashText.FillFailed);
        }

        // 冶炼配方只进熔炉，合成配方只进合成格，两边互不串门。
        if (!target.Accepts(recipe))
        {
            return new StashFillResult(
                false, target.IsFurnace ? StashText.FillNeedsCraftingGrid : StashText.FillNeedsFurnace);
        }

        if (!RecipeShape.Fits(recipe.Ingredients, target.Columns, target.Rows))
        {
            (int width, int height) = RecipeShape.Extent(recipe.Ingredients);
            return new StashFillResult(
                false, StashText.FillRecipeTooLarge(width, height, target.Columns, target.Rows));
        }

        var world = new FillWorld(target, sources);

        if (world.Sources.Count == 0)
        {
            StashDiag.Log($"填材料：{sources.Count} 块来源全被跳过（多半是创造模式的物品栏），无处取料");
            return new StashFillResult(false, StashText.FillNoUsableSource);
        }

        StashDiag.Log($"填材料：目标 {target.Columns}×{target.Rows}"
            + $"（{(target.IsFurnace ? "熔炉" : "合成格")}，库存 {world.Grid.Inventory.GetType().Name}）"
            + $"，来源 {world.Sources.Count} 块，权威端={StashPlatform.Current.IsAuthoritative}");
        StashDiag.Log($"填材料：合成格现状 {StashDiag.Describe(target.Grid, 0, target.SlotCount)}");

        // ── 第一步：在模型里把合成格腾空，东西退回来源 ──
        if (!world.EvacuateGrid(out string blocked))
        {
            StashDiag.Warn($"填材料：腾空失败（{blocked}），本次不做任何改动");
            return new StashFillResult(false, StashText.FillNoRoomToClear);
        }

        // ── 第二步：按腾空**之后**的状态收集可用材料 ──
        // 顺序不能反：合成格里原本那几个材料退回来源后，正好可以再被用上。
        List<AvailableStack> available = world.CollectAvailable();
        int maxSets = fillMax ? MaxSets(recipe, target) : 1;

        CraftFillPlan plan = CraftFillPlanner.Plan(
            recipe.Ingredients, target.Columns, target.Rows, available, maxSets);

        if (plan.Failure == CraftFillFailure.RecipeTooLarge)
        {
            (int width, int height) = RecipeShape.Extent(recipe.Ingredients);
            return new StashFillResult(
                false, StashText.FillRecipeTooLarge(width, height, target.Columns, target.Rows));
        }

        if (!plan.Ok)
        {
            string missing = DescribeMissing(plan.Missing);
            StashDiag.Log($"填材料：材料不够，缺 {missing}（可用堆 {available.Count} 组）");
            return new StashFillResult(false, StashText.FillMissing(missing));
        }

        // ── 第三步：把搬运落到模型上 ──
        if (!world.ApplyMoves(plan, out string failure))
        {
            StashDiag.Warn($"填材料：模拟搬运失败（{failure}），本次不做任何改动");
            return new StashFillResult(false, StashText.FillFailed);
        }

        // ── 第四步：交给平台落地。到这一步为止，游戏里的任何一格都还没被改过 ──
        StashPlan gamePlan = world.BuildPlan();
        if (gamePlan.IsEmpty)
        {
            StashDiag.Log("填材料：算下来没有任何格子需要改动");
            return new StashFillResult(false, StashText.FillFailed);
        }

        foreach (string line in world.EvacuationLog)
        {
            StashDiag.Log($"填材料：腾空 {line}");
        }

        LogPlan(gamePlan);
        StashPlatform.Current.Execute(gamePlan);

        // 联机客户端只是把计划发出去了，真正生效要等服务端回包。
        return new StashFillResult(
            true,
            StashPlatform.Current.IsAuthoritative ? StashText.Filled(plan.Sets) : StashText.FillSent);
    }

    /// <summary>
    /// 哪些格子的材料不够。返回的是**规范 3×3 的下标**（0~8），和
    /// <c>CraftingRecipe.Ingredients</c> 的下标一致，界面拿去把那几格标红。
    ///
    /// 按配料串分组统计"要几个 / 有几个"：同一样配料占两格就要两个。
    /// 通配配料（没写 data）和挑剔配料（写了 data）之间的抢占关系这里**不细算**——
    /// 那是 <see cref="CraftFillPlanner"/> 的活。这里只是给玩家一个红底提示，
    /// 宁可偶尔漏标一格，也别为了个提示把逻辑做重。
    /// </summary>
    public static HashSet<int> FindShortCells(CraftingRecipe recipe, IReadOnlyList<IInventory> sources)
    {
        var short_ = new HashSet<int>();
        if (recipe == null || sources.Count == 0)
        {
            return short_;
        }

        var needed = new Dictionary<string, int>();
        for (int i = 0; i < recipe.Ingredients.Length; i++)
        {
            string? ingredient = recipe.Ingredients[i];
            if (!string.IsNullOrEmpty(ingredient))
            {
                needed[ingredient] = needed.TryGetValue(ingredient, out int n) ? n + 1 : 1;
            }
        }

        if (needed.Count == 0)
        {
            return short_;
        }

        List<AvailableStack> available = CollectFrom(sources);

        foreach ((string ingredient, int count) in needed)
        {
            CraftIngredient want = CraftIngredient.Parse(ingredient);
            int have = 0;
            foreach (AvailableStack stack in available)
            {
                if (want.Matches(stack.CraftingId, stack.Data))
                {
                    have += stack.Count;
                    if (have >= count)
                    {
                        break;
                    }
                }
            }

            if (have < count)
            {
                for (int i = 0; i < recipe.Ingredients.Length; i++)
                {
                    if (recipe.Ingredients[i] == ingredient)
                    {
                        short_.Add(i);
                    }
                }
            }
        }

        return short_;
    }

    /// <summary>
    /// 一次最多能凑几份：受限于合成格每格的容量（= 该物品的堆叠上限）。
    /// 不看这个的话，"凑满"会往一格里塞超过上限的数量。
    /// </summary>
    private static int MaxSets(CraftingRecipe recipe, StashCraftTarget target)
    {
        if (!RecipeShape.TryPlace(recipe.Ingredients, target.Columns, target.Rows, out var placements))
        {
            return 1;
        }

        int cap = int.MaxValue;
        foreach ((int slot, string ingredient) in placements)
        {
            CraftIngredient want = CraftIngredient.Parse(ingredient);
            Block[] blocks;
            try
            {
                blocks = BlocksManager.FindBlocksByCraftingId(want.CraftingId);
            }
            catch
            {
                return 1;
            }

            if (blocks.Length == 0)
            {
                return 1;
            }

            int value = Terrain.MakeBlockValue(blocks[0].BlockIndex, 0, want.Data ?? 0);
            int capacity;
            try
            {
                capacity = target.Grid.GetSlotCapacity(slot, value);
            }
            catch
            {
                capacity = blocks[0].GetMaxStacking(value);
            }

            cap = MathUtils.Min(cap, MathUtils.Max(capacity, 1));
        }

        return cap == int.MaxValue ? 1 : cap;
    }

    /// <summary>直接从游戏库存收集（只读，给标红提示用）。</summary>
    private static List<AvailableStack> CollectFrom(IReadOnlyList<IInventory> sources)
    {
        var stacks = new List<AvailableStack>();

        for (int source = 0; source < sources.Count; source++)
        {
            IInventory inventory = sources[source];
            if (inventory == null)
            {
                continue;
            }

            for (int slot = 0; slot < inventory.SlotsCount; slot++)
            {
                int count = inventory.GetSlotCount(slot);
                if (count <= 0)
                {
                    continue;
                }

                string craftingId = CraftingIdOf(inventory.GetSlotValue(slot));
                if (!string.IsNullOrEmpty(craftingId))
                {
                    stacks.Add(new AvailableStack(
                        source, slot, craftingId, Terrain.ExtractData(inventory.GetSlotValue(slot)), count));
                }
            }
        }

        return stacks;
    }

    /// <summary>
    /// 物品值 → craftingId。必须用**虚方法** <c>GetCraftingId(value)</c>，
    /// 不能读 <c>Block.CraftingId</c> 字段：染色方块之类每个 data 的 craftingId 不一样。
    /// </summary>
    private static string CraftingIdOf(int value)
    {
        try
        {
            int contents = Terrain.ExtractContents(value);
            if (contents <= 0 || contents >= BlocksManager.Blocks.Length)
            {
                return string.Empty;
            }

            return BlocksManager.Blocks[contents].GetCraftingId(value) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void LogPlan(StashPlan plan)
    {
        foreach ((IInventory inventory, IReadOnlyList<SlotAssignment> assignments) in plan.Parts)
        {
            var parts = new List<string>();
            foreach (SlotAssignment assignment in assignments)
            {
                parts.Add(assignment.Count > 0
                    ? $"{assignment.SlotIndex}←{StashDiag.Name(assignment.Value)}×{assignment.Count}"
                    : $"{assignment.SlotIndex}←空");
            }

            StashDiag.Log($"填材料：计划 {inventory.GetType().Name}"
                + $"(id={StashDiag.InventoryId(inventory)}) {string.Join(", ", parts)}");
        }
    }

    private static string DescribeMissing(IReadOnlyList<string> missing)
    {
        var names = new List<string>();

        foreach (string ingredient in missing)
        {
            CraftIngredient want = CraftIngredient.Parse(ingredient);
            string name = want.CraftingId;

            try
            {
                Block[] blocks = BlocksManager.FindBlocksByCraftingId(want.CraftingId);
                if (blocks.Length > 0)
                {
                    int value = Terrain.MakeBlockValue(blocks[0].BlockIndex, 0, want.Data ?? 0);
                    name = blocks[0].GetDisplayName(null!, value) ?? want.CraftingId;
                }
            }
            catch
            {
                // 显示名拿不到就用 craftingId，总比什么都不说强。
            }

            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names.Count > 0 ? string.Join("、", names) : "?";
    }

    /// <summary>
    /// 一块库存的内存副本。所有搬运先在这上面跑完，确认无误再一次性生成计划。
    ///
    /// 这样做的另一个好处：**中途失败等于什么都没发生**。
    /// 直接搬的版本一旦搬到一半失败，玩家的东西就散落在两个容器里了。
    /// </summary>
    private sealed class InventoryModel
    {
        private readonly int[] m_values;
        private readonly int[] m_counts;
        private readonly bool[] m_dirty;

        public InventoryModel(IInventory inventory)
        {
            Inventory = inventory;
            int slots = inventory.SlotsCount;
            m_values = new int[slots];
            m_counts = new int[slots];
            m_dirty = new bool[slots];

            for (int slot = 0; slot < slots; slot++)
            {
                m_counts[slot] = inventory.GetSlotCount(slot);
                m_values[slot] = m_counts[slot] > 0 ? inventory.GetSlotValue(slot) : 0;
            }
        }

        public IInventory Inventory { get; }

        public int SlotsCount => m_counts.Length;

        public int CountAt(int slot) => m_counts[slot];

        public int ValueAt(int slot) => m_values[slot];

        public int CapacityAt(int slot, int value)
        {
            try
            {
                return Inventory.GetSlotCapacity(slot, value);
            }
            catch
            {
                return 0;
            }
        }

        public void Take(int slot, int count)
        {
            m_counts[slot] -= count;
            if (m_counts[slot] <= 0)
            {
                m_counts[slot] = 0;
                m_values[slot] = 0;
            }

            m_dirty[slot] = true;
        }

        public void Put(int slot, int value, int count)
        {
            m_values[slot] = value;
            m_counts[slot] += count;
            m_dirty[slot] = true;
        }

        /// <summary>找一格已经放着同种物品、而且还没满的格子。</summary>
        public int FindStackToMerge(int value)
        {
            for (int slot = 0; slot < SlotsCount; slot++)
            {
                if (m_counts[slot] > 0 && m_values[slot] == value && m_counts[slot] < CapacityAt(slot, value))
                {
                    return slot;
                }
            }

            return -1;
        }

        /// <summary>找一个空格。</summary>
        public int FindEmptySlot(int value)
        {
            for (int slot = 0; slot < SlotsCount; slot++)
            {
                if (m_counts[slot] == 0 && CapacityAt(slot, value) > 0)
                {
                    return slot;
                }
            }

            return -1;
        }

        /// <summary>把改动过的格子整理成赋值列表。没动过的格子**绝不列进来**——
        /// 列进去等于让服务端多做一次无谓的清空重填。</summary>
        public List<SlotAssignment> Changes()
        {
            var changes = new List<SlotAssignment>();
            for (int slot = 0; slot < SlotsCount; slot++)
            {
                if (m_dirty[slot])
                {
                    changes.Add(new SlotAssignment(slot, m_counts[slot] > 0 ? m_values[slot] : 0, m_counts[slot]));
                }
            }

            return changes;
        }
    }

    /// <summary>合成格 + 所有来源的整体模型。</summary>
    private sealed class FillWorld
    {
        private readonly StashCraftTarget m_target;

        public FillWorld(StashCraftTarget target, IReadOnlyList<IInventory> sources)
        {
            m_target = target;
            Grid = new InventoryModel(target.Grid);

            foreach (IInventory inventory in sources)
            {
                if (inventory == null)
                {
                    continue;
                }

                // 合成格自己混进来源里会让"腾空"变成原地打转，直接跳过。
                if (ReferenceEquals(inventory, target.Grid))
                {
                    continue;
                }

                // ── 创造模式的物品栏一律不当来源 ──
                //
                // 它根本不是个正常库存：`GetSlotCount` 对任何非空格**一律返回 9999**，
                // 容量能到 99980001，取走也不会变少。按它算出来的计划毫无意义。
                // 而且联机版服务端的守恒校验会**直接拒收**任何碰到创造物品栏的请求
                // （StashServerGuard："不允许对创造物品栏执行整理"），
                // 整个包被拒 = 连合成格那一半也不会生效。
                //
                // 创造模式本来就能直接拿到任何东西，跳过它没有实际损失。
                if (inventory is ComponentCreativeInventory)
                {
                    StashDiag.Once("fill-creative",
                        "填材料：跳过创造模式的物品栏（它的格子数量是假的 9999，联机时服务端也会拒收）。"
                        + "要验证填材料请用生存模式。");
                    continue;
                }

                Sources.Add(new InventoryModel(inventory));
            }
        }

        public InventoryModel Grid { get; }

        public List<InventoryModel> Sources { get; } = new();

        /// <summary>腾空的去向，等整件事确定要执行了再打进日志。</summary>
        public List<string> EvacuationLog { get; } = new();

        /// <summary>
        /// 把合成格里现有的东西退回来源。有一件退不回去就整体失败。
        ///
        /// ─────────────────────────────────────────────────────────────────────────
        /// **落点规则：先在所有来源里找同种物品的堆合并，都找不到才用空格。**
        ///
        /// 第一版是"按来源顺序，每个来源内部先合并后占空格"，实机踩了坑：
        /// 玩家从存储网络取了 4 个木板，换配方时这 4 个木板**跑进了行囊的空格**——
        /// 而存储网络里明明有一摞木板等着合并。玩家的原话是"木板没有退回终端"，
        /// 从他的角度看就是东西被吞了（东西其实在行囊里，只是没人想得到去那儿找）。
        ///
        /// 根因是优先级搞反了：**行囊的空格**排在**网络容器里的同种堆**前面。
        /// 改成两轮全局扫描后，材料会回到"已经有这东西的地方"，
        /// 也就是绝大多数情况下它原本来的地方。
        /// ─────────────────────────────────────────────────────────────────────────
        /// </summary>
        public bool EvacuateGrid(out string blocked)
        {
            blocked = string.Empty;

            for (int slot = 0; slot < m_target.SlotCount && slot < Grid.SlotsCount; slot++)
            {
                int count = Grid.CountAt(slot);
                if (count <= 0)
                {
                    continue;
                }

                int value = Grid.ValueAt(slot);
                var went = new List<string>();

                // 第一轮：所有来源里找同种物品的堆。
                int left = Deposit(value, count, merge: true, went);

                // 第二轮：还有剩的，才按来源顺序占空格。
                if (left > 0)
                {
                    left = Deposit(value, left, merge: false, went);
                }

                if (left > 0)
                {
                    blocked = $"{StashDiag.Name(value)} 还剩 {left} 件没地方放";
                    return false;
                }

                // 只记下来，**先别打日志**：这一步还只是在内存模型里，
                // 后面材料不够就会整个放弃。早先在这里直接打，日志上就出现了
                // "腾空 木板×4 → 箱子" 紧跟着 "材料不够" ——看着像东西已经搬走了，
                // 实际什么都没发生，排查时会被带偏。
                EvacuationLog.Add($"{StashDiag.Name(value)}×{count} → {string.Join("，", went)}");
                Grid.Take(slot, count);
            }

            return true;
        }

        /// <param name="merge">true = 只往已有的同种堆里塞；false = 只占空格。</param>
        /// <returns>还没安置下的数量。</returns>
        private int Deposit(int value, int left, bool merge, List<string> went)
        {
            foreach (InventoryModel source in Sources)
            {
                while (left > 0)
                {
                    int room = merge ? source.FindStackToMerge(value) : source.FindEmptySlot(value);
                    if (room < 0)
                    {
                        break;
                    }

                    int space = source.CapacityAt(room, value) - source.CountAt(room);
                    if (space <= 0)
                    {
                        break;
                    }

                    int move = MathUtils.Min(space, left);
                    source.Put(room, value, move);
                    left -= move;

                    went.Add($"{source.Inventory.GetType().Name} 槽{room}×{move}{(merge ? "（合并）" : "（空格）")}");
                }

                if (left == 0)
                {
                    break;
                }
            }

            return left;
        }

        /// <summary>按模型的**当前**状态收集可用材料。</summary>
        public List<AvailableStack> CollectAvailable()
        {
            var stacks = new List<AvailableStack>();

            for (int source = 0; source < Sources.Count; source++)
            {
                InventoryModel model = Sources[source];
                for (int slot = 0; slot < model.SlotsCount; slot++)
                {
                    int count = model.CountAt(slot);
                    if (count <= 0)
                    {
                        continue;
                    }

                    int value = model.ValueAt(slot);
                    string craftingId = CraftingIdOf(value);
                    if (!string.IsNullOrEmpty(craftingId))
                    {
                        stacks.Add(new AvailableStack(
                            source, slot, craftingId, Terrain.ExtractData(value), count));
                    }
                }
            }

            return stacks;
        }

        /// <summary>把规划器算出来的搬运落到模型上，并校验合成格那一侧装得下。</summary>
        public bool ApplyMoves(CraftFillPlan plan, out string failure)
        {
            failure = string.Empty;

            foreach (CraftMove move in plan.Moves)
            {
                if (move.Source < 0 || move.Source >= Sources.Count)
                {
                    failure = $"来源下标 {move.Source} 越界";
                    return false;
                }

                InventoryModel source = Sources[move.Source];
                if (move.SourceSlot < 0 || move.SourceSlot >= source.SlotsCount)
                {
                    failure = $"来源槽位 {move.SourceSlot} 越界";
                    return false;
                }

                if (source.CountAt(move.SourceSlot) < move.Count)
                {
                    failure = $"来源第 {move.SourceSlot} 格只有 {source.CountAt(move.SourceSlot)} 件，要 {move.Count} 件";
                    return false;
                }

                int value = source.ValueAt(move.SourceSlot);

                if (move.TargetSlot < 0 || move.TargetSlot >= Grid.SlotsCount)
                {
                    failure = $"目标槽位 {move.TargetSlot} 越界";
                    return false;
                }

                // 原版 AddSlotItems 装不下时是**静默失败**的，这里必须自己先算。
                int existing = Grid.CountAt(move.TargetSlot);
                if (existing > 0 && Grid.ValueAt(move.TargetSlot) != value)
                {
                    failure = $"合成格第 {move.TargetSlot} 格已经放着别的东西";
                    return false;
                }

                int capacity = Grid.CapacityAt(move.TargetSlot, value);
                if (existing + move.Count > capacity)
                {
                    failure = $"合成格第 {move.TargetSlot} 格装不下：{existing}+{move.Count} > {capacity}";
                    return false;
                }

                source.Take(move.SourceSlot, move.Count);
                Grid.Put(move.TargetSlot, value, move.Count);
            }

            return true;
        }

        /// <summary>生成给平台执行的计划。同一块库存只出现一次。</summary>
        public StashPlan BuildPlan()
        {
            var plan = new StashPlan();
            plan.Add(Grid.Inventory, Grid.Changes());

            foreach (InventoryModel source in Sources)
            {
                plan.Add(source.Inventory, source.Changes());
            }

            return plan;
        }
    }
}
