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
| 方块贴图 | ✅ `TerrainGeometry.GetGeometry(Texture2D)` 按贴图分批（**见 5.5，这一行原先写错了**） | ✅ 同，另有 `Block.GetDefaultTexture(value)` |
| 自定义衣物槽 | ❌ `ClothingSlot` 是 enum，写死 4 格 | ✅ `ClothingSlot.AddClothingSlot(name)` |
| Harmony | ❌（未随包提供） | ✅ 随包 `0Harmony.dll` |
| 自定义网络包 | ✅ `PackageManager.RegisterPackage` | 不适用（单人） |
| UI 注入钩子 | `OnWidgetConstruct` / `OnModalPanelWidgetSet` / `GuiUpdate` | 以上全有，另有 `OnWidgetContentsLoaded` 等更多钩子 |
| 方块索引池 | 1024 个，原版占 258 个（0~263） | 同 |

> **关键推论（5.5 又推翻了一次，以 5.5 为准）**：
> "替换整张 `Textures/Blocks`"的方案作废——世界设置里的「方块贴图」（`.scbtex` 材质包）
> 会让 `SubsystemBlocksTexture` 直接加载玩家选的那张整图，我们塞进去的格子会全部失效。
> 但**不需要**因此退回染色：`TerrainGeometry.GetGeometry(Texture2D)` 允许方块把自己的顶点
> 写进绑着自家贴图的批次，两个平台都有。所以现在是**自带图集**，染色只作为拿不到贴图时的退路。

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

## 5.5 自定义贴图：三条通道都能走（一开始判断错了，这里改正）

**① 世界里的方块面 —— 能有自己的贴图。**
地形几何是**按贴图分批**的：

```csharp
public Dictionary<Texture2D, TerrainGeometry[]> Draws;
public TerrainGeometry GetGeometry(Texture2D texture);
```

`GenerateTerrainVertices` 里把顶点写进 `geometry.GetGeometry(自己的贴图).OpaqueSubsetsByFace`，
那一批就用自己的贴图渲染。**联机版和插件版（SCAPI 1.9.2.1）都有这个方法。**
所以不用碰全局的 `Textures/Blocks`——那张图玩家的材质包也在改，碰了必然打架。

UV 仍然按 `格号 % 列数 / 格号 / 列数` 算（`BlocksManager.cs:571`），
所以自家图集要重写 `GetTextureSlotCount`（返回列数）和 `GetFaceTextureSlot`（返回格号）。

**② 物品图标 / 手持模型**：`BlocksManager.DrawCubeBlock(..., Texture2D texture)` 和
`DrawFlatBlock(..., Texture2D texture)` 都有带贴图的重载，两个平台都有。
`DrawFlatBlock` 取的是 `GetFaceTextureSlot(-1, value)`，写 `GetFaceTextureSlot` 时要考虑 face = -1。

**③ 衣物贴图**：`.clo` 的 `TextureName` 直接走 `ContentManager.Get<Texture2D>`，完全由模组提供。

**资源怎么进包**：`.netmod`/`.scmod` 里 `Assets/` 下的所有文件都会被
`ModEntity.InitResources` 注册进 `ContentManager`，键是去掉 `Assets/` 前缀的路径。
`ContentManager.Add` 是**覆盖**语义，所以理论上能顶掉原版资源——正因如此，别去顶 `Textures/Blocks`。

### 自带图集**必须是 16 列**（实机全黑的教训）

第一版做成 8×8 格（256×256）并覆写 `Block.GetTextureSlotCount` 返回 8，结果实机六个方块全是纯黑。

原因：UV 是 `格号 % 列数 / 列数` 算的，而**不是每条路径都会走到那个覆写**——
按 `BlocksData` 里的 `DefaultTextureSlot`（我们当时填的是原版的 25 / 196）配上默认列数 16 去取格，
采样点正好落在自家图集里没画东西的地方；**透明像素在不透明批次里渲染出来就是纯黑**，
而且不报任何错，看不出是"UV 算错"还是"贴图没加载"。

现在的做法：

1. 图集就用 **512×512 / 16 列**，和原版同规格，`GetTextureSlotCount` 一个字都不覆写；
2. 每个方块的 `DefaultTextureSlot` 直接指向**自己在图集里的正面格**——
   即使 `GetFaceTextureSlot` 因故没生效，也只是六面同图，不会变黑；
