using Engine;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 手里拿着背包右键 = 打开它。
///
/// 挂在原版 <c>ClothingBlock</c>（索引 203）上，只认我们那三档背包的衣物索引，
/// 其它衣物一律放行给原版逻辑。
///
/// 两个平台的 <c>SubsystemBlockBehavior.OnUse</c> 签名一致，这份可以共用。
/// </summary>
public class SubsystemStashBackpackBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => new[] { StashBackpackTiers.ClothingBlockIndex };

    public override void Load(ValuesDictionary valuesDictionary) => base.Load(valuesDictionary);

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        IInventory inventory = componentMiner.Inventory;
        if (inventory == null || componentMiner.ComponentPlayer == null)
        {
            return false;
        }

        int held = inventory.GetSlotValue(inventory.ActiveSlotIndex);
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

        // 非权威端消费掉就行：界面本来就是本地开的，内容由服务端同步过来。
        StashBackpack.Open(componentMiner.ComponentPlayer);
        return true;
    }
}
