# Survivalcraft 平台事实与约束

本文记录设计所依据的**已核实**事实，全部来自反编译源码 / 程序集元数据，而非记忆或推测。
引用位置：`~/sc-src`（联机版 Survivalcraft.dll 反编译）、`~/.nuget/packages/survivalcraftapi.survivalcraft/1.9.2.1`（插件版 API 程序集）。

## 1. 物品的数据模型：只有一个 int

```
value = contents(10 bit) | light(4 bit) | data(18 bit)
ContentsMask = 1023, LightShift = 10, DataShift = 14, DataMask = -16384
```

两个平台的常量**完全一致**（`sc-src/Game/Terrain.cs:73-101`；插件版 `Terrain` 字段元数据同值）。

槽位 = `(int Value, int Count)`（`ComponentInventoryBase.Slot`）。

> **关键推论**：SC 没有 MC 的 NBT。物品**无法自带任意负载**。
> 任何"物品内部装东西"（可转手的储物袋等）都必须走
> **data 位存 ID + 世界侧注册表存内容** 的路子。18 bit = 262143 个可用 ID，够用。
> 这不是发明：原版家具（FurnitureDesign 下标）、颜料/染色都用 data 位做同样的事。

## 2. 库存体系

- `IInventory`：`SlotsCount / GetSlotValue / GetSlotCount / GetSlotCapacity / AddSlotItems / RemoveSlotItems / ProcessSlotItems / DropAllItems`。
- 两个平台的 `IInventory` **签名不同**：联机版多了 `Id / OnSlotChange / AddNetSlotItems / RemoveNetSlotItems / SetSlotValue / DropSlotItems`，且 `ProcessSlotItems` 多了 source 参数。→ 共享层不能直接吃 `IInventory`，必须自带抽象 + 每平台适配器。
- 容量：`GetSlotCapacity` → `BlocksManager.Blocks[contents].GetMaxStacking(value)`。
- 原版容量现状（`Assets/Database.xml`）：**玩家 26 格**（10 快捷栏 + 16 背包），**箱子 16 格**。
  → 存储压力是真实痛点，这个 Mod 有存在价值。

## 3. 联机版是服务端权威的

`Game.NetWork.Packages/ComponentInventoryPackage.cs`：

- 客户端发 `HandleMoveItem` / `HandleDragDrop`（携带 inventoryId + slotIndex），**服务端执行**再同步回来。
- `SubsystemInventories` 给每个库存分配全局 Id，服务端改槽位后 `pushSyncItem` 增量下发。
- 服务端对创造物品、跨玩家库存有校验，非法操作会 `RemoveClient` 踢人。

> **关键推论**：整理/一键存取**不能在客户端直接改槽位**，必须定义自定义包，服务端算好再落地。
> 好消息：一旦服务端改了槽位，同步是免费的（`OnSlotChange` 自动推送）。
> 自定义包可行——`PackageManager.RegisterPackage`，SCTM 已经用 217/41 两个包号验证过。

## 4. 两个平台的能力差（决定功能分级）

| 能力 | 联机版 netmod | 插件版 scmod (API 1.9.2.1) |
|---|---|---|
| 加新方块 | ✅ `Block` 子类 + `public static int Index`（`ModEntity.LoadDllLogic`） | ✅ 同 |
| 方块贴图 | ❌ 只有一张 256 格公共图集，无逐方块贴图接口 | ✅ `Block.GetDefaultTexture(value)` 可给每个方块自己的贴图 |
| 自定义衣物槽 | ❌ `ClothingSlot` 是 enum，写死 4 格 | ✅ `ClothingSlot.AddClothingSlot(name)` |
| Harmony | ❌（未随包提供） | ✅ 随包 `0Harmony.dll` |
| 自定义网络包 | ✅ `PackageManager.RegisterPackage` | 不适用（单人） |
| UI 注入钩子 | `OnWidgetConstruct` / `OnModalPanelWidgetSet` / `GuiUpdate` | 以上全有，另有 `OnWidgetContentsLoaded` 等更多钩子 |
| 方块索引池 | 1024 个，原版占 258 个（0~263） | 同 |

