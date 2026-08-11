namespace Stash.Shared.Sorting;

public enum SortMethod
{
    /// <summary>类别 → 创造栏顺序 → 名称。默认，和玩家对创造栏的肌肉记忆一致。</summary>
    CategoryThenDisplayOrder,

    /// <summary>按显示名。</summary>
    Name,

    /// <summary>按方块索引 + data，最"稳定"但对玩家不直观。</summary>
    RawValue,

    /// <summary>数量从多到少。</summary>
    CountDescending,

    /// <summary>数量从少到多。</summary>
    CountAscending,
}