3. 没用到的格子填**暗品红棋盘格**，下次再采错一眼就看得出来，不会又是一片黑。

### 尺寸与 UV 实测

- 方块图集 `Textures/Blocks.png` = **512×512**，16×16 格，每格 **32×32**
- 衣物贴图 = **64×64** RGBA（`CharacterSkinsManager.ValidateCharacterSkin` 只要求 2 的幂、≤1024）
- 躯干（`Assets/Models/OuterClothingMale.dae` 的 Body 网格，把 COLLADA 的 v 翻成自上而下）：

  | 面 | UV 区域 | 备注 |
  |---|---|---|
  | 正面 | x 4..18, y 15..35 | 法线 +Y。用 LeatherJerkin 验证：有系带花纹的是正面 |
  | 背面 | x 27..41, y 15..35 | 背包本体画这里 |
  | 左侧 | x 46..52, y 16..35 | u=46 前沿，u=52 后沿 |
  | 右侧 | x 55..61, y 16..35 | u=55 后沿，u=61 前沿 |
  | 顶面 | x 47..60, y 12..16 | 两个半边的中间（u≈50..57）朝后 |

  躯干盒子 14 宽 × 6 深 × 20 高。

### 本体资源怎么解出来

`Content.scpak` 是加壳的 zip：头部是 `再乱改就跑路，谁也别想玩！`（UTF-8），
后面按奇偶分半交织——偶数位取前半段、奇数位取后半段。
解法在 `SurvivalCraftModEntity.GetDecipherStream`。
（`.scmod`/`.netmod` 用的是另外两个 HeadingCode，解法在 `ModsManager.GetDecipherStream`。）

## 5.6 衣物：层数、磨损、图标取景

- **躯干槽能叠穿**：`ComponentClothing` 每个槽存的是 `List<int>`，不是单件。
  能不能再叠一件由 `CanWearClothing` 决定：**新的 Layer 必须大于当前最外层**。
  原版躯干的 Layer 是 0~4，护甲（铁/钻石/铜胸甲、木甲、皮衣）全在 4。
  → 想做一件"和护甲共存、永远穿在最外面"的衣物，取 **Layer 5** 即可；
  贴图上不该盖住的地方留透明，下面那层照常露出来。

- **磨损速度 = `20 * Sturdiness` 游戏秒掉 1 点**（湿身时 `10 * Sturdiness`），
  见 `ComponentClothing.Update`；创造模式和关闭生存机制时不磨损。
  → 想做"不会坏"的衣物，把 `Sturdiness` 拉到极大即可。

- **耐久条关不掉**：`InventorySlotWidget` 的判据是 `block.Durability >= 0`，
  而所有衣物共用 `ClothingBlock`（索引 203）的 `Durability = 15`，**没有逐件的开关**。
  不磨损的衣物只能表现为"耐久条一直是满的"。

- **图标取景是逐件可改的**：`BlockIconWidget` 用
  `Matrix.CreateLookAt(block.GetIconViewOffset(Value, env), Vector3.Zero, Vector3.UnitY)`，
  而 `GetIconViewOffset(int value, ...)` 是 virtual **且带 value**。
  原版 `ClothingBlock` 的 `DefaultIconViewOffset` 是 `(-1, 1, -1)`（看到胸前）；
  水平取反成 `(1, 1, 1)` 就是从背后看。

- **千万别用子类顶替原版方块实例**（试过，所有世界都打不开）。
  写一个 `public new static int Index = 203` 的 `ClothingBlock` 子类，
  `BlocksManager.Initialize` 确实会用它覆盖 `m_blocks[203]`，但随后两件事全崩：
  1. `BlocksManager.LoadBlocksData` 是按**类名**匹配 BlocksData 里的行的。
     子类叫别的名字 → 一行都匹配不上 → `CraftingId` / `Durability` / 显示名 / 图标参数
     全部停留在默认值，配方按 CraftingId 找不到衣物，进世界直接
     `Object reference not set to an instance of an object`。
  2. `ClothingBlock` 里所有语言查询都写成 `$"{GetType().Name}:{index}"`
     （`GetDisplayName` / `GetDescription` / `LoadClothingData`），
     类名一变，**所有衣物**的名字都会退化成字面量 `[ClothingBlock:13]`。

  → 要逐件改外观，改**控件**而不是方块：
  `InventorySlotWidget.HideHealthBar`（关耐久条）和
  `BlockIconWidget.CustomViewMatrix`（换图标机位）都是逐个格子的公开属性，两个平台都有。
  用 `ModLoader.GuiUpdate` 从 GUI 根节点往下扫一遍就能全覆盖（快捷栏、物品栏、各种容器界面
  都在这棵树下；图鉴那种独立屏幕不在）。