> **关键推论（已按 5.1 的发现修正）**：贴图差异一度看起来是两版最大的鸿沟，
> 但两条路绕开了它——分级方块用**染色**（复用原版贴图 × 色调），背包做成**衣物**（衣物贴图本来就独立于图集）。
> 所以"替换整张 `Textures/Blocks`"的方案作废：世界设置里的「方块贴图」（`.scbtex` 材质包）
> 会让 `SubsystemBlocksTexture` 直接加载玩家选的那张整图，我们塞进去的格子会全部失效。

## 5. 其它可用的原版机制

- **方块实体**：`SubsystemBlockEntities.CreateBlockEntity("Chest", pos, miner)` + `.xdb` 里的 EntityTemplate；箱子就是"Entity(ComponentChest + ComponentBlockEntity)"。新箱子 = 新 EntityTemplate + 新 Block + 新 BlockBehavior（照抄 `SubsystemChestBlockBehavior`，92 行）。
- **UI**：`Widgets/*.xml` + 代码。`ChestWidget` 只有 52 行，把 GridPanelWidget 填上 `InventorySlotWidget` 即可 → 自定义容器 UI 成本极低。
- **合成**：`.cr` 文件（`CraftingRecipes.xml` 格式），Mod 可直接合并；`ModLoader.DecodeResult / DecodeIngredient` 还能扩语法。
- **电路**：`IElectricElementBlock` + `SubsystemElectricity.GetAllConnectedNeighbors` —— 原版电线已经有一套连通图。存储网络可以**直接跑在原版电线上**，不用发明管道，也不用新贴图。
- **持久化**：世界目录 JSON（`Storage.CombinePaths(GameInfo.DirectoryName, "...")`，SurvivalcraftRuins 已验证）+ 方块实体自己的 `ValuesDictionary`。

## 5.1 后续核实补充的事实（都影响了设计取舍）

- **SC 原版没有金**。`BlocksData.txt` 里不存在任何 `Gold*` 条目；可用金属/宝石只有
  煤、铜（孔雀石 `MalachiteChunkBlock` 冶炼成 `CopperIngotBlock`）、铁、硫磺、硝石、锗、钻石。
  → 分级箱子的材料链必须重排，没有"金箱"这一档。
- **染色渲染是现成的**：`PaintedCubeBlock.GenerateTerrainVertices / DrawBlock` 就是
  "同一张图集贴图 × 一个 `Color`"。`GenerateCubeVertices` 接受任意颜色（原版只是恰好从 16 色调色板取色）。
  → 分级方块可以完全复用原版贴图，用色调区分，**两版一致、零图集冲突**，不再需要图集扩展包。
- **实测原版图集色值**（`Content.zip → Assets/Textures/Blocks.webp`，512×512、16×16 格、每格 32px，
  用 Engine.dll 的 `Image.Load` 解码后取众数色）：
  箱子木色 `#806030`、铁锭 `#A0A0A0`、铜锭 `#C0B0A0`、钻石块 `#50A0F0`、玻璃 `#C0D0F0`、
  孔雀石方块 `#30B090`、煤方块 `#101010`。
- **`MaxStacking` 是 `BlocksData.txt` 的一列**（第 38 列）：普通方块 40、钻石 4、
  **工具 / 衣服 / 箱子 = 1**。耐久值本身存在 data 位里，用过的工具和新工具是不同的 value，本就不会合并。
  → "抽屉只收可堆叠物品"这条规则不需要额外实现，照着 `MaxStacking > 1` 判断即可。
