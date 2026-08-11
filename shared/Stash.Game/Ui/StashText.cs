namespace Stash.Game;

/// <summary>
/// 界面文案。按钮位置很挤，一律用两个字以内的短标签。
/// 目前中英各一份，后续接 LanguageControl 再扩。
/// </summary>
public static class StashText
{
    public static bool UseEnglish { get; set; }

    public static string Sort => UseEnglish ? "Sort" : "整理";


    public static string Deposit => UseEnglish ? "Put" : "存入";

    public static string DepositAll => UseEnglish ? "Put*" : "全存";

    public static string Restock => UseEnglish ? "Take" : "取出";

    public static string Lock => UseEnglish ? "Lock" : "锁定";

    public static string Undo => UseEnglish ? "Undo" : "撤销";

    public static string Nothing => UseEnglish ? "Nothing to do" : "没有可整理的";

    public static string NothingMoved => UseEnglish ? "Nothing moved" : "没有可搬运的";

    public static string Moved(int count) => UseEnglish ? $"Moved {count}" : $"搬运 {count} 个";

    public static string Sorted(int slots) => UseEnglish ? $"Sorted {slots} slots" : $"整理了 {slots} 格";

    public static string Undone => UseEnglish ? "Undone" : "已撤销";

    public static string NothingToUndo => UseEnglish ? "Nothing to undo" : "没有可撤销的操作";

    public static string Locked => UseEnglish ? "Slot locked" : "已锁定该格";

    public static string Unlocked2 => Unlocked;

    public static string PlayerInventory => UseEnglish ? "Inventory" : "背包";

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
        4 => UseEnglish ? "Leather Backpack Upgrade" : "皮革背包升级件",
        5 => UseEnglish ? "Ironbound Backpack Upgrade" : "铁扣背包升级件",
        _ => UseEnglish ? "Upgrade Kit" : "升级件",
    };

    public static string BackpackUpgraded(StashBackpackTier tier) =>
        UseEnglish ? $"Upgraded to {BackpackName(tier)}" : $"背包升级为{BackpackName(tier)}";

    public static string UpgradeWrongTier =>
        UseEnglish ? "This upgrade does not fit that chest" : "这个升级件配不上这个箱子";

    public static string UpgradeNoRoom =>
        UseEnglish ? "Chest contents do not fit" : "箱子里的东西装不下";

    public static string UpgradeFailed =>
        UseEnglish ? "Upgrade failed" : "升级失败";

    public static string BackpackName(StashBackpackTier tier) => tier.Key switch
    {
        "cloth" => UseEnglish ? "Cloth Backpack" : "布背包",
        "leather" => UseEnglish ? "Leather Backpack" : "皮革背包",
        "iron" => UseEnglish ? "Ironbound Backpack" : "铁扣背包",
        _ => UseEnglish ? "Backpack" : "背包",
    };

    public static string BackpackNotWorn =>
        UseEnglish ? "You are not wearing a backpack" : "你没有背着背包";

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

    public static string ClearSearch => UseEnglish ? "Clear" : "清除";

    public static string SearchHint => UseEnglish ? "(all items)" : "（全部物品）";

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

    public static string Unlocked => UseEnglish ? "Slot unlocked" : "已解锁该格";
}