## 5.7 插件版会**重新分配方块索引**（联机版不会）

`public static int Index` 在两个平台的含义不一样：

- **联机版**：`ModEntity.LoadDll` 只是**读**它 —— `block.BlockIndex = (int)fieldInfo.GetValue(null)`。
  我们写 700 就是 700。
- **插件版（SCAPI）**：它自己挑一个空闲索引，**再写回那个静态字段**。
  实机日志实测：我们申请的 700/701/702 全是 `AirBlock`，真实索引落在 300 一带
  （存储枢纽 304、无线终端 305）。

> **推论：任何地方都不能用编译期常量比较方块索引。**
> 一律在运行时读方块类自己的 `Index` 静态字段（两个平台都对）。

我们踩的坑：`StashChestTiers` 里把三档箱子和升级件的索引写成了 `const`，
而 `StashHubBlock.Index` / `StashWirelessTerminalBlock.Index` 恰好是静态字段。
于是插件版下**只有枢纽和无线终端能用**，分级箱子右键打不开、升级件没反应、
存储网络也认不出自家箱子——因为那些代码在拿 700 去比一个实际是 300 的方块。
联机版正好不重写索引，所以一直没暴露。

修法：`StashChestTier.Index` 改成 `Func<int>` 现取（`() => StashCopperChestBlock.Index`），
常量降级成"申请值"，只用来初始化各方块类的静态字段。

**排查手法**：`StashSelfCheck` 现在会打
`索引=304（申请 709） 类=StashHubBlock`——
两个数不一样是正常的，**类名不是自家类**才是出事了。

## 5.8 拖动中的图标不在 GuiWidget 底下

`InventorySlotWidget` 找拖动宿主写的是 `GameWidget.Children.Find<DragHostWidget>()`，
也就是说 `DragHostWidget` 是 `GuiWidget` 的**兄弟**，不是它的子节点。
要遍历所有物品图标（含拖动中的那个），根节点得取 `GameWidget` 而不是 `GuiWidget`。

## 5.9 拖动中的物品图标是**另一个控件**

`InventorySlotWidget` 开始拖动时不是把自己搬过去，而是：

```csharp
ContainerWidget w = (ContainerWidget)Widget.LoadWidget(null, ContentManager.Get<XElement>("Widgets/InventoryDragWidget"), null);
w.Children.Find<BlockIconWidget>("InventoryDragWidget.Icon").Value = ...;
DragHostWidget.BeginDrag(w, ...);
```

也就是说拖动中的图标是一个**裸的 `BlockIconWidget`**，外面套的是普通 `ContainerWidget`，
**不是** `InventorySlotWidget`。而且 `DragHostWidget` 挂在 `GameWidget` 下，
是 `GuiWidget` 的**兄弟**（`GameWidget.Children.Find<DragHostWidget>()`）。

→ 想统一改所有物品图标（例如换取景机位），遍历时**根节点要取 `GameWidget`**，
并且**要单独认 `BlockIconWidget`**，只认 `InventorySlotWidget` 会漏掉拖动中的那个。

## 5.10 界面画布只有 708×398（最坏情况）——JEI 式侧栏做不了

虚拟画布的尺寸写死在 `ScreensManager.LayoutAndDrawWidgets`：

```csharp
float num = 850f / MathUtils.Clamp(SettingsManager.UIScale, 0.5f, 1.2f);
Vector2 availableSize = new Vector2(num, num / vector.X * vector.Y);
float num3 = num * 9f / 16f;                 // 高度不低于宽 × 9/16
```

`SettingsManager.UIScale` 默认 **0.8**，可调范围 0.5~1.2。于是：

