namespace Stash.Game;

/// <summary>
/// 背包档位。背包是**衣物**（原版 ClothingBlock，索引 203），不占方块索引，
/// 贴图也走衣物那套独立贴图，不受方块图集限制——联机版同样有得看。
///
/// **衣物索引必须紧接原版最后一个（37）往下排，中间不能留空洞。**
/// 联机版 <c>ClothingBlock.m_clothingData</c> 是"按索引下标"的数组，
/// <c>GetCreativeValues()</c> 里 <c>OrderBy(cd =&gt; cd.DisplayIndex)</c> 在排序阶段就会读到空洞里的 null
/// （它只在循环体里判了空，排序时没判）→ 进世界时 ComponentCreativeInventory 直接 NRE。
/// 一开始取的是 100~102，结果 38~99 全是 null，任何世界都进不去。
/// </summary>
public sealed record StashBackpackTier(int ClothingIndex, string Key, int SlotsCount, int Columns);

public static class StashBackpackTiers
{
    /// <summary>衣物所在的方块索引（原版 ClothingBlock）。</summary>
    public const int ClothingBlockIndex = 203;

    /// <summary>组件里实际开的槽位数，取最大档位，换档只改可见格数。</summary>
    public const int MaxSlots = 32;

    public static readonly StashBackpackTier Cloth = new(38, "cloth", SlotsCount: 16, Columns: 8);

    public static readonly StashBackpackTier Leather = new(39, "leather", SlotsCount: 24, Columns: 8);

    public static readonly StashBackpackTier Iron = new(40, "iron", SlotsCount: 32, Columns: 8);

    public static readonly IReadOnlyList<StashBackpackTier> All = new[] { Cloth, Leather, Iron };

    public static StashBackpackTier? ByClothingIndex(int clothingIndex)
    {
        foreach (StashBackpackTier tier in All)
        {
            if (tier.ClothingIndex == clothingIndex)
            {
                return tier;
            }
        }

        return null;
    }
}
