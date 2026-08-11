using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 背包的运行时逻辑：判断穿没穿、穿的哪一档、打开界面、降档时把多出来的东西退还。
/// </summary>
public static class StashBackpack
{
    /// <summary>玩家现在穿着的背包档位；没穿返回 null。</summary>
    public static StashBackpackTier? GetWornTier(ComponentPlayer player)
    {
        var clothing = player?.Entity.FindComponent<ComponentClothing>(throwOnError: false);
        if (clothing == null)
        {
            return null;
        }

        // 背包挂在躯干槽的最外层，和衣服共存（原版每个槽存的是一个列表）。
        foreach (int value in clothing.GetClothes(ClothingSlot.Torso))
        {
            if (Terrain.ExtractContents(value) != StashBackpackTiers.ClothingBlockIndex)
            {
                continue;
            }

            int clothingIndex = ClothingBlock.GetClothingIndex(Terrain.ExtractData(value));
            StashBackpackTier? tier = StashBackpackTiers.ByClothingIndex(clothingIndex);
            if (tier != null)
            {
                return tier;
            }
        }

        return null;
    }

    public static ComponentStashBackpack? GetInventory(ComponentPlayer player) =>
        player?.Entity.FindComponent<ComponentStashBackpack>(throwOnError: false);

    /// <summary>打开背包界面。没穿背包就提示一声。</summary>
    public static bool Open(ComponentPlayer player)
    {
        StashBackpackTier? tier = GetWornTier(player);
        ComponentStashBackpack? inventory = GetInventory(player);

        if (tier == null)
        {
            player?.ComponentGui.DisplaySmallMessage(StashText.BackpackNotWorn, Color.White, blinking: false, playNotificationSound: false);
            return false;
        }

        if (inventory == null)
        {
            // 组件没挂上（历史存档 / 数据库注入失败）。以前这里直接静默返回，
            // 表现就是"按 B 什么都不发生"，查起来毫无线索。
            player.ComponentGui.DisplaySmallMessage(
                StashText.BackpackComponentMissing, Color.White, blinking: false, playNotificationSound: false);
            Engine.Log.Warning("[Stash] 玩家实体上没有 ComponentStashBackpack，背包无法打开。");
            return false;
        }

        SpillBeyondTier(player, inventory, tier);

        player.ComponentGui.ModalPanelWidget = new StashContainerWidget(
            StashText.BackpackName(tier),
            inventory,
            firstSlot: 0,
            slots: tier.SlotsCount,
            columns: tier.Columns,
            slotSize: 60f,
            player.ComponentMiner.Inventory);   // 背包界面右侧就是物品栏，不再套一层切换
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        return true;
    }

    /// <summary>
    /// 换成更小的背包后，超出可用格数的东西要还给玩家，不能锁在看不见的格子里。
    /// 放不下就掉在脚下——丢东西比"东西消失了"好交代。
    /// </summary>
    public static void SpillBeyondTier(ComponentPlayer player, ComponentStashBackpack inventory, StashBackpackTier tier)
    {
        if (StashPlatform.IsReady && !StashPlatform.Current.IsAuthoritative)
        {
            return;
        }

        for (int slot = tier.SlotsCount; slot < inventory.SlotsCount; slot++)
        {
            int count = inventory.GetSlotCount(slot);
            if (count <= 0)
            {
                continue;
            }

            int value = inventory.GetSlotValue(slot);
            int removed = inventory.RemoveSlotItems(slot, count);
            if (removed <= 0)
            {
                continue;
            }

            int leftover = ComponentInventoryBase.AcquireItems(player.ComponentMiner.Inventory, value, removed);
            if (leftover > 0)
            {
                player.Project.FindSubsystem<SubsystemPickables>(throwOnError: true)
                    .AddPickable(value, leftover, player.ComponentBody.Position, Vector3.Zero, null);
            }
        }
    }
}
