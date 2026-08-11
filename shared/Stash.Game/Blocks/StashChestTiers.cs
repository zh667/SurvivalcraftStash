using Engine;

namespace Stash.Game;

/// <summary>
/// 分级箱子的档位表。
///
/// 材料链按 SC 实际存在的矿物定：原版**没有金**，能用的是铜（孔雀石冶炼）、铁、钻石。
/// 颜色是从原版图集实测出来的（见 docs/SC-PLATFORM.md 5.1），铜做了暖化以便和铁的灰区分开。
/// </summary>
public sealed record StashChestTier(
    int Index,
    string Key,
    int SlotsCount,
    int Columns,
    float SlotSize,
    Color Tint,
    string EntityTemplateName,
    int StackMultiplier)
{
    public int Rows => SlotsCount / Columns;
}

public static class StashChestTiers
{
    /// <summary>
    /// 方块索引区间。原版占 0~263，这里挑 700 起的一段连续区，集中在一处方便和别的 Mod 错开。
    /// </summary>
    public const int BaseIndex = 700;

    public const int CopperChestIndex = BaseIndex + 0;
    public const int IronChestIndex = BaseIndex + 1;
    public const int DiamondChestIndex = BaseIndex + 2;
    public const int UpgradeItemIndex = BaseIndex + 4;

    /// <summary>木箱是原版箱子（索引 45），不属于我们的方块，只作为升级链的起点。</summary>
    public const int VanillaChestIndex = 45;

    // StackMultiplier：这一档箱子每格能装几倍于原版堆叠上限。
    // 比起单开一种抽屉方块，直接把格子容量做大更直白——想囤圆石就升箱子，不用记两套东西。
    public static readonly StashChestTier Copper = new(
        CopperChestIndex, "copper", SlotsCount: 32, Columns: 8, SlotSize: 60f,
        new Color(200, 160, 96), "StashChestCopper", StackMultiplier: 2);

    public static readonly StashChestTier Iron = new(
        IronChestIndex, "iron", SlotsCount: 48, Columns: 8, SlotSize: 60f,
        new Color(160, 160, 160), "StashChestIron", StackMultiplier: 4);

    public static readonly StashChestTier Diamond = new(
        DiamondChestIndex, "diamond", SlotsCount: 80, Columns: 10, SlotSize: 48f,
        new Color(80, 160, 240), "StashChestDiamond", StackMultiplier: 16);


    public static readonly IReadOnlyList<StashChestTier> All = new[] { Copper, Iron, Diamond };

    /// <summary>升级链：源方块索引 → 目标档位。升级件的 data 值就是这个链上的位置。</summary>
    public static readonly IReadOnlyList<(int FromBlockIndex, StashChestTier To)> UpgradeChain = new[]
    {
        (VanillaChestIndex, Copper),
        (CopperChestIndex, Iron),
        (IronChestIndex, Diamond),
    };

    public static StashChestTier? ByBlockIndex(int blockIndex)
    {
        foreach (StashChestTier tier in All)
        {
            if (tier.Index == blockIndex)
            {
                return tier;
            }
        }

        return null;
    }

    public static bool IsStashChest(int blockIndex) => ByBlockIndex(blockIndex) != null;

    /// <summary>这块箱子（含原版木箱）能不能被第 <paramref name="upgradeData"/> 号升级件升级。</summary>
    public static StashChestTier? ResolveUpgrade(int fromBlockIndex, int upgradeData)
    {
        if (upgradeData < 0 || upgradeData >= UpgradeChain.Count)
        {
            return null;
        }

        (int from, StashChestTier to) = UpgradeChain[upgradeData];
        return from == fromBlockIndex ? to : null;
    }

    public static Color UpgradeTint(int upgradeData)
    {
        if (upgradeData < 0 || upgradeData >= UpgradeChain.Count)
        {
            return Color.White;
        }

        return UpgradeChain[upgradeData].To.Tint;
    }
}
