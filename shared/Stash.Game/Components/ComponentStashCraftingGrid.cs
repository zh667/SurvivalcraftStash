using System.Globalization;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 无线合成终端用的 3×3 合成格。挂在**玩家实体**上，跟着存档走。
///
/// ─────────────────────────────────────────────────────────────────────────
/// **为什么不直接继承 <see cref="ComponentCraftingTable"/>**（那样能白嫖一堆逻辑）：
///
/// <c>ComponentGui</c> 按 E 开物品栏时是这么拿合成格的：
/// <code>
/// m_componentPlayer.Entity.FindComponent&lt;ComponentCraftingTable&gt;(throwOnError: true)
/// </code>
/// 玩家身上本来就有一个 2×2 的 <c>ComponentCraftingTable</c>。再挂一个同类型的，
/// 这句要么抛异常、要么随机拿到我们这个 3×3 的——**按 E 直接打不开物品栏**，
/// 属于把整个游戏搞坏的级别。所以这里从 <see cref="ComponentInventoryBase"/> 派生，
/// 类型不同就不会被那句捞到（行囊组件已经这么干了，实机验证过）。
///
/// 代价是 <c>UpdateCraftingResult</c> 那套要自己抄一遍。下面这份是照着原版
/// <c>ComponentCraftingTable</c> 逐行改的，只是把网格边长写死成 3。
/// ─────────────────────────────────────────────────────────────────────────
///
/// 槽位布局和原版一致：0~8 是 3×3 网格，倒数第二格是产物，最后一格是残留。
/// </summary>
public class ComponentStashCraftingGrid : ComponentInventoryBase
{
    public const int GridSize = 3;

    /// <summary>3×3 + 产物 + 残留。</summary>
    public const int RequiredSlots = GridSize * GridSize + 2;

    private readonly string[] m_matchedIngredients = new string[9];

    private SubsystemTerrain m_subsystemTerrain = null!;

    public CraftingRecipe? MatchedRecipe { get; private set; }

    public int RemainsSlotIndex => SlotsCount - 1;

