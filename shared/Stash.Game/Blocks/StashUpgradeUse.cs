using Engine;
using Game;
using GameEntitySystem;

namespace Stash.Game;

/// <summary>
/// "拿着升级件点箱子"的共享逻辑。两个平台的行为基类不同，各自的薄壳调这里。
/// </summary>
public static class StashUpgradeUse
{
    /// <summary>
    /// 返回 true 表示这次交互被我们消费掉了（无论升级成功与否），调用方不应再走别的处理。
    /// </summary>
    public static bool TryUseUpgradeItem(
        Project project,
        SubsystemTerrain terrain,
        SubsystemBlockEntities blockEntities,
        ComponentMiner miner,
        TerrainRaycastResult raycastResult)
    {
        if (miner?.Inventory == null)
        {
            return false;
        }

        IInventory inventory = miner.Inventory;

        int activeSlot = inventory.ActiveSlotIndex;
        int heldValue = inventory.GetSlotValue(activeSlot);
        if (Terrain.ExtractContents(heldValue) != StashChestTiers.UpgradeItemIndex)
        {
            return false;
        }

        // 同 OnInteract：非权威端只消费掉这次使用，真正的升级由服务端执行。
        if (StashPlatform.IsReady && !StashPlatform.Current.IsAuthoritative)
        {
            return true;
        }

        int upgradeData = Terrain.ExtractData(heldValue);

        int x = raycastResult.CellFace.X;
        int y = raycastResult.CellFace.Y;
        int z = raycastResult.CellFace.Z;

        bool upgraded = StashChestCore.TryUpgrade(
            project, terrain, blockEntities, x, y, z, upgradeData, out string failureReason);

        if (upgraded)
        {
            inventory.RemoveSlotItems(activeSlot, 1);
            Notify(miner, StashText.Upgraded);
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }
        else
        {
            Notify(miner, failureReason);
        }

        return true;
    }

    private static void Notify(ComponentMiner miner, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        miner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
    }
}
