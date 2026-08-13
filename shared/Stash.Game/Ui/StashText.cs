namespace Stash.Game;

/// <summary>
/// 界面文案。按钮位置很挤，一律用两个字以内的短标签。
/// 目前中英各一份，后续接 LanguageControl 再扩。
/// </summary>
public static class StashText
{
    public static bool UseEnglish { get; set; }

    public static string Sort => UseEnglish ? "Sort" : "整理";

    /// <summary>一个界面里可能有两块库存（箱子 + 物品栏），按钮得说清楚整理的是哪一边。</summary>
    public static string SortInventory => UseEnglish ? "Sort Inventory" : "整理物品栏";

    public static string SortChest => UseEnglish ? "Sort Chest" : "整理箱子";

    public static string SortBackpack => UseEnglish ? "Sort Backpack" : "整理行囊";







    public static string Nothing => UseEnglish ? "Nothing to do" : "没有可整理的";



    public static string Sorted(int slots) => UseEnglish ? $"Sorted {slots} slots" : $"整理了 {slots} 格";





    public static string PlayerInventory => UseEnglish ? "Inventory" : "物品栏";

    public static string ChestName(StashChestTier tier) => tier.Key switch
    {
        "copper" => UseEnglish ? "Copper Chest" : "铜箱",
        "iron" => UseEnglish ? "Iron Chest" : "铁箱",
        "diamond" => UseEnglish ? "Diamond Chest" : "钻石箱",
        _ => UseEnglish ? "Chest" : "箱子",
    };

    public static string UpgradeName(int upgradeData) => upgradeData switch
    {
        0 => UseEnglish ? "Copper Chest Upgrade" : "铜箱升级件",
        1 => UseEnglish ? "Iron Chest Upgrade" : "铁箱升级件",
        2 => UseEnglish ? "Diamond Chest Upgrade" : "钻石箱升级件",
        _ => UseEnglish ? "Upgrade Kit" : "升级件",
    };


    public static string FurnaceName(StashFurnaceTier tier) => tier.Key switch
    {
        "copper" => UseEnglish ? "Copper Furnace" : "铜熔炉",
        "iron" => UseEnglish ? "Iron Furnace" : "铁熔炉",
        "diamond" => UseEnglish ? "Diamond Furnace" : "钻石熔炉",
        _ => UseEnglish ? "Furnace" : "熔炉",
    };

    public static string FurnaceUpgradeName(int upgradeData) => upgradeData switch
    {
        0 => UseEnglish ? "Copper Furnace Upgrade" : "铜熔炉升级件",
        1 => UseEnglish ? "Iron Furnace Upgrade" : "铁熔炉升级件",
        2 => UseEnglish ? "Diamond Furnace Upgrade" : "钻石熔炉升级件",
        _ => UseEnglish ? "Furnace Upgrade Kit" : "熔炉升级件",
    };

    /// <summary>开炉子时飘一句，告诉玩家这一档快多少。倍率不写死，跟着档位表走。</summary>
    public static string FurnaceOpened(StashFurnaceTier tier) => UseEnglish
        ? $"{FurnaceName(tier)} · {tier.SpeedMultiplier:0.#}x speed"
        : $"{FurnaceName(tier)} · {tier.SpeedMultiplier:0.#} 倍速度";

    public static string FurnaceUpgradeWrongTier =>
        UseEnglish ? "This upgrade does not fit that furnace" : "这个升级件配不上这座熔炉";

    public static string UpgradeWrongTier =>
        UseEnglish ? "This upgrade does not fit that chest" : "这个升级件配不上这个箱子";

    public static string UpgradeNoRoom =>
        UseEnglish ? "Chest contents do not fit" : "箱子里的东西装不下";

    public static string UpgradeFailed =>
        UseEnglish ? "Upgrade failed" : "升级失败";

    public static string BackpackName(StashBackpackTier tier) => tier.Key switch
    {
        "copper" => UseEnglish ? "Copper Pack" : "铜行囊",
        "iron" => UseEnglish ? "Iron Pack" : "铁行囊",
        "diamond" => UseEnglish ? "Diamond Pack" : "钻石行囊",
        _ => UseEnglish ? "Pack" : "行囊",
    };

    public static string BackpackNotWorn =>
        UseEnglish ? "You are not wearing a pack" : "你没有背着行囊（拿在手里右键就能背上）";

    public static string BackpackWornNow(StashBackpackTier tier) =>
        UseEnglish ? $"{BackpackName(tier)} equipped" : $"背上了{BackpackName(tier)}";

    /// <summary>背包是躯干最外层，外面还套着别的衣服时穿不上（原版 CanWearClothing 按层数比较）。</summary>
    public static string BackpackBlockedByClothes =>
        UseEnglish
            ? "Take off your outer torso clothing first — the pack goes on top"
            : "行囊要背在最外层，先把身上更外层的衣服脱掉";

