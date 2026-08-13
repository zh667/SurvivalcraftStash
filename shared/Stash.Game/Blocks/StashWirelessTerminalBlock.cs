using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 无线终端（物品，不可放置）。
///
/// data 位存**绑定的存储终端编号**（0 = 未绑定）：
/// 拿着它右键一个存储终端方块就绑上，之后对着空处右键就能远程打开那一个终端。
/// 编号存在世界登记簿里（<see cref="StashHubNaming"/>），玩家可以给终端改名。
///
/// 渲染和绑定逻辑都在 <see cref="StashWirelessBlockBase"/>，这里只交代"长什么样、叫什么"。
/// </summary>
public class StashWirelessTerminalBlock : StashWirelessBlockBase
{
    // 取 710 而不是填补 703/705~708 那些空号：那几个是已删掉的观景箱和抽屉，
    // 玩家存档里可能还放着，复用索引会让旧方块直接变成无线终端。
    public static int Index = StashChestTiers.BaseIndex + 10;

    protected override int UnboundSlot => StashBlockTextures.WirelessUnbound;

    protected override int BoundSlot => StashBlockTextures.WirelessBound;

    protected override string UnboundName => StashText.WirelessTerminalUnbound;

    protected override string BoundName(string hubName) => StashText.WirelessTerminalBound(hubName);
}

/// <summary>
/// 无线合成终端（物品，不可放置）。
///
/// 和普通无线终端一样绑定、一样远程开存储终端，**多一个 3×3 合成格**：
/// 界面右下角就能合成，材料由"填材料"从**整个存储网络 + 物品栏 + 行囊**里凑。
///
/// 贴图是黄铜机身 + 绿屏（普通那台是钢灰 + 青屏），两台同时躺在物品栏里能一眼分清。
/// </summary>
public class StashWirelessCraftingTerminalBlock : StashWirelessBlockBase
{
    public static int Index = StashChestTiers.BaseIndex + 15;

    protected override int UnboundSlot => StashBlockTextures.CraftTerminalUnbound;

    protected override int BoundSlot => StashBlockTextures.CraftTerminalBound;

    protected override string UnboundName => StashText.CraftTerminalUnbound;

    protected override string BoundName(string hubName) => StashText.CraftTerminalBound(hubName);

    /// <summary>拿不到贴图时的退路色调，跟贴图的黄铜/绿屏对齐。</summary>
    protected override Color BoundTint => new(150, 225, 130);

    protected override Color UnboundTint => new(170, 140, 70);

    private const string IronIngotId = "ironingot";
    private const string CraftingTableId = "craftingtable";

    /// <summary>
    /// 合成这台机器时**保留原来那台无线终端的绑定**。
    ///
    /// 普通配方做不到：<c>CraftingRecipe.ResultValue</c> 是配方表里写死的一个整数，
    /// data 位固定，所以静态配方产出的永远是"未绑定"的。
    ///
    /// 原版留了一条口子——<c>Block.GetAdHocCraftingRecipe</c>：
    /// <c>CraftingRecipesManager.FindMatchingRecipe</c> 会**先**遍历所有方块问一遍这个方法，
    /// 谁返回的配方能匹配当前格子内容就用谁。原版给衣服染色、修工具走的就是这条
    /// （染色要保留衣服本身，和我们要保留绑定编号是同一类需求）。
    ///
    /// 这里照做：认出"七铁 + 工作台 + 无线终端"的摆法后，
    /// 从**格子里那台终端**的 data 读出绑定编号，原样搬到产物的 data 上。
    ///
    /// <c>.cr</c> 里那条静态配方**故意留着**：临时配方是枚举不出来的，
    /// 删了合成表和我们的配方浏览器里就再也找不到这台机器怎么做。
    /// 两条并存时临时配方优先（上面那个循环排在静态表之前），所以实际合成走的是这里。
    /// </summary>
#if STASH_SCMOD
    public override CraftingRecipe GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain, string[] ingredients, float heatLevel, float playerLevel)
#else
    public override CraftingRecipe GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain, string[] ingredients, float heatLevel, ComponentPlayer player)
