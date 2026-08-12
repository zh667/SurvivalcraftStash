using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 手里拿着背包右键 = **背上它，然后顺手打开**。
///
/// 为什么不是"右键直接打开手里这个背包"：SC 的物品只有一个 int，
/// 没有 NBT 之类的随身数据（见 docs/SC-PLATFORM.md 第 1 节），
/// **一个躺在物品栏里的背包物品不可能自己装着东西**。
/// 存储必须挂在玩家实体上，也就是"穿上的那个背包"才有内容。
///
/// 所以两条路是这样分工的，不会再互相打架（实机反馈"拿在手里右键说我没穿戴"）：
///   - 手里拿着 + 右键 → 背上（物品从物品栏移到躯干槽）并打开
///   - 已经背着 + B 键 → 打开
///
/// 挂在原版 <c>ClothingBlock</c>（索引 203）上，只认我们那三档背包的衣物索引，
/// 其它衣物一律放行给原版逻辑。两个平台的 <c>OnUse</c> 签名一致，这份可以共用。
/// </summary>
public class SubsystemStashBackpackBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => new[] { StashBackpackTiers.ClothingBlockIndex };

    public override void Load(ValuesDictionary valuesDictionary) => base.Load(valuesDictionary);

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        IInventory inventory = componentMiner.Inventory;
        ComponentPlayer? player = componentMiner.ComponentPlayer;
        if (inventory == null || player == null)
        {
            return false;
        }

        int slotIndex = inventory.ActiveSlotIndex;
        int held = inventory.GetSlotValue(slotIndex);
        if (Terrain.ExtractContents(held) != StashBackpackTiers.ClothingBlockIndex)
        {
            return false;
        }

        int clothingIndex = ClothingBlock.GetClothingIndex(Terrain.ExtractData(held));
        if (StashBackpackTiers.ByClothingIndex(clothingIndex) == null)
        {
            // 别人的衣服，不管。
            return false;
        }

        if (StashBackpack.GetWornTier(player) != null)
        {
            // 已经背着一个了。直接换的话得先把旧的还回物品栏、还要处理降档溢出，
            // 容易在联机下和服务端打架——让玩家自己去衣物界面脱，语义最清楚。
            player.ComponentGui.DisplaySmallMessage(
                StashText.BackpackAlreadyWorn, Color.White, blinking: false, playNotificationSound: false);
            StashBackpack.Open(player);
            return true;
        }

        Wear(player, inventory, slotIndex);
        return true;
    }

    /// <summary>
    /// 走原版的 <see cref="InventorySlotWidget.HandleDragDrop"/>，等价于玩家自己
    /// 把背包拖到衣物界面的躯干槽上。
    ///
    /// 好处是联机不用自己发包：这个静态方法内部会在 <c>WorkType.Client</c> 时
    /// 自己排一个 <c>ComponentInventoryPackage(HandleDragDrop)</c> 给服务端，
    /// 服务端改完 <c>ComponentClothing</c> 又会通过 <c>OnSlotChange</c> 同步回来。
    /// </summary>
    private static void Wear(ComponentPlayer player, IInventory inventory, int slotIndex)
    {
        var clothing = player.Entity.FindComponent<ComponentClothing>(throwOnError: false);
        if (clothing == null)
        {
            return;
        }

#if STASH_SCMOD
        // 插件版的 HandleDragDrop 是实例方法（而且没有联机分支），照抄它那几行就行——
        // 这正是原版把衣服拖到躯干槽时走的流程。
        int torso = (int)ClothingSlot.Torso;
        int value = inventory.GetSlotValue(slotIndex);
        bool worn = false;

        if (clothing.GetSlotProcessCapacity(torso, value) > 0)
        {
            int processCount = inventory.RemoveSlotItems(slotIndex, 1);
            clothing.ProcessSlotItems(
                torso, value, 1, processCount,
                out int processedValue, out int processedCount);

            // 没穿成的话原版会把东西塞回原格子，这里照做，别把玩家的背包吞了。
            if (processedValue != 0 && processedCount != 0)
            {
                inventory.AddSlotItems(
                    slotIndex, processedValue,
                    MathUtils.Min(inventory.GetSlotCapacity(slotIndex, processedValue), processedCount));
            }

            worn = true;
        }
#else
        bool worn = InventorySlotWidget.HandleDragDrop(
            inventory,
            slotIndex,
            DragMode.SingleItem,
            clothing,
            (int)ClothingSlot.Torso,
            processingOnly: false,
            player);
#endif

        StashBackpackTier? tier = StashBackpack.GetWornTier(player);
        if (!worn || tier == null)
        {
            // 最常见的原因：躯干上已经套了层数更高的衣服，原版 CanWearClothing 会拒绝。
            player.ComponentGui.DisplaySmallMessage(
                StashText.BackpackBlockedByClothes, Color.White, blinking: false, playNotificationSound: false);
            return;
        }

        player.ComponentGui.DisplaySmallMessage(
            StashText.BackpackWornNow(tier), Color.White, blinking: false, playNotificationSound: false);
        StashBackpack.Open(player);
    }
}