- **衣物不走方块图集**：`Clothes.xml` 每条 `ClothingData` 自带 `TextureName="Textures/Clothing/..."`，
  是独立 `Texture2D`；所有衣物共用 `ClothingBlock`（索引 203）一个方块索引，靠 data 区分类型/耐久/颜色。
  → 背包做成衣物：**联机版也能有独立贴图，且不占方块索引**。代价是 data 位放不下实例 ID。
- **衣物槽就是普通库存槽**：`ClothingWidget` 用的是 `InventorySlotWidget.AssignInventorySlot(ComponentClothing, i)`，
  且每个槽存的是 `List<int>`（可叠穿多层）。→ 拖到人物身上穿戴、拖出来脱下，**原版白送**。
- **衣物只换贴图不换几何**：`ComponentOuterClothingModel` 用一套固定人形网格（Leg1/Leg2/Body/Head）。
  想让背包在人物背上"鼓出来"，只能用 `OnModelRendererDrawExtra` 自己挂模型。
- **图鉴（帮助 → 合成配方）自动收录**：`RecipaediaScreen` 按 `BlocksManager.Categories` +
  各方块 `GetCreativeValues()` 列条目，`RecipaediaRecipesScreen` 按 `ResultValue` 取配方。
  只要类别、创造值、`.cr` 三者对上就会出现，不需要额外代码。原版箱子的类别是 `Items`。

## 5.2 实机踩到的坑（2026-08-11 第一次装进游戏）

- **联机版加衣物必须索引连续，否则所有世界都进不去**（原版的 bug，我们踩了）：
  `ClothingBlock.m_clothingData` 是**按索引下标的 DynamicArray**，`LoadClothingData` 里
  `if (result >= Count) Count = result + 1;` 会把中间撑出一堆 null；
  而 `GetCreativeValues()` 写的是
  ```csharp
  m_clothingData.OrderBy(cd => cd.DisplayIndex)      // ← 排序阶段就读 null.DisplayIndex，NRE
  foreach (...) { if (clothingData != null) { ... } } // 循环体里判了空，但已经晚了
  ```
  → 进世界时 `ComponentCreativeInventory.Load` 抛 NullReferenceException，新老世界全部打不开。
  我们最初把背包放在索引 100，38~99 全是 null，直接炸。**必须紧接原版最后一个（37）连续往下排。**
  插件版没这个问题（那边 `m_clothingData` 是 Dictionary），但为了两版一致，索引统一取 38~40。

- **包号要避开生态里其他 Mod**。实机日志：`数据包ID冲突！ID:219 已被 GeniusToolPackage 占用`。
  目前已知占用：原版 0-40 / 56-59 / 250-253，SCTM 41 / 217，Genius 219。本 Mod 改用 **230 / 231 / 232**。

- **多个包要一个一个注册**：写在同一个 `try` 里的话，第一个撞号抛异常，后面的包会全部漏注册。

## 5.3 第二次实机（同日）暴露的三个坑

- **钩子必须显式注册，否则 override 根本不会被调用。**
  `ModsManager.HookAction(name, ...)` 只会遍历 `ModHooks[name]` 里注册过的 loader，
  而注册要自己在 `__ModInitialize` 里写 `ModsManager.RegisterHook("OnModalPanelWidgetSet", this)`。
  官方示例模组的注释写明了这点（"必须在 __ModInitialize() 方法中注册，否则无效"），我第一版全漏了
  → 整理按钮、准星预览、世界数据加载全部静默失效，日志里也不会报错。**两个平台都是这样。**

- **联机版 `ClothingWidgetOpen` 是个死钩子**：`ComponentGui` 里派发时传的钩子名是空串
  （`ModsManager.HookAction("", ...)`，原版写漏了），永远匹配不到任何 loader。
  插件版传的是正确的 `"ClothingWidgetOpen"`。→ 依赖它的功能在联机版必须另找入口。

