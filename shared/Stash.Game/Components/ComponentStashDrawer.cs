using GameEntitySystem;
using Stash.Game;
using TemplatesDatabase;

namespace Game;

/// <summary>
/// 抽屉的库存组件：**一格**，但这一格能装很多。
///
/// 实现方式就是覆写 <c>GetSlotCapacity</c>——原版的插入/取出、联机同步、
/// 甚至我们自己的整理与一键存取，全都是按这个容量算的，不需要额外改任何东西。
///
/// 只收可堆叠物品（<c>MaxStacking &gt; 1</c>）：工具和衣服在原版就是一格一个，
/// 而且耐久值存在 data 位里，两把用过的镐是不同的 value，塞进来也合并不了。
/// </summary>
public class ComponentStashDrawer : ComponentInventoryBase
{
    /// <summary>容量倍数（多少"组"），由实体模板参数给出，对应抽屉档位。</summary>
    public int CapacityInStacks { get; private set; } = 64;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        CapacityInStacks = valuesDictionary.GetValue("CapacityInStacks", 64);
    }

    public override int GetSlotCapacity(int slotIndex, int value)
    {
        int maxStacking = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value);
        if (maxStacking <= 1)
        {
            return 0;
        }

        return maxStacking * CapacityInStacks;
    }

    /// <summary>抽屉里现有物品的种类（空则为 0）。</summary>
    public int StoredValue => GetSlotCount(0) > 0 ? GetSlotValue(0) : 0;

    public int StoredCount => GetSlotCount(0);

    public int StoredCapacity => StoredValue != 0 ? GetSlotCapacity(0, StoredValue) : 0;
}
