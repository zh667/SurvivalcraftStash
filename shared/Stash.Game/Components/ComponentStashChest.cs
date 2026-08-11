using GameEntitySystem;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 分级箱子的库存组件。槽位数与每格容量倍数都来自实体模板参数，每个档位一份模板。
///
/// 刻意**不继承** <see cref="ComponentChest"/>：联机版原版流程会按 <c>is ComponentChest</c>
/// 去开写死 4×4 的原版界面，继承它会导致我们的大箱子只显示前 16 格。
/// </summary>
public class ComponentStashChest : ComponentInventoryBase
{
    /// <summary>每格容量 = 该物品的原版堆叠上限 × 这个倍数。木箱是 1，越高档越大。</summary>
    public int StackMultiplier { get; private set; } = 1;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        StackMultiplier = valuesDictionary.GetValue("StackMultiplier", 1);
    }

    public override int GetSlotCapacity(int slotIndex, int value)
    {
        int maxStacking = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value);

        // 不可堆叠的东西（工具、衣服）不放大：耐久存在 data 位里，两把用过的镐本来就是不同物品。
        if (maxStacking <= 1)
        {
            return maxStacking;
        }

        return maxStacking * StackMultiplier;
    }
}
