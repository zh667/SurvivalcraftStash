namespace Stash.Game;

/// <summary>
/// 背包档位，和箱子一样按铜/铁/钻石分级；升级靠合成表（背包 + 一圈材料 → 高一档背包），
/// 不用升级件——背包穿在身上，没法像箱子那样放在地上让你拿升级件去点。
///
/// 背包是**衣物**（原版 ClothingBlock，索引 203），不占方块索引，
/// 贴图也走衣物那套独立贴图，不受方块图集限制——联机版同样有得看。
///
/// ─────────────────────────────────────────────────────────────────────────
/// **衣物索引是全局写死的，没有分配机制，两个平台能选的范围还不一样。**
///
/// 实机反馈：和「工业时代2」(SCIE) 同装时，三档行囊和它的钢头盔/钢胸甲/钢护腿
/// 撞在同一个索引上——物品显示我们的名字、套着它的模型，配方表里两条配方混在一起。
/// SCIE 占 38~42 和 50。
///
/// **联机版只能是 38/39/40。** <c>m_clothingData</c> 是"按索引下标"的
/// <c>DynamicArray</c>，而 <c>GetCreativeValues()</c> 里
/// <c>OrderBy(cd =&gt; cd.DisplayIndex)</c> **在判空之前就解引用**
/// （空判只写在循环体里）→ 中间留一个空号，进世界直接 NRE。
/// 一开始取的是 100~102，结果 38~99 全是 null，任何世界都进不去。
/// 所以这个平台没法躲，只能紧接原版最后一个（37）连续排。
///
/// **插件版搬到 160/161/162。** 那边 <c>m_clothingData</c> 是
/// <c>Dictionary&lt;int, ClothingData&gt;</c>，留空号无所谓——SCIE 自己就是 42 之后跳到 50。
/// 高位远离"大家都紧挨着原版往下排"的混战区。
///
/// 索引只有 8 位（<c>GetClothingIndex(data) = data &amp; 0xFF</c>），上限 255。
/// 改这里必须同步改对应平台的 <c>Assets/StashClothes.clo</c> 和 <c>Assets/StashBackpacks.cr</c>。
/// 详见 docs/SC-PLATFORM.md 5.28。
/// ─────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed record StashBackpackTier(int ClothingIndex, string Key, int SlotsCount, int Columns);

public static class StashBackpackTiers
{
    /// <summary>衣物所在的方块索引（原版 ClothingBlock）。</summary>
    public const int ClothingBlockIndex = 203;

    /// <summary>组件里实际开的槽位数，取最大档位，换档只改可见格数。</summary>
    public const int MaxSlots = 32;

    /// <summary>
    /// 三档行囊的起始衣物索引。两个平台不一样，理由见类注释。
    /// **改这里必须同步改该平台 Assets 下的 StashClothes.clo 和 StashBackpacks.cr。**
    /// </summary>
#if STASH_SCMOD
    public const int FirstClothingIndex = 160;
#else
    public const int FirstClothingIndex = 38;
#endif

    public static readonly StashBackpackTier Copper =
        new(FirstClothingIndex, "copper", SlotsCount: 16, Columns: 8);

    public static readonly StashBackpackTier Iron =
        new(FirstClothingIndex + 1, "iron", SlotsCount: 24, Columns: 8);

    public static readonly StashBackpackTier Diamond =
        new(FirstClothingIndex + 2, "diamond", SlotsCount: 32, Columns: 8);

    public static readonly IReadOnlyList<StashBackpackTier> All = new[] { Copper, Iron, Diamond };

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