| UI 缩放 | 画布 | 原版面板 614×382 之后剩下 |
|---|---|---|
| 0.5 | 1700×956 | 每边 543 |
| 0.8（默认） | 1062×598 | 每边 224 |
| 1.2（手机常用） | **708×398** | 每边 **47**，上下各 **8** |

一个原版格子 72 单位、我们终端里 50 单位——**47 单位塞不下任何东西**。
所以 MC 那种"面板旁边永远挂一列物品"在 SC 上不可能，只能做成整屏浮层。

**实际踩到的两处溢出**（都是这次才发现的）：
- `StashTerminalWidget` 设计尺寸 816×438，在 708 宽的画布上左右各被切掉 54——整整一列格子点不到。
- `StashUiInjector.MakeRoomForBar` 把面板加高 40 → 422，UI 缩放超过 **1.13** 就上下各被切 12。

现在统一走 `StashScreenMetrics`：先按设计尺寸算，再 `FitScale` 等比压进
`ScreensManager.RootWidget.ActualSize`。**等比缩小总比裁掉强**——格子小还能点，切掉就没了。

浮层挂在 `ComponentGui.ControlsContainerWidget` 里、`DragHostWidget` **之前**
（两个平台都有这个属性），这样浮层盖住面板，而拖拽图标仍在最上层。
不能用 `ModalPanelWidget`：那是**一个**槽位，塞进去会顶掉正开着的工作台界面，
而"一键填料"恰恰需要那个界面背后的合成格还活着。

## 5.11 `ComponentFurnace`：两个平台差得最远的一个组件

| | 联机版 | 插件版（SCAPI 1.9.2.1） |
|---|---|---|
| `Update(float)` | **不是 virtual**（`IUpdateable` 的隐式实现） | `virtual`，可直接 override |
| 强制改方块为 64/65 | **有** | **没有**（改成组件自己持有 `FireParticleSystem`） |
| 可调速度 | 无，`0.15f` 写死在 Update 里 | `public float SmeltSpeed = 0.15f` |
| 内部状态 | 字段全 public，抄得动 | 藏在属性/虚方法后面，抄不动 |

**最要命的是第二行。** 联机版 `ComponentFurnace.Update` 末尾有：

```csharp
if (m_heatLevel > 0f) { if (num3 != 65) ChangeCell(..., ReplaceContents(cellValue, 65)); }
else if (num3 != 64)  { ChangeCell(..., ReplaceContents(cellValue, 64)); }
```

64 / 65 是**原版熔炉和点燃熔炉的方块索引，写死的**。派生一个分级熔炉组件然后调
`base.Update(dt * 倍率)`，结果是炉子放下去第一帧就变回原版熔炉，
而且那次 `ChangeCell` 会触发我们自己的 `OnBlockRemoved`，把炉里的东西撒一地、
连方块实体一起删掉。

→ 联机版必须**把原版 Update 抄一份、只删掉方块替换那一段**；插件版直接 override 调 base 即可。
代价是联机版的分级熔炉没有原版那种"点燃冒火"的粒子（那是
`SubsystemFurnaceBlockBehavior` 认方块索引 65 才加的），正面贴图上画了火来补。

**非 virtual 方法怎么顶掉**：在派生类上**重新声明接口并显式实现**——
C# 会为派生类重建接口映射，而 `SubsystemUpdate` 正是通过接口调用的
（`SubsystemUpdate.cs:123` `sortedUpdateable.Update(dt);`）。
写成 `public new void Update` 是**没用的**，接口调用仍会走到基类。

注意插件版的 `IUpdateable` 多一个成员 `float FloatUpdateOrder`，
所以显式实现那条路在插件版上要多实现一个——我们那边用 override，绕开了。

## 5.12 白嫖到的两条链路

- **联机版开熔炉界面不用自己发包。** 原版 `BlockEditPackage`（`OpenInventoryByPoint`）
  服务端拿坐标找到方块实体的 `IInventory` 回给客户端，客户端按
  `inventory is ComponentFurnace` 分发到 `FurnaceWidget`——子类正好命中。
  （分级箱子就不行：那条路按 `is ComponentChest` 开写死 4×4 的界面，我们 80 格的箱子只能看到前 16 格。）
- **火焰/进度的联机同步也是白嫖的。** `SubsystemFurnaceBlockBehavior.Update` 每秒遍历
  **所有**方块实体收集 `ComponentFurnace` 发 `ComponentFurnacePackage`，我们的也在里面。

