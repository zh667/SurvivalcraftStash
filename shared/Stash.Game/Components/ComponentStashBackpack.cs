namespace Game;

/// <summary>
/// 玩家随身背包的库存组件，挂在 Player 实体上（见 StashDatabase.xdb）。
///
/// 为什么不是把内容写进自己的 JSON 账本：挂成玩家实体的组件之后，
/// 存档由原版负责、联机同步由 <c>SubsystemInventories</c> 负责、
/// 拖放取放走原版 <c>ComponentInventoryPackage</c> 那条服务端权威路径——全都是白送的。
///
/// 槽位固定开 32 格，实际能用几格由**当前穿着的背包档位**决定（见 <c>StashBackpack</c>）。
/// 这样换档位不需要动存档结构。
/// </summary>
public class ComponentStashBackpack : ComponentInventoryBase
{
}
