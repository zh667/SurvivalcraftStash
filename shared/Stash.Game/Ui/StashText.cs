namespace Stash.Game;

/// <summary>
/// 界面文案。按钮位置很挤，一律用两个字以内的短标签。
/// 目前中英各一份，后续接 LanguageControl 再扩。
/// </summary>
public static class StashText
{
    public static bool UseEnglish { get; set; }

    public static string Sort => UseEnglish ? "Sort" : "整理";







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


    public static string UpgradeWrongTier =>
        UseEnglish ? "This upgrade does not fit that chest" : "这个升级件配不上这个箱子";

    public static string UpgradeNoRoom =>
        UseEnglish ? "Chest contents do not fit" : "箱子里的东西装不下";

    public static string UpgradeFailed =>
        UseEnglish ? "Upgrade failed" : "升级失败";

    public static string BackpackName(StashBackpackTier tier) => tier.Key switch
    {
        "copper" => UseEnglish ? "Copper Backpack" : "铜背包",
        "iron" => UseEnglish ? "Iron Backpack" : "铁背包",
        "diamond" => UseEnglish ? "Diamond Backpack" : "钻石背包",
        _ => UseEnglish ? "Backpack" : "背包",
    };

    public static string BackpackNotWorn =>
        UseEnglish ? "You are not wearing a backpack" : "你没有背着背包";

    public static string BackpackComponentMissing =>
        UseEnglish ? "Backpack storage is unavailable in this world" : "这个世界里背包存储没挂上（换个新世界或重装 Mod 试试）";

    public static string OpenBackpack => UseEnglish ? "Backpack" : "背包";



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

    public static string HubName => UseEnglish ? "Storage Terminal" : "存储终端";

    public static string TerminalStatus(int containers, int kinds, int page, int pages) => UseEnglish
        ? $"{containers} containers · {kinds} kinds · page {page}/{pages}"
        : $"{containers} 个容器 · {kinds} 种物品 · 第 {page}/{pages} 页";

    public static Func<int, string> TerminalTaken => count => UseEnglish ? $"Took {count}" : $"取出 {count} 个";

    public static Func<int, string> TerminalStored => count => UseEnglish ? $"Stored {count}" : $"存入 {count} 个";

    public static string TerminalNoRoom => UseEnglish ? "No room in your inventory" : "背包装不下了";

    public static string TerminalFull => UseEnglish ? "Nothing could be stored" : "没有能存进去的东西";

    public static string HubEmpty => UseEnglish ? "No containers connected" : "枢纽旁边没有连着容器";






    public static string Upgraded =>
        UseEnglish ? "Chest upgraded" : "箱子已升级";

}
