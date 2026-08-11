namespace Stash.Game;

/// <summary>
/// 界面文案。按钮位置很挤，一律用两个字以内的短标签。
/// 目前中英各一份，后续接 LanguageControl 再扩。
/// </summary>
public static class StashText
{
    public static bool UseEnglish { get; set; }

    public static string Sort => UseEnglish ? "Sort" : "整理";

    public static string SortBackpack => UseEnglish ? "SortInv" : "整背包";

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

    public static string Unlocked => UseEnglish ? "Slot unlocked" : "已解锁该格";
}
