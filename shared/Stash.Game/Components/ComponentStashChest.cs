namespace Game;

/// <summary>
/// 分级箱子的库存组件。放在 Game 命名空间下，和官方示例模组一致——
/// 实体模板里的 Class 字符串要能被 TypeCache 解析到。槽位数来自实体模板里的 <c>SlotsCount</c> 参数，每个档位一份模板。
///
/// 刻意**不继承** <see cref="ComponentChest"/>：联机版原版流程会按 <c>is ComponentChest</c>
/// 去开写死 4×4 的原版界面，继承它会导致我们的大箱子只显示前 16 格。
/// </summary>
public class ComponentStashChest : ComponentInventoryBase
{
}
