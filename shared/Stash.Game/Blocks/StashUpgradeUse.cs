using Engine;
using Game;
using GameEntitySystem;

namespace Stash.Game;

/// <summary>
/// "拿着升级件点方块"的共享逻辑。两个平台的行为基类不同，各自的薄壳调这里。
///
/// 箱子和熔炉各有一种升级件（图标不同，见 <see cref="StashFurnaceUpgradeBlock"/> 的注释），
/// 但"检查手持 → 判权威端 → 执行升级 → 扣一个 → 飘提示"这套流程是一样的，
/// 只有中间那一步不同，所以用一个委托传进来。
/// </summary>
public static class StashUpgradeUse
{
    /// <summary>执行升级本身。返回 false 时通过 <c>out</c> 给出要显示的原因。</summary>
    public delegate bool UpgradeAction(int upgradeData, int x, int y, int z, out string failureReason);

    public static bool TryUseUpgradeItem(
        Project project,
        SubsystemTerrain terrain,
        SubsystemBlockEntities blockEntities,
        ComponentMiner miner,
        TerrainRaycastResult raycastResult) =>
        TryUse(miner, raycastResult, StashChestTiers.UpgradeItemIndex, StashText.ChestUpgraded,
            (int data, int x, int y, int z, out string reason) =>
                StashChestCore.TryUpgrade(project, terrain, blockEntities, x, y, z, data, out reason));

    public static bool TryUseFurnaceUpgradeItem(
        Project project,
        SubsystemTerrain terrain,
        SubsystemBlockEntities blockEntities,
        ComponentMiner miner,
        TerrainRaycastResult raycastResult) =>
        TryUse(miner, raycastResult, StashFurnaceTiers.UpgradeItemIndex, StashText.FurnaceUpgraded,
            (int data, int x, int y, int z, out string reason) =>
                StashFurnaceCore.TryUpgrade(project, terrain, blockEntities, x, y, z, data, out reason));

    /// <summary>
    /// 返回 true 表示这次交互被我们消费掉了（无论升级成功与否），调用方不应再走别的处理。
    /// </summary>
    /// <param name="successMessage">
    /// 成功时飘的那句。**必须按对象区分**——箱子和熔炉共用这段流程，
    /// 一度都写死成"箱子已升级"，给熔炉升级也这么说（实机反馈）。
    /// </param>
    private static bool TryUse(
        ComponentMiner miner,
        TerrainRaycastResult raycastResult,
        int upgradeItemIndex,
        string successMessage,
        UpgradeAction upgrade)
    {
        if (miner?.Inventory == null)
        {
            return false;
        }

        IInventory inventory = miner.Inventory;

        int activeSlot = inventory.ActiveSlotIndex;
        int heldValue = inventory.GetSlotValue(activeSlot);
        if (Terrain.ExtractContents(heldValue) != upgradeItemIndex)
        {
            return false;
        }

        // 同 OnInteract：非权威端只消费掉这次使用，真正的升级由服务端执行。
        if (StashPlatform.IsReady && !StashPlatform.Current.IsAuthoritative)
        {
            return true;
        }

        int upgradeData = Terrain.ExtractData(heldValue);

        bool upgraded = upgrade(
            upgradeData,
            raycastResult.CellFace.X,
            raycastResult.CellFace.Y,
            raycastResult.CellFace.Z,
            out string failureReason);

        if (upgraded)
        {
            inventory.RemoveSlotItems(activeSlot, 1);
            Notify(miner, successMessage);
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