    public static string BackpackAlreadyWorn =>
        UseEnglish ? "You are already wearing a pack" : "你已经背着一个行囊了（先脱下来再换）";

    public static string BackpackComponentMissing =>
        UseEnglish ? "Pack storage is unavailable in this world" : "这个世界里行囊存储没挂上（换个新世界或重装 Mod 试试）";

    /// <summary>
    /// **不要叫"背包"**：原版把玩家物品栏那块网格也标成"背包"（看衣物界面的标题），
    /// 我们的按钮再叫背包，玩家分不清点了会切到哪儿。用"行囊"。
    /// </summary>
    public static string OpenBackpack => UseEnglish ? "Pack" : "行囊";

    /// <summary>原版界面那块网格只有 16 格，装不下整只背包，所以要分页。</summary>
    public static string BackpackPage(int page, int pages) =>
        UseEnglish ? $"Pack {page}/{pages}" : $"行囊 {page}/{pages}";



    /// <summary>大数缩写：终端里一格可能有十几万个，原样显示会撑破格子。</summary>
    public static string FormatCount(long count)
    {
        if (count < 10_000)
        {
            return count.ToString();
        }

        return count < 1_000_000
            ? (count / 1000f).ToString("0.#") + "k"
            : (count / 1_000_000f).ToString("0.##") + "M";
    }

    public static string Search => UseEnglish ? "Search" : "搜索";

    /// <summary>输入框空着的时候显示在框里的灰字，告诉玩家这儿能点、能打字。</summary>
    public static string SearchHint => UseEnglish ? "click here to type…" : "点这里输入物品名…";

    /// <summary>输入框有焦点时挂在状态栏的提示：怎么退出去。</summary>
    public static string SearchExit => UseEnglish
        ? "Enter = keep filter · Esc = clear · or just click elsewhere"
        : "回车＝保留筛选 · Esc＝清空退出 · 点界面别处也能退出";

    public static string DefaultHubName(int id) => UseEnglish ? $"Storage Terminal {id}" : $"存储终端{id}";

    public static string WirelessTerminalUnbound =>
        UseEnglish ? "Wireless Terminal (unbound)" : "无线终端（未绑定）";

    public static string WirelessTerminalBound(string hubName) =>
        UseEnglish ? $"Wireless Terminal → {hubName}" : $"{hubName}的远程终端";

    public static string WirelessBoundTo(string hubName) =>
        UseEnglish ? $"Bound to {hubName}" : $"已绑定到{hubName}";

    public static string WirelessNotBound =>
        UseEnglish ? "Bind it by using it on a storage terminal first" : "先拿它右键一个存储终端来绑定";

    public static string WirelessHubGone =>
        UseEnglish ? "That storage terminal is gone" : "绑定的存储终端已经不在了";

    public static string CraftTerminalUnbound =>
        UseEnglish ? "Wireless Crafting Terminal (unbound)" : "无线合成终端（未绑定）";

    public static string CraftTerminalBound(string hubName) =>
        UseEnglish ? $"Wireless Crafting Terminal → {hubName}" : $"{hubName}的远程合成终端";

    /// <summary>合成无线合成终端时的说明：绑定会被带过来。</summary>
    public static string CraftTerminalRecipeDescription => UseEnglish
        ? "Keeps the binding of the terminal you put in"
        : "沿用放进去那台终端的绑定";

    /// <summary>终端里那块合成格的小标题。</summary>
    public static string CraftingGrid => UseEnglish ? "Crafting" : "合成";

    /// <summary>残留格里的淡色字（水桶合面团会退回空桶那种）。原版工作台也这么标。</summary>
    public static string CraftRemains => UseEnglish ? "left" : "剩余";

    public static string HubName => UseEnglish ? "Storage Terminal" : "存储终端";

    public static string TerminalStatus(int containers, int kinds, int page, int pages) => UseEnglish
        ? $"{containers} containers · {kinds} kinds · page {page}/{pages}"
        : $"{containers} 个容器 · {kinds} 种物品 · 第 {page}/{pages} 页";

    public static Func<int, string> TerminalTaken => count => UseEnglish ? $"Took {count}" : $"取出 {count} 个";

    public static Func<int, string> TerminalStored => count => UseEnglish ? $"Stored {count}" : $"存入 {count} 个";

    public static string TerminalNoRoom => UseEnglish ? "No room in your inventory" : "物品栏装不下了";

    public static string TerminalFull => UseEnglish ? "Nothing could be stored" : "没有能存进去的东西";