## 5.13 配方匹配会穷举平移和翻转 → "配方过大"能精确判定

`CraftingRecipesManager.MatchRecipe`：

```csharp
for (int num = 0; num < 2; num++)              // 翻不翻
  for (int num2 = -3; num2 <= 3; num2++)       // 竖移
    for (int num3 = -3; num3 <= 3; num3++)     // 横移
```

配方存成规范 3×3（`Ingredients` 是 `string[9]`，下标 = 列 + 行*3）。
既然平移自由，**只要非空格的包围盒不超过 N×N 就一定摆得下**——
2×2 的自带合成格判 2、工作台判 3，不用试。
摆放时把包围盒推到左上角，格 (列,行) → 槽位 `列 + 行*N`
（正是 `ComponentCraftingTable` 的编号方式）。

配料串比对规则（`CompareIngredients`）：配方没写 data = 通配，写了 = 必须相等。
取料时**要用虚方法 `Block.GetCraftingId(value)`**，不能读 `Block.CraftingId` 字段——
染色方块之类每个 data 的 craftingId 不一样。

另外两个平台的签名不一样，别直接调：
`FindMatchingRecipe(terrain, ingredients, heatLevel, ComponentPlayer)`（联机版）
vs `(terrain, ingredients, heatLevel, float playerLevel)`（插件版）。

## 5.14 `BlockIconWidget.Light` 写的是 `Value` 的光照位

```csharp
public int Light { get => Terrain.ExtractLight(Value);
                   set => Value = Terrain.ReplaceLight(Value, value); }
```

**不是独立字段。** 所以必须先写 `Value` 再补 `Light`；
反过来写的话光照会被后面的 `Value` 覆盖掉，整屏图标全是暗的——
和当初贴图"全黑"是同一类症状，画面上看不出原因。

顺带：目录里的物品格**不能用 `InventorySlotWidget`**（那个必须绑真实库存的某一格）。
用 `BevelledButtonWidget` 套一个 `BlockIconWidget` 即可——`ButtonWidget` 本身就是 `CanvasWidget`。

## 5.15 `BevelledButtonWidget` 的点击区比按钮小一圈，而且依赖文字撑开

`Widgets/BevelledButtonContents.xml`：

```xml
<BevelledButtonWidget>
  <CanvasWidget Name="BevelledButton.Canvas" Margin="6, 6">
    <BevelledRectangleWidget Name="BevelledButton.Rectangle" />
    <RectangleWidget Name="BevelledButton.Image" IsVisible="false" />
    <LabelWidget Name="BevelledButton.Label" HorizontalAlignment="Center" VerticalAlignment="Center" />
    <ClickableWidget Name="BevelledButton.Clickable" SoundName="Audio/UI/ButtonClick" />
  </CanvasWidget>
</BevelledButtonWidget>
```

`ClickableWidget` 在那个 `Margin="6, 6"` 的画布里，所以**可点范围比按钮四周各小 6 单位**。
按钮有文字时无所谓（文字本来就在中间），但拿它当"无文字的物品格"用就不可靠了——
配方浏览器第一版这么写，实机点物品没反应。

自己搭格子的正确姿势（见 `StashItemButton`）：

```csharp
Size = new Vector2(size, size);                 // Size 在 CanvasWidget 上，Widget 没有
Children.Add(frame);                            // BevelledRectangleWidget，IsHitTestVisible=false
Children.Add(icon);                             // BlockIconWidget，IsHitTestVisible=false
Children.Add(new ClickableWidget { … });        // 最后一个 = 最先命中
```

两条依据：
- `ClickableWidget` **没有 Size 属性**（`Size` 声明在 `CanvasWidget` 上）。
  它靠 `Widget` 构造里的 `DesiredSize = Infinity` 撑满父控件——
  `ContainerWidget.ArrangeChildWidgetInCell` 把 Infinity 夹成整个格子。
- `Widget.HitTestGlobal` 从根往下、**孩子倒序**遍历，返回第一个
  `IsHitTestVisible && HitTest(point)` 的控件；`ClickableWidget.Update` 要求
  `HitTestGlobal(point) == this`。所以点击区必须是**最后一个孩子**，其余装饰件一律
  `IsHitTestVisible = false`。

