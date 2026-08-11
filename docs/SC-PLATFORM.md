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
> 任何"物品内部装东西"（背包、末影箱绑定、抽屉物品）都必须走
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

> **关键推论**：**贴图能力是两版最大的差异**。插件版可以做漂亮的分级箱子；
> 联机版要么复用原版图集槽位（视觉靠命名 + UI 区分），要么可选地替换整张 `Textures/Blocks`（与材质包/其他 Mod 冲突）。
> 待验证项：解码 `Content.zip → Assets/Textures/Blocks.webp`，数清 256 格里还有多少空格。

## 5. 其它可用的原版机制

- **方块实体**：`SubsystemBlockEntities.CreateBlockEntity("Chest", pos, miner)` + `.xdb` 里的 EntityTemplate；箱子就是"Entity(ComponentChest + ComponentBlockEntity)"。新箱子 = 新 EntityTemplate + 新 Block + 新 BlockBehavior（照抄 `SubsystemChestBlockBehavior`，92 行）。
- **UI**：`Widgets/*.xml` + 代码。`ChestWidget` 只有 52 行，把 GridPanelWidget 填上 `InventorySlotWidget` 即可 → 自定义容器 UI 成本极低。
- **合成**：`.cr` 文件（`CraftingRecipes.xml` 格式），Mod 可直接合并；`ModLoader.DecodeResult / DecodeIngredient` 还能扩语法。
- **电路**：`IElectricElementBlock` + `SubsystemElectricity.GetAllConnectedNeighbors` —— 原版电线已经有一套连通图。存储网络可以**直接跑在原版电线上**，不用发明管道，也不用新贴图。
- **持久化**：世界目录 JSON（`Storage.CombinePaths(GameInfo.DirectoryName, "...")`，SurvivalcraftRuins 已验证）+ 方块实体自己的 `ValuesDictionary`。

## 6. 已知的坑

- 联机版世界文件夹名会被回收复用（见 SCTM 经验）→ 世界侧注册表必须带**种子/世界 GUID 校验**，否则串档。
- `AddNetSlotItems` 在"槽位已有不同物品"或"超过容量"时返回 false 而不是部分插入 → 自己实现插入要处理部分插入。
- 创造模式可复制物品栈：背包 ID 存在 data 位，**同 ID 多份**必须在打开时做写时复制（split-on-open），否则两个背包共享一份内容。
