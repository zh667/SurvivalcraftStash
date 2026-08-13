namespace Stash.Game;

/// <summary>
/// 分级熔炉的档位表。结构和 <see cref="StashChestTier"/> 一一对应，故意保持同样的形状——
/// 索引全部走 <c>Func&lt;int&gt;</c> 现取（插件版 SCAPI 会重排方块索引再写回静态字段，
/// 详见 <see cref="StashChestTier.Index"/> 上的注释）。
///
/// **这一档只做"烧得更快"**：格子数、燃料效率、并行路数一律不动，
/// 熔炉界面直接复用原版 <c>FurnaceWidget</c>。
/// </summary>
public sealed record StashFurnaceTier(
    Func<int> IndexProvider,
    string Key,
    float SpeedMultiplier,
    string EntityTemplateName,
    int TextureFirstSlot)
{
    public int Index => IndexProvider();

    public int FrontSlot => TextureFirstSlot;

    public int SideSlot => TextureFirstSlot + 1;

    public int TopSlot => TextureFirstSlot + 2;
}

public static class StashFurnaceTiers
{
    /// <summary>方块索引申请值。接着箱子那段（700~710）往后排。</summary>
    public const int RequestedCopperFurnaceIndex = StashChestTiers.BaseIndex + 11;
    public const int RequestedIronFurnaceIndex = StashChestTiers.BaseIndex + 12;
    public const int RequestedDiamondFurnaceIndex = StashChestTiers.BaseIndex + 13;
    public const int RequestedFurnaceUpgradeIndex = StashChestTiers.BaseIndex + 14;

    /// <summary>原版熔炉（FurnaceBlock.Index = 64），升级链的起点，不是我们的方块。</summary>
    public const int VanillaFurnaceIndex = 64;

    /// <summary>熔炉升级件的运行时索引。</summary>
    public static int UpgradeItemIndex => StashFurnaceUpgradeBlock.Index;

    // SpeedMultiplier：原版每秒推进 0.15 的冶炼进度，这里整体按倍数缩放时间。
    // 燃料也按同样倍数消耗，所以**每件东西耗的燃料不变**，只是等得更短——
    // 这正是"等级越高烧的越快"想要的效果，不会顺带变成燃料作弊。
    public static readonly StashFurnaceTier Copper = new(
        () => StashCopperFurnaceBlock.Index, "copper", SpeedMultiplier: 2f,
        "StashFurnaceCopper", StashBlockTextures.FurnaceFirst);

    public static readonly StashFurnaceTier Iron = new(
        () => StashIronFurnaceBlock.Index, "iron", SpeedMultiplier: 4f,
        "StashFurnaceIron", StashBlockTextures.FurnaceFirst + 3);

    public static readonly StashFurnaceTier Diamond = new(
        () => StashDiamondFurnaceBlock.Index, "diamond", SpeedMultiplier: 8f,
        "StashFurnaceDiamond", StashBlockTextures.FurnaceFirst + 6);

    public static readonly IReadOnlyList<StashFurnaceTier> All = new[] { Copper, Iron, Diamond };

    /// <summary>升级链：源方块 → 目标档位。升级件的 data 值就是链上的下标。</summary>
    public static readonly IReadOnlyList<(Func<int> From, StashFurnaceTier To)> UpgradeChain = new[]
    {
        (new Func<int>(() => VanillaFurnaceIndex), Copper),
        (new Func<int>(() => StashCopperFurnaceBlock.Index), Iron),
        (new Func<int>(() => StashIronFurnaceBlock.Index), Diamond),
    };

    public static StashFurnaceTier? ByBlockIndex(int blockIndex)
    {
        foreach (StashFurnaceTier tier in All)
        {
            if (tier.Index == blockIndex)
            {
                return tier;
            }
        }

        return null;
    }

    public static bool IsStashFurnace(int blockIndex) => ByBlockIndex(blockIndex) != null;

    /// <summary>这座熔炉（含原版熔炉）能不能被第 <paramref name="upgradeData"/> 号升级件升级。</summary>
    public static StashFurnaceTier? ResolveUpgrade(int fromBlockIndex, int upgradeData)
    {
        if (upgradeData < 0 || upgradeData >= UpgradeChain.Count)
        {
            return null;
        }

        (Func<int> from, StashFurnaceTier to) = UpgradeChain[upgradeData];
        return from() == fromBlockIndex ? to : null;
    }
}