## 5.16 位图字体里没有 ◀ ▶

实机会画成两个空心方块（豆腐块）。**▲ ▼ 是有的**，翻页可以放心用。
要做"上一个 / 下一个"就老老实实写字。

## 5.17 物品目录：`DisplayOrder` 只在分类内有意义

`Block.GetDisplayOrder(value)` 的值在**不同分类之间没有可比性**——
原版图鉴是先选分类、再在分类内列表的（`RecipaediaScreen.PopulateBlocksList`）。

拉平成一个大列表只按 DisplayOrder 排会出事：`ClothingBlock.GetDisplayOrder` 返回的是
`ClothingData.DisplayIndex`，而 `DisplayIndex` 是加载 `.clo` 时的**递增计数**
（`LoadClothingData` 里的 `num++`），我们的三档行囊落在 39~41，
于是就插到了 DisplayOrder 也是 39~41 的泥土/树叶中间——
实机看到的是"一堆行囊夹在草方块里"。

正确做法：**先按分类（`BlocksManager.Categories` 的原始顺序）分组，再按 DisplayOrder 排。**

## 5.18 可染色衣物会在创造目录里变成 16 条

```csharp
// ClothingBlock.GetCreativeValues()
int colorsCount = ((!clothingData.CanBeDyed) ? 1 : 16);
```

`.clo` 里写 `CanBeDyed="True"` 的每件衣物都会吐出 16 种颜色的变体。
三档行囊 = 48 条，在目录里连着刷一屏半全是长得差不多的包。
不需要染色就写 `False`。

## 5.19 配方查询**必须**按整个 value 精确匹配

试过"精确匹配不到就退回只按方块索引匹配"，想照顾 data 位对不上的情况。**实机直接翻车**：

- 所有衣物共用方块索引 203 → 随便点一件染色衣服都"找到 37 条配方"；
- 所有蛋共用一个索引 → 煮熟的鸽子蛋显示成煮熟的海鸥蛋的配方；
- 鱼卵一类同理，一点就是 14 条。

data 位本来就是用来区分这些东西的。原版图鉴用的就是精确匹配
（`Recipes.Where(r => r.ResultValue == value)`），照抄即可。

配套的一条：**配方面板的标题要显示"玩家点的那个物品"的名字**，
不是 `recipe.ResultValue` 的名字——否则同一条配方被多个变体命中时，标题会张冠李戴。

## 5.20 `CraftingRecipeSlotWidget` 的两个陷阱（点配料跳转时踩到）

```csharp
public override void MeasureOverride(Vector2 parentAvailableSize)
{
    m_blockIconWidget.IsVisible = false;
    m_labelWidget.IsVisible = false;
    if (!string.IsNullOrEmpty(m_ingredient))
    {
        CraftingRecipesManager.DecodeIngredient(m_ingredient, out string craftingId, out int? data);
        Block[] array = BlocksManager.FindBlocksByCraftingId(craftingId);
        if (array.Length != 0)
        {
            Block block = array[(int)(1.0 * Time.RealTime) % array.Length];   // ← 按时间轮播
            m_blockIconWidget.Value = Terrain.MakeBlockValue(block.BlockIndex, 0,
                data.HasValue ? data.Value : 4);                              // ← 没写 data 就填 4
            m_blockIconWidget.IsVisible = true;
        }
    }
    …
}
```

**一、没写 data 的配料，图标里填的是 `4`，不是 0。**
拿这个值直接去查配方必然查空。实机表现：从物品目录点木棒有配方（值 `23`），
从配方里点同一个木棒却说没有（值 `65559` = `23 | (4 << 14)` = 木棒:4）；木板、石板同理。
→ **data 要从配料串自己解析**：写了就照抄，没写就用 **0**；方块索引才取图标里那个。

**二、`SetIngredient(null)` 不清 `m_blockIconWidget.Value`**，只把 `IsVisible` 关掉。
不检查 `IsVisible` 的话，跳到一个没有配方的物品之后，鼠标移到空格上
还会显示**上一条配方**的材料名。

**三、方块索引必须取图标当前那个。** 一个 craftingId 对应多个方块时它按 `Time.RealTime`
轮播，写死取 `array[0]` 会出现"点到的和看到的不是一个东西"。

