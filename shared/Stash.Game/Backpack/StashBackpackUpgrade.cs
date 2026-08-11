using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 背包升级：和箱子一个机制——拿着升级件对着**穿在身上的背包**用一下就升一档，里面的东西不动。
///
/// 复用箱子那套升级件（同一个方块，data 表示链上位置），链条接在箱子后面：
/// 4 布→皮革、5 皮革→铁扣。这样玩家只要认一种"升级件"就行。
/// </summary>
public static class StashBackpackUpgrade
{
    /// <summary>背包升级件在升级链上的起始 data 值（0~3 是箱子的四级）。</summary>
    public const int FirstUpgradeData = 4;

    public static (StashBackpackTier From, StashBackpackTier To)? Resolve(int upgradeData)
    {
        int index = upgradeData - FirstUpgradeData;
        return index switch
        {
            0 => (StashBackpackTiers.Cloth, StashBackpackTiers.Leather),
            1 => (StashBackpackTiers.Leather, StashBackpackTiers.Iron),
            _ => null,
        };
    }

    public static bool IsBackpackUpgrade(int upgradeData) => Resolve(upgradeData) != null;

    /// <summary>
    /// 用升级件升级身上的背包。返回 true 表示这次使用被消费掉了（不管成没成）。
    /// </summary>
    public static bool TryUpgradeWorn(ComponentPlayer player, int upgradeData, out bool upgraded)
    {
        upgraded = false;

        (StashBackpackTier From, StashBackpackTier To)? step = Resolve(upgradeData);
        if (step == null || player == null)
        {
            return false;
        }

        var clothing = player.Entity.FindComponent<ComponentClothing>(throwOnError: false);
        if (clothing == null)
        {
            return false;
        }

        StashBackpackTier? worn = StashBackpack.GetWornTier(player);
        if (worn == null)
        {
            Notify(player, StashText.BackpackNotWorn);
            return true;
        }

        if (worn.ClothingIndex != step.Value.From.ClothingIndex)
        {
            Notify(player, StashText.UpgradeWrongTier);
            return true;
        }

        // 把躯干槽里那件旧背包换成新档位的，其它衣服原样保留。
        var clothes = new List<int>(clothing.GetClothes(ClothingSlot.Torso));
        for (int i = 0; i < clothes.Count; i++)
        {
            if (Terrain.ExtractContents(clothes[i]) != StashBackpackTiers.ClothingBlockIndex)
            {
                continue;
            }

            int data = Terrain.ExtractData(clothes[i]);
            if (ClothingBlock.GetClothingIndex(data) != worn.ClothingIndex)
            {
                continue;
            }

            int newData = ClothingBlock.SetClothingIndex(data, step.Value.To.ClothingIndex);
            clothes[i] = Terrain.MakeBlockValue(StashBackpackTiers.ClothingBlockIndex, 0, newData);
            clothing.SetClothes(ClothingSlot.Torso, clothes);

            upgraded = true;
            Notify(player, StashText.BackpackUpgraded(step.Value.To));
            return true;
        }

        Notify(player, StashText.UpgradeFailed);
        return true;
    }

    private static void Notify(ComponentPlayer player, string message) =>
        player.ComponentGui?.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
}