#endif
    {
        // 这个方法会被**每个方块**在每次格子变动时问一遍，不匹配要尽快返回。
        if (heatLevel > 0f || ingredients.Length < 9)
        {
            return null!;
        }

        int hubId = MatchUpgradeLayout(ingredients);
        if (hubId < 0)
        {
            return null!;
        }

        // 匹配上了才打——这个方法每次格子变动会被**每个方块**问一遍，
        // 无条件打日志等于每放一个物品刷几百行。
        StashDiag.Log($"无线合成终端：临时配方命中，带出绑定编号 {hubId}"
            + $"（{(hubId == 0 ? "未绑定" : "有绑定")}）");

        return new CraftingRecipe
        {
            ResultValue = Terrain.MakeBlockValue(BlockIndex, 0, hubId),
            ResultCount = 1,
            Ingredients = BuildPattern(),
            Description = StashText.CraftTerminalRecipeDescription,
        };
    }

    /// <summary>
    /// 认这个摆法：正中一台无线终端，正上方一个工作台，其余七格铁锭。
    /// 认出来返回那台终端绑定的编号（0 = 未绑定），认不出返回 -1。
    /// </summary>
    private static int MatchUpgradeLayout(string[] ingredients)
    {
        string terminalId = CraftingIdOf(StashWirelessTerminalBlock.Index);
        if (string.IsNullOrEmpty(terminalId))
        {
            StashDiag.Once("craftterm-noid", "无线合成终端：查不到无线终端的 craftingId，临时配方永远不会命中");
            return -1;
        }

        // 正中放着一台无线终端 = 玩家**显然**在尝试这条配方。
        // 只有这种时候才值得解释"为什么没匹配上"——否则日志会被每次格子变动刷爆。
        bool attempting = !string.IsNullOrEmpty(ingredients[4])
            && ingredients[4].StartsWith(terminalId, StringComparison.Ordinal);

        int hubId = -1;

        for (int i = 0; i < 9; i++)
        {
            string cell = ingredients[i];
            if (string.IsNullOrEmpty(cell))
            {
                Explain(attempting, $"第 {i} 格是空的，这条配方九格必须摆满");
                return -1;
            }

            CraftingRecipesManager.DecodeIngredient(cell, out string craftingId, out int? data);

            // 下标是规范 3×3 的"列 + 行*3"：1 = 上中，4 = 正中。
            if (i == 4)
            {
                if (craftingId != terminalId)
                {
                    Explain(attempting, $"正中是 {craftingId}，需要 {terminalId}");
                    return -1;
                }

                hubId = data ?? 0;
            }
            else if (i == 1)
            {
                if (craftingId != CraftingTableId)
                {
                    Explain(attempting, $"上中是 {craftingId}，需要 {CraftingTableId}");
                    return -1;
                }
            }
            else if (craftingId != IronIngotId)
            {
                Explain(attempting, $"第 {i} 格是 {craftingId}，需要 {IronIngotId}");
                return -1;
            }
        }

        return hubId;
    }

    /// <summary>
    /// 解释"为什么这次没走临时配方"。
    ///
    /// 这条日志的价值在于：没命中时会**悄悄退回静态配方**，做出来的是未绑定的终端，
    /// 界面上没有任何报错，玩家只会觉得"绑定怎么又没了"。有了这行才知道差在哪一格。
    /// </summary>
    private static void Explain(bool attempting, string reason)
    {
        if (attempting)
        {
            StashDiag.Once($"craftterm-{reason}", $"无线合成终端：临时配方没匹配上——{reason}（会退回静态配方，产物不带绑定）");
        }
    }

    /// <summary>
    /// 交给 <c>MatchRecipe</c> 比对的样板。**不写 data** = 通配，
    /// 这样任何绑定状态的终端都能当材料
    /// （<c>CompareIngredients</c>：required 没写 data 就只比 craftingId）。
    /// </summary>
    private static string[] BuildPattern()
    {
        var pattern = new string[9];
        for (int i = 0; i < 9; i++)
        {
            pattern[i] = IronIngotId;
        }

        pattern[1] = CraftingTableId;
        pattern[4] = CraftingIdOf(StashWirelessTerminalBlock.Index);
        return pattern;
    }

    /// <summary>
    /// 按**运行时**索引取 craftingId。插件版会重排方块索引，但 craftingId 是稳定的。
    /// </summary>
    private static string CraftingIdOf(int blockIndex)
    {
        if (blockIndex <= 0 || blockIndex >= BlocksManager.Blocks.Length)
        {
            return string.Empty;
        }

        return BlocksManager.Blocks[blockIndex]?.CraftingId ?? string.Empty;
    }
}