顺带澄清一个容易误会的点：配方匹配**和数量无关**。
`CompareIngredients` 只比 craftingId 和 data，`ResultCount`（比如木板→4 个木棒的那个 4）
不参与任何匹配。

## 5.21 方块数据从别的方块克隆时，`FireDuration` 会跟着一起来

分级熔炉的 BlocksData 行是从存储枢纽（而它又源自木箱）克隆的，于是继承了
**`FireDuration = 30`**——石头砌的熔炉放下去会被点着，三台一起烧
（实机截图确认）。原版 `FurnaceBlock` 是 `0`。

克隆行的时候要把"这块材料是什么"相关的列一并从**同材质的原版方块**抄过来：
`FireDuration` / `DigMethod` / `DigResilience` / `ExplosionResilience` /
`Density` / `DefaultSoundMaterialName` / `RequiredToolLevel`。

但**不能全抄**：原版熔炉是**模型方块**，我们是实心立方体，这几列必须按自己的形状写——
`IsFluidBlocker`（我们要 TRUE，不然水会穿过去）、`ExplosionKeepsPickables`、
`DefaultExplosionIncendiary`。

## 5.22 插件版的 `ComponentInventoryBase` 没有 `OnSlotChange`

那是**联机版专有**的（它要把槽位改动同步给客户端）。共享代码里直接调会编译不过，
得包一层：

```csharp
private void SlotChanged(int slotIndex)
{
#if !STASH_SCMOD
    OnSlotChange(slotIndex);
#endif
}
```

## 5.23 往玩家身上挂第二个合成格：**别继承 `ComponentCraftingTable`**

`ComponentGui` 按 E 开物品栏时是：

```csharp
m_componentPlayer.Entity.FindComponent<ComponentCraftingTable>(throwOnError: true)
```

玩家身上本来就有一个 2×2 的。再挂一个**同类型**的，这句要么抛异常、要么拿错——
按 E 直接打不开物品栏。所以无线合成终端那块 3×3 从
`ComponentInventoryBase` 派生（行囊组件也是这么挂的，实机验证过），
代价是 `UpdateCraftingResult` / 取产物扣材料那套要自己抄一遍。

抄的时候注意 `CraftingRecipesManager.FindMatchingRecipe` **两个平台最后一个参数不同**：
联机版是 `ComponentPlayer`，插件版是 `float playerLevel`。

## 5.24 抽象基类 + 每个子类各自的 `public static int Index`

两个无线终端共用渲染逻辑，抽了个 `StashWirelessBlockBase`。
**`Index` 字段必须留在每个具体子类上**，不能提到基类——
插件版按类型找这个静态字段并写回真实索引，共用一个字段会让两个方块抢同一个索引。
分级箱子（抽象基类 + 三个各带 `Index` 的子类）早就验证过这个写法可行。

## 5.25 想让"产物的 data 取决于材料"，只能走 `GetAdHocCraftingRecipe`

静态配方的 `CraftingRecipe.ResultValue` 是配方表里写死的一个整数，data 位固定。
所以"合成后保留原物品身上的某个状态"（我们要保留无线终端绑定的存储终端编号）
用静态配方**做不到**。

原版留的口子在 `CraftingRecipesManager.FindMatchingRecipe` 开头：

```csharp
foreach (Block b in BlocksManager.Blocks) {
    CraftingRecipe adHoc = b.GetAdHocCraftingRecipe(terrain, ingredients, heatLevel, …);
    if (adHoc != null && MatchRecipe(adHoc.Ingredients, ingredients)) { craftingRecipe = adHoc; break; }
}
// 匹配不到才去翻静态表
```

**临时配方优先于静态表。** 原版自己用它做染色和修理（染色同样要保留衣服本体）。
方块在这个方法里能拿到 `ingredients`（每格是 `craftingId:data`），
于是可以从材料里读 data，算进产物。

三个注意点：

1. **签名两个平台不同**：联机版最后一个参数是 `ComponentPlayer`，插件版是 `float playerLevel`。
2. **它会被每个方块在每次格子变动时问一遍**，不匹配必须尽快 return null。
3. **临时配方枚举不出来**（`Recipes` 里没有它），所以静态配方**要留着**，
   否则合成表和配方浏览器里再也找不到这东西怎么做。两条并存不冲突：
   查得到的是静态那条，实际合成走临时那条。