- **联机版 `SubsystemTerrain.ChangeCell` 在 `miner == null` 时只调 5 参的 `OnBlockAdded`**：
  ```csharp
  if (miner == null) { behaviors[j].OnBlockAdded(value, old, x, y, z); continue; }
  behaviors[j].OnBlockAdded(value, old, x, y, z);
  behaviors[j].OnBlockAdded(value, old, x, y, z, miner);
  ```
  只覆写 6 参版的话，任何走 `ChangeCell` 的方块替换都不会建方块实体。
  我们的箱子升级就栽在这里——外观换了、实体没建，于是"提示升级失败 + 新箱子打不开"。**两个重载都要覆写。**
  另外 `ChangeCell` 自带 territory 校验和 `SubsystemTerrainPackage` 广播，服务端改格子用它就对了。

## 5.4 `WidgetInput.Clear()` 会把整个界面的鼠标弄死

想在 `UpdateInput` 钩子里"吃掉按键、别让打字触发热键"时，**不能调 `input.Clear()`**。

原因在点击的合成方式上：`WidgetInput` 是个状态机，按下时记 `m_mouseDownPoint`，
抬起时才合成出 `Click`。而 `Clear()` 除了清 `Click/Tap/Press`，还会把 `m_mouseDownPoint = null`：

```csharp
public void Clear()
{
    m_isCleared = true;
    m_mouseDownPoint = null;      // ← 按下的记录没了
    ...
    ClearInput();
}
```

按下的那一帧被清掉，抬起时就永远合不出 `Click`。于是**只要我们持续 Clear，整个界面的鼠标全废**：
点不掉输入框的焦点、按钮没反应、物品也拖不动。实机表现就是"搜索框光标一直闪，退不出去，别的功能都测试不了"。

替代做法是**只置 `m_isCleared = true`**（公开字段，直接写）：

- 它单独作用时只挡 `IsKeyDown/IsKeyDownOnce/IsKeyDownRepeat/LastKey/LastChar` 这类**键盘**查询；
- `Click / Tap / Press / Drag` 是普通自动属性，不受它管 → 鼠标照常；
- 下一帧 `WidgetInput.Update()` 开头就 `m_isCleared = false`，不会积累。

`Back` / `Cancel`（原版把 Esc 翻译过来的）也是普通属性，`m_isCleared` 管不到，要单独置 false，
否则打字时按 Esc 会连界面一起关掉。

**帧内顺序**（`Widget.UpdateWidgetsHierarchy` 是"先子后父、子控件倒序"）：

```
RootWidget.WidgetsHierarchyInput.Update()   // m_isCleared = false，重算 Click
  └ … → GameWidget.WidgetsHierarchyInput.Update()
        └ GuiWidget 子树（我们的界面控件在这里 Update）
        └ GameWidget.Update()
  └ GameScreen.Update() → GameManager.UpdateProject()
        └ ComponentInput.Update() → HookAction("UpdateInput")   // 我们的钩子在这儿
```

所以钩子里改 `m_isCleared` 只会影响**排在我们后面的 Mod 钩子**和本帧剩下的组件逻辑，
影响不到自己的控件（它们已经更新完了，下一帧又会被复位）。

顺带：原版自己处理"聊天框获得焦点时别让按键当热键"用的是
`ComponentInput.AllowHandleInput = false`（见 `GameWidget.UpdateInputState`），这是官方姿势，
但它只管原版自己，管不到别的 Mod。

## 6. 已知的坑

- 联机版世界文件夹名会被回收复用（见 SCTM 经验）→ 世界侧注册表必须带**种子/世界 GUID 校验**，否则串档。
- `AddNetSlotItems` 在"槽位已有不同物品"或"超过容量"时返回 false 而不是部分插入 → 自己实现插入要处理部分插入。
- 创造模式可复制物品栈：将来做 B 方案储物袋时，实例 ID 存在 data 位，**同 ID 多份**必须在打开时做写时复制（split-on-open），否则两个袋子共享一份内容。（A 方案的背包按玩家索引，没有这个问题。）
