using Engine;

namespace Stash.Game;

/// <summary>
/// 分级箱子的档位表。
///
/// 材料链按 SC 实际存在的矿物定：原版**没有金**，能用的是铜（孔雀石冶炼）、铁、钻石。
/// 颜色是从原版图集实测出来的（见 docs/SC-PLATFORM.md 5.1），铜做了暖化以便和铁的灰区分开。
/// </summary>
public sealed record StashChestTier(
    Func<int> IndexProvider,
    string Key,
    int SlotsCount,
    int Columns,
    float SlotSize,
    Color Tint,
    string EntityTemplateName,
    int StackMultiplier)
{
    /// <summary>
    /// **运行时**的方块索引，每次现取，不缓存。
    ///
    /// 插件版（SCAPI）不认我们在 <c>public static int Index</c> 里写的值——它自己分配一个
    /// 空闲索引，再**写回**那个静态字段。实机日志里我们申请的 700/701/702 全是 AirBlock，
    /// 真实索引跑到了 300 一带（存储枢纽落在 304、无线终端 305）。
    /// 之前到处用编译期常量 700+，于是插件版下：分级箱子右键打不开、升级件没反应、
    /// 存储网络也认不出自家箱子。联机版正好不重写索引，所以一直没暴露。
    ///
    /// → **任何地方都不要再用常量比较方块索引，一律走这里。**
    /// </summary>
    public int Index => IndexProvider();

    public int Rows => SlotsCount / Columns;
}

public static class StashChestTiers
{
    /// <summary>
    /// 方块索引区间。原版占 0~263，这里挑 700 起的一段连续区，集中在一处方便和别的 Mod 错开。
    /// </summary>
    public const int BaseIndex = 700;

    // 下面这四个是**申请值**，只用来初始化各方块类的 `public static int Index` 字段。
    // 真正生效的索引要在运行时从那个字段读回来（插件版会改写它），见 StashChestTier.Index。
    public const int RequestedCopperChestIndex = BaseIndex + 0;
    public const int RequestedIronChestIndex = BaseIndex + 1;
    public const int RequestedDiamondChestIndex = BaseIndex + 2;
    public const int RequestedUpgradeItemIndex = BaseIndex + 4;

    /// <summary>升级件的运行时索引。</summary>
    public static int UpgradeItemIndex => StashChestUpgradeBlock.Index;

    /// <summary>木箱是原版箱子（索引 45），不属于我们的方块，只作为升级链的起点。</summary>
    public const int VanillaChestIndex = 45;

    // StackMultiplier：这一档箱子每格能装几倍于原版堆叠上限。
    // 比起单开一种抽屉方块，直接把格子容量做大更直白——想囤圆石就升箱子，不用记两套东西。
    public static readonly StashChestTier Copper = new(
        () => StashCopperChestBlock.Index, "copper", SlotsCount: 32, Columns: 8, SlotSize: 60f,
        new Color(200, 160, 96), "StashChestCopper", StackMultiplier: 2);

    public static readonly StashChestTier Iron = new(
        () => StashIronChestBlock.Index, "iron", SlotsCount: 48, Columns: 8, SlotSize: 60f,
        new Color(160, 160, 160), "StashChestIron", StackMultiplier: 4);

    public static readonly StashChestTier Diamond = new(
        () => StashDiamondChestBlock.Index, "diamond", SlotsCount: 80, Columns: 10, SlotSize: 48f,
        new Color(80, 160, 240), "StashChestDiamond", StackMultiplier: 16);


    public static readonly IReadOnlyList<StashChestTier> All = new[] { Copper, Iron, Diamond };

    /// <summary>
    /// 升级链：源方块 → 目标档位。升级件的 data 值就是这个链上的位置。
    /// 源方块也用委托取索引，理由同 <see cref="StashChestTier.Index"/>。
    /// </summary>
    public static readonly IReadOnlyList<(Func<int> From, StashChestTier To)> UpgradeChain = new[]
    {
        (new Func<int>(() => VanillaChestIndex), Copper),
        (new Func<int>(() => StashCopperChestBlock.Index), Iron),
        (new Func<int>(() => StashIronChestBlock.Index), Diamond),
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

        (Func<int> from, StashChestTier to) = UpgradeChain[upgradeData];
        return from() == fromBlockIndex ? to : null;
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
