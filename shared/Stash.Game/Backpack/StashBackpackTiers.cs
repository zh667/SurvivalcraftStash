namespace Stash.Game;

/// <summary>
/// 背包档位。背包是**衣物**（原版 ClothingBlock，索引 203），不占方块索引，
/// 贴图也走衣物那套独立贴图，不受方块图集限制——联机版同样有得看。
///
/// 衣物索引取 100 起：原版只用到 37，而联机版的衣物索引只有 8 位（0~255），
/// 插件版是 10 位，取 100~102 两版都合法。
/// </summary>
public sealed record StashBackpackTier(int ClothingIndex, string Key, int SlotsCount, int Columns);

public static class StashBackpackTiers
{
    /// <summary>衣物所在的方块索引（原版 ClothingBlock）。</summary>
    public const int ClothingBlockIndex = 203;

    /// <summary>组件里实际开的槽位数，取最大档位，换档只改可见格数。</summary>
    public const int MaxSlots = 32;

    public static readonly StashBackpackTier Cloth = new(100, "cloth", SlotsCount: 16, Columns: 8);

    public static readonly StashBackpackTier Leather = new(101, "leather", SlotsCount: 24, Columns: 8);

    public static readonly StashBackpackTier Iron = new(102, "iron", SlotsCount: 32, Columns: 8);

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