    public int ResultSlotIndex => SlotsCount - 2;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);

        // 这块格子挂在玩家身上、跟着存档走，关界面**不会**自动清空。
        // 上一局留在里面的东西从任何别的界面都看不见，只有这行日志能证明它还在。
        // 格子数也一起打：SlotsCount 和 RequiredSlots 对不上说明 .xdb 里的 Value 改漏了，
        // 那会让产物格/残留格错位——症状是"合成出来的东西跑到奇怪的地方"。
        Stash.Game.StashDiag.Log(
            $"合成终端格子载入：id={Stash.Game.StashDiag.InventoryId(this)} "
            + $"槽位 {SlotsCount}（应为 {RequiredSlots}）"
            + $"，网格内容 {Stash.Game.StashDiag.Describe(this, 0, GridSize * GridSize)}"
            + $"，产物格 {Stash.Game.StashDiag.Describe(this, ResultSlotIndex, 1)}"
            + $"，残留格 {Stash.Game.StashDiag.Describe(this, RemainsSlotIndex, 1)}");

        if (SlotsCount != RequiredSlots)
        {
            Stash.Game.StashDiag.Warn(
                $"合成终端格子槽位数不对（{SlotsCount} ≠ {RequiredSlots}），产物/残留格会错位，"
                + "检查 StashDatabase.xdb 里 ComponentStashCraftingGrid 的 Value");
        }

        // 产物格是从存档读回来的，而 MatchedRecipe 是内存字段、此刻还是 null——
        // 两者不同步。<see cref="RemoveSlotItems"/> 已经堵死了"白拿"那条路，
        // 但玩家仍会看到一个点不动的产物，要等下一次格子变动才自己纠正。
        // 这里当场重算一次，把状态对齐。
        //
        // 放在最后：UpdateResult 要用 m_subsystemTerrain，得等上面取到。
        // 出错也不能挡住进世界——大不了退回"点一下才纠正"。
        try
        {
            UpdateResult();
        }
        catch (Exception exception)
        {
            Stash.Game.StashDiag.Warn($"合成终端载入时重算产物失败：{exception.Message}");
        }
    }

    /// <summary>关界面时报一次残留，让"东西留在合成格里"这件事在日志里留痕。</summary>
    public void ReportLeftovers(string when)
    {
        int total = 0;
        for (int slot = 0; slot < GridSize * GridSize; slot++)
        {
            total += GetSlotCount(slot);
        }

        if (total > 0)
        {
            Stash.Game.StashDiag.Log($"合成终端{when}：网格里还留着 "
                + Stash.Game.StashDiag.Describe(this, 0, GridSize * GridSize));
        }
    }

    /// <summary>产物和残留两格只能取不能放，和原版一致。</summary>
    public override int GetSlotCapacity(int slotIndex, int value) =>
        slotIndex < SlotsCount - 2 ? base.GetSlotCapacity(slotIndex, value) : 0;

    public override void AddSlotItems(int slotIndex, int value, int count)
    {
        base.AddSlotItems(slotIndex, value, count);
        UpdateResult();
    }

    /// <summary>
    /// ─────────────────────────────────────────────────────────────────────────
    /// **产物格必须单独一支，而且 <see cref="MatchedRecipe"/> 为 null 时什么都不给。**
    ///
    /// 早先写成了 <c>if (是产物格 &amp;&amp; MatchedRecipe != null) 取产物; else 走基类</c>，
    /// 于是"产物格 + 配方为 null"会掉进 else，**把产物白送出去而不扣材料**。
    /// 这不是理论问题，实机日志里那个状态每次重进世界都会出现：
    /// <code>
    /// 合成终端格子载入：… 网格内容 0:木板×1, 3:木板×1，产物格 9:木棍×4
    /// </code>
    /// 槽位是存档存下来的，而 <c>MatchedRecipe</c> 是**内存字段，载入后是 null**。
    /// 于是重进世界后第一次拖走产物 = 白拿一份；<c>UpdateResult</c> 紧接着又按
    /// 原封不动的材料重算出一份产物，可以反复刷。
    ///
    /// 原版 <c>ComponentCraftingTable</c> 没这个洞，因为它的产物格那一支**没有 else**：
    /// 配方为 null 就一件不给，然后靠末尾的 <c>UpdateCraftingResult</c> 自己纠正。
    /// 这里照抄原版的形状。
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public override int RemoveSlotItems(int slotIndex, int count)
    {
        int removed = 0;

        if (slotIndex == ResultSlotIndex)
        {
            // 配方为 null 就一件不给。绝不能退到基类，那等于无条件放行。
            if (MatchedRecipe != null)
            {
                // 取产物前后的网格各记一次。**这是唯一能证明"材料真的被扣了"的证据**——
                // 关界面那条日志只在面板切换时才打，中间取了几次产物完全看不出来，
                // 上一轮就是因为这个，光看日志没法判断复制漏洞修没修好。
                string before = Stash.Game.StashDiag.Describe(this, 0, GridSize * GridSize);

                // 产物的 value 要在取之前存下来：TakeResult 会把产物格扣空，
                // 之后再读就是 0，日志里会印成 `?0`（实机日志里就是这么印的）。
                int productValue = m_slots[ResultSlotIndex].Value;

                removed = TakeResult(count);
                Stash.Game.StashDiag.Log(
                    $"合成终端取产物：{Stash.Game.StashDiag.Name(productValue)}×{removed}"
                    + $"，材料 {before} → {Stash.Game.StashDiag.Describe(this, 0, GridSize * GridSize)}");
            }
            else
            {
                // 重进世界后产物格是存档里的旧值、而配方是内存字段（null），两者不同步。
                // 这条日志出现 = 白拿那条路被挡住了，是**好事**。
                Stash.Game.StashDiag.Log(
                    $"合成终端：产物格被取但当前没有匹配的配方，不发放"
                    + $"（产物格里是 {Stash.Game.StashDiag.Describe(this, ResultSlotIndex, 1)}，"
                    + "重进世界后的正常现象，已挡住白拿）");
            }
        }
        else
        {
            removed = base.RemoveSlotItems(slotIndex, count);
        }

        UpdateResult();
        return removed;
    }

    /// <summary>
    /// 取走产物：按份取，并且同步扣掉网格里的材料、补上残留。
    /// 这一段的形状照抄原版 <c>ComponentCraftingTable.RemoveSlotItems</c>。
    /// </summary>
    private int TakeResult(int count)
    {
        CraftingRecipe recipe = MatchedRecipe!;

        if (recipe.RemainsValue != 0 && recipe.RemainsCount > 0)
        {
            Slot remains = m_slots[RemainsSlotIndex];
            if (remains.Count == 0 || remains.Value == recipe.RemainsValue)
            {
                int room = BlocksManager.Blocks[Terrain.ExtractContents(recipe.RemainsValue)]
                    .GetMaxStacking(recipe.RemainsValue) - remains.Count;
                count = MathUtils.Min(count, room / recipe.RemainsCount * recipe.ResultCount);
            }
            else
            {
                // 残留格被别的东西占着，这一次就取不了。
                count = 0;
            }
        }

        // 只能整份整份地取。
        count = count / recipe.ResultCount * recipe.ResultCount;

        int removed = base.RemoveSlotItems(ResultSlotIndex, count);
        if (removed <= 0)
        {
            return removed;
        }

        int sets = removed / recipe.ResultCount;

        for (int i = 0; i < 9; i++)
        {
            if (string.IsNullOrEmpty(m_matchedIngredients[i]))
            {
                continue;
            }

            // 规范 3×3 的下标 → 我们的槽位下标。这里网格边长就是 3，所以是一一对应，
            // 但仍然照原版的算式写，将来改边长不会漏。
            int slot = i % 3 + GridSize * (i / 3);
            m_slots[slot].Count = MathUtils.Max(m_slots[slot].Count - sets, 0);
            SlotChanged(slot);
        }

        if (recipe.RemainsValue != 0 && recipe.RemainsCount > 0)
        {
            m_slots[RemainsSlotIndex].Value = recipe.RemainsValue;
            m_slots[RemainsSlotIndex].Count += sets * recipe.RemainsCount;
            SlotChanged(RemainsSlotIndex);
        }

        return removed;
    }

    /// <summary>网格内容变了就重算产物。</summary>
    public void UpdateResult()
    {
        int minCount = int.MaxValue;

        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                int canonical = x + y * 3;
                int slot = x + y * GridSize;

                int value = GetSlotValue(slot);
                int count = GetSlotCount(slot);

                if (count > 0)
                {
                    Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
                    m_matchedIngredients[canonical] = block.CraftingId + ":"
                        + Terrain.ExtractData(value).ToString(CultureInfo.InvariantCulture);
                    minCount = MathUtils.Min(minCount, count);
                }
                else
                {
                    m_matchedIngredients[canonical] = null!;
                }
            }
        }

        CraftingRecipe? recipe = FindRecipe();

        if (recipe != null && recipe.ResultValue != 0)
        {
            MatchedRecipe = recipe;
            m_slots[ResultSlotIndex].Value = recipe.ResultValue;
            m_slots[ResultSlotIndex].Count = recipe.ResultCount * minCount;
        }
        else
        {
            MatchedRecipe = null;
            m_slots[ResultSlotIndex].Value = 0;
            m_slots[ResultSlotIndex].Count = 0;
        }

        SlotChanged(ResultSlotIndex);
    }

    /// <summary>
    /// 通知"这一格变了"。
    ///
    /// <c>OnSlotChange</c> 是**联机版独有的**（它要把改动同步给客户端），
    /// 插件版的 <c>ComponentInventoryBase</c> 根本没这个方法——直接调会编译不过。
    /// 单机不需要同步，所以插件版这里是空的。
    /// </summary>
    private void SlotChanged(int slotIndex)
    {
#if !STASH_SCMOD
        OnSlotChange(slotIndex);
#endif
    }

    /// <summary>
    /// <c>FindMatchingRecipe</c> 的**最后一个参数两个平台不一样**（实测各自的程序集）：
    /// 联机版收 <c>ComponentPlayer</c>，插件版收 <c>float playerLevel</c>。
    /// </summary>
    private CraftingRecipe? FindRecipe()
    {
        try
        {
#if STASH_SCMOD
            float level = Entity.FindComponent<ComponentPlayer>(throwOnError: false)?.PlayerData.Level ?? 1f;
            return CraftingRecipesManager.FindMatchingRecipe(m_subsystemTerrain, m_matchedIngredients, 0f, level);
#else
            ComponentPlayer? player = Entity.FindComponent<ComponentPlayer>(throwOnError: false);
            return CraftingRecipesManager.FindMatchingRecipe(m_subsystemTerrain, m_matchedIngredients, 0f, player);
#endif
        }
        catch (Exception exception)
        {
            // 配方匹配挂了最坏是合不出东西，不该把界面带崩。
            Log.Warning($"[Stash] 合成终端匹配配方失败：{exception.Message}");
            return null;
        }
    }
}