返回的配方里 `Ingredients` 不写 data = 通配（`CompareIngredients` 只在 required 写了 data 时才比 data），
这样任何绑定状态的材料都收。

## 5.26 联机版：客户端**不能**直接改槽位，而三个 API 的失败方式各不相同

这是整个联机方案里最容易写错的一处，因为**三个 API 在客户端上表现完全不一样，而且都不报错**。

```csharp
// 1. AcquireItems：客户端直接什么都不做，返回 0
public static int AcquireItems(IInventory inventory, int value, int count)
{
    if (CommonLib.WorkType != WorkType.Client) { …真正搬运…; return count; }
    return 0;                       // ← 客户端走这里
}
```

返回值的含义是**没塞进去的剩余数量**。所以客户端上「返回 0」看起来正好等于「全部收下了」——
调用方一旦按这个语义去 `RemoveSlotItems`，东西就凭空消失了。本项目的填材料踩过这个坑。

```csharp
// 2. AddSlotItems / RemoveSlotItems：客户端毫无拦截，就地改本地副本
public virtual void AddSlotItems(int slotIndex, int value, int count)
{
    AddNetSlotItems(slotIndex, value, count);   // ← 返回值被丢掉了
    OnSlotChange(slotIndex);                    // ← 只有 Server 才真的推送
}

// 3. AddNetSlotItems：装不下时静默返回 false
if ((GetSlotCount(i) != 0 && GetSlotValue(i) != value)
    || GetSlotCount(i) + count > GetSlotCapacity(i, value)) return false;
```

客户端本地改完，服务端根本不知道；下一次 `InventorySync` 推回来就把改动冲掉。
表现是「东西闪了一下又回去了」或者干脆少一份。

**正确做法**：任何跨库存的搬运都不要逐格调 API，而是

1. 在内存里把结果算出来（快照 → 模拟 → 只记变动过的格子）；
2. 生成 `StashPlan`（一组 `SlotAssignment`：某库存某格最终放什么、放几个）；
3. 交给 `StashPlatform.Current.Execute(plan)`——
   单人/主机直接 `GameInventory.Apply`，客户端发 `StashOpPackage` 给服务端；
4. 服务端 `StashServerGuard.Validate` 跑守恒校验（只许重排，不许凭空增减）再落地。

顺带的好处：**中途失败等于什么都没发生**。逐格搬的版本一旦搬到一半失败，
玩家的东西就散在两个容器里了。

`IInventory.Id` 是联机版独有的成员，插件版接口里没有——共享代码里要用 `#if` 隔开。

## 5.27 自己实现合成格时，产物格那一支**不能有 else**

`ComponentInventoryBase` 的槽位是**存档存下来的**，包括产物格。
而"当前匹配到哪条配方"是个**内存字段**，载入后是 null。两者天然不同步：

```
合成终端格子载入：… 网格内容 0:木板×1, 3:木板×1，产物格 9:木棍×4
```

产物格里躺着上一局算出来的木棍，但 `MatchedRecipe == null`。

于是这样写就是个复制漏洞：

```csharp
// ✗ 错的
if (slotIndex == ResultSlotIndex && MatchedRecipe != null) removed = TakeResult(count);
else removed = base.RemoveSlotItems(slotIndex, count);   // ← 产物格掉进这里 = 白送
```

重进世界后第一次拖走产物，走的是 else，**产物给出去了但材料一个没扣**；
紧接着 `UpdateResult` 又按原封不动的材料重算出一份产物，可以反复刷。

原版 `ComponentCraftingTable` 没这个洞，因为它的产物格那一支**没有 else**：

```csharp
// ✓ 原版的形状
int num = 0;
if (slotIndex == ResultSlotIndex)
{
    if (m_matchedRecipe != null) { …扣材料、给产物… }
    // 没有 else：配方为 null 就一件不给
}
else num = base.RemoveSlotItems(slotIndex, count);
UpdateCraftingResult();   // 末尾无条件重算，状态自己就纠正回来了
```

顺带：原版**不在 `Load` 里重算**，所以载入后玩家会看到一个点不动的产物，
要等下一次格子变动才纠正。我们在 `Load` 末尾补了一次 `UpdateResult()`（try/catch 兜底），
省掉那一次"点了没反应"。
