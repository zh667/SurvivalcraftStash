using Engine;

namespace Stash.Game;

/// <summary>
/// 抽屉档位。容量单位是"组"——1 组 = 该物品自己的原版堆叠上限（圆石 40/组）。
/// 不设无穷的理由见 docs/DESIGN.md P5。
/// </summary>
public sealed record StashDrawerTier(
    int Index,
    string Key,
    int CapacityInStacks,
    Color Tint,
    string EntityTemplateName);

public static class StashDrawerTiers
{
    public const int WoodDrawerIndex = StashChestTiers.BaseIndex + 5;
    public const int CopperDrawerIndex = StashChestTiers.BaseIndex + 6;
    public const int IronDrawerIndex = StashChestTiers.BaseIndex + 7;
    public const int DiamondDrawerIndex = StashChestTiers.BaseIndex + 8;

    public static readonly StashDrawerTier Wood = new(
        WoodDrawerIndex, "wood", 64, new Color(128, 96, 48), "StashDrawerWood");

    public static readonly StashDrawerTier Copper = new(
        CopperDrawerIndex, "copper", 256, new Color(200, 160, 96), "StashDrawerCopper");

    public static readonly StashDrawerTier Iron = new(
        IronDrawerIndex, "iron", 1024, new Color(160, 160, 160), "StashDrawerIron");

    public static readonly StashDrawerTier Diamond = new(
        DiamondDrawerIndex, "diamond", 4096, new Color(80, 160, 240), "StashDrawerDiamond");

    public static readonly IReadOnlyList<StashDrawerTier> All = new[] { Wood, Copper, Iron, Diamond };

    public static StashDrawerTier? ByBlockIndex(int blockIndex)
    {
        foreach (StashDrawerTier tier in All)
        {
            if (tier.Index == blockIndex)
            {
                return tier;
            }
        }

        return null;
    }

    public static bool IsDrawer(int blockIndex) => ByBlockIndex(blockIndex) != null;
}