    public static string HubEmpty => UseEnglish ? "No containers connected" : "枢纽旁边没有连着容器";






    // ───────────────────────────── 配方浏览器 ─────────────────────────────

    public static string RecipeBrowser => UseEnglish ? "Recipes" : "配方";

    public static string BrowserTitle => UseEnglish ? "Recipe Browser" : "配方浏览";

    public static string Bookmarks => UseEnglish ? "Bookmarks" : "收藏";

    public static string Close => UseEnglish ? "Close" : "关闭";

    public static string AllItems => UseEnglish ? "All items" : "全部物品";

    public static string ShowRecipes => UseEnglish ? "Recipe" : "合成配方";

    public static string AddBookmark => UseEnglish ? "Bookmark" : "收藏";

    public static string RemoveBookmark => UseEnglish ? "Bookmarked" : "已收藏";

    /// <summary>
    /// **不要用 ◀ ▶**：SC 的位图字体里没有这两个字形，实机会画成两个方块
    /// （实机反馈"怎么还有两个按钮里面是方块"）。▲▼ 是有的，翻页仍然用那两个。
    /// </summary>
    public static string PrevRecipe => UseEnglish ? "Prev" : "上一个";

    public static string NextRecipe => UseEnglish ? "Next" : "下一个";

    /// <summary>把配方需要的材料从物品栏和行囊搬进合成格。</summary>
    public static string Fill => UseEnglish ? "Fill" : "填材料";

    /// <summary>顺着材料钻进去之后往回退一层。</summary>
    public static string Back => UseEnglish ? "Back" : "返回";

    /// <summary>钻了不止一层时把深度写出来，玩家才知道还要按几次。</summary>
    public static string BackDepth(int depth) => UseEnglish ? $"Back {depth}" : $"返回 {depth}";

    public static string NoRecipe => UseEnglish ? "No recipe" : "没有合成配方";

    /// <summary>冶炼配方的标记，配着成品下面那簇火一起显示。</summary>
    public static string NeedsFurnace => UseEnglish ? "in a furnace" : "需要熔炉";

    /// <summary>缺料时的一行提示；具体缺哪几格由红底标出来。</summary>
    public static string SomeIngredientsMissing =>
        UseEnglish ? "red = you don't have it" : "红底的材料你没有";

    public static string PickAnItem => UseEnglish ? "Pick an item" : "点一个物品看配方";

    public static string RecipeCounter(int index, int total) =>
        UseEnglish ? $"{index}/{total}" : $"{index}/{total}";

    public static string BrowserStatus(int shown, int total, int page, int pages) => UseEnglish
        ? $"{shown}/{total} items · page {page}/{pages}"
        : $"{shown}/{total} 个物品 · 第 {page}/{pages} 页";

    /// <summary>格子装不下这个配方。要说清楚差在哪，否则玩家会以为是缺材料。</summary>
    public static string FillRecipeTooLarge(int width, int height, int columns, int rows) => UseEnglish
        ? $"Recipe is {width}x{height}, this grid is only {columns}x{rows} — use a crafting table"
        : $"配方是 {width}×{height} 的，这里只有 {columns}×{rows} 格——去工作台上做";

    /// <summary>拿合成配方去点熔炉的"填材料"。</summary>
    public static string FillNeedsCraftingGrid =>
        UseEnglish ? "That is a crafting recipe — not a furnace one" : "这是合成配方，熔炉做不了";

    public static string FillNeedsFurnace =>
        UseEnglish ? "That is a smelting recipe — use a furnace" : "这是冶炼配方，要用熔炉";

    public static string FillNoRoomToClear =>
        UseEnglish ? "No room to clear the crafting grid first" : "合成格里的东西退不回物品栏，先腾个位置";

    public static string FillMissing(string what) =>
        UseEnglish ? $"Missing: {what}" : $"缺：{what}";

    public static string FillFailed => UseEnglish ? "Could not fill" : "没能填进去";

    public static string Filled(int sets) => UseEnglish ? $"Filled {sets}x" : $"填了 {sets} 份";

    /// <summary>创造模式的物品栏不能当取料来源，理由见 StashCraftFill。</summary>
    public static string FillNoUsableSource =>
        UseEnglish ? "No inventory to pull from (creative mode is skipped)" : "没有可取料的库存（创造模式的物品栏不算）";

    /// <summary>联机客户端只是把计划发出去了，格子要等服务端回包才动。</summary>
    public static string FillSent =>
        UseEnglish ? "Sent to server…" : "已请求服务端填料…";

    public static string ChestUpgraded =>
        UseEnglish ? "Chest upgraded" : "箱子已升级";

    public static string FurnaceUpgraded =>
        UseEnglish ? "Furnace upgraded" : "熔炉已升级";

}
