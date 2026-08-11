# 参考模组反编译笔记

反编译工具：CFR 0.152。源包：

| 包 | 类数 | 备注 |
|---|---|---|
| sophisticatedcore-26.2-1.4.97 | 781 | 精妙背包的依赖，本体是"存储框架" |
| sophisticatedbackpacks-26.2-3.25.86 | 358 | 背包实现，几乎全部逻辑在 core 里 |
| toms_storage-26.2-2.11.1 | 253 | 存储网络 + 终端 |
| ironchest-1.21.11-16.7.3 | 90 | 最简单，纯分级箱子 |
| InventoryProfilesNext-2.3.6 | 750（含 kotlin embedded） | 纯客户端整理，Kotlin |

---

## 1. SophisticatedCore —— 值得整套抄的是它的**分层抽象**

核心接口 `api/IStorageWrapper`：一个"存储体"= 库存 + 升级 + 设置 + 渲染数据 + 排序，
背包/箱子/木桶都实现它，所有升级逻辑对存储类型无感知。

```
IStorageWrapper
 ├ InventoryHandler      物品槽（含堆叠倍率、槽位屏蔽、内容追踪）
 ├ UpgradeHandler        升级槽；按槽位实例化 IUpgradeWrapper，按接口分类缓存
 ├ SettingsHandler       每存储体的设置分类：memory / nosort / itemdisplay / main
 └ RenderDataHandler     外观（升级图标、罐体、电量）
```

升级不是 if-else，而是**一组能力接口**，`UpgradeHandler.getWrappersThatImplement(X.class)` 按需取：

`ITickableUpgrade`（每 tick）、`IPickupResponseUpgrade`（拦截拾取）、`IInsertResponseUpgrade` /
`IExtractResponseUpgrade`（拦截出入库）、`IOverflowResponseUpgrade`（溢出）、`IFilteredUpgrade`（带过滤器）、
`IInventorySlotBlocker`（屏蔽槽位）、`IInventoryLayoutContributor`（占用 UI 布局）、`IUpgradeAccessModifier`（套娃）。

**过滤器**（`upgrades/FilterLogic`）是全模组复用的一等公民：白/黑名单 + 匹配维度
（物品 / 标签 / 耐久 / 组件）+ `PrimaryMatch`（按物品还是按标签）+ "匹配存储内已有物品"模式。

**两个设置类别特别聪明，SC 一定要抄：**
- `MemorySettingsCategory`：把某个槽位"记住"某种物品——即使空着也只收这种物品，
  自动存入/磁铁会优先喂它。等于给玩家自己定义的"归位规则"。
- `NoSortSettingsCategory`：标记不参与排序的槽位。
  两者合起来构成 `getFixedLayoutSlots()`，排序和布局重排都绕开它们。

磁铁升级（`magnet/MagnetUpgradeWrapper`）细节：全局 tick 节流（10 tick，满了退到 40 tick）、
半径 AABB 取实体、过滤器命中才吸、`PreventRemoteMovement` 标记豁免、部分插入后写回剩余数量。
这套"冷却 + 部分插入"的写法直接可移植。

## 2. SophisticatedBackpacks —— 背包本体

- 背包 = 物品 + 可放置方块（`BackpackBlock/BackpackBlockEntity`），放地上就是箱子，捡起来内容不丢。
- 内容不存在物品 NBT 里，而是 `BackpackStorage`（世界 SavedData）按 **contentsUuid** 索引 →
  **和我们在 SC 上被迫采用的方案是同一个思路**，说明这条路走得通（他们还专门写了 `UUIDDeduplicator` 处理复制）。
- 特色升级：`inception`（套娃：把子背包的槽位并进父背包）、`everlasting`（掉岩浆不毁）、
  `refill`（自动补货到手上/快捷栏）、`restock`（把身上同类物品收进背包）、`deposit`（打开箱子时一键存入）、
  `toolswapper`（按方块自动换工具）、`mobcatcher`、`anvil/smithing`。
- `overrideStackedOnOther / overrideOtherStackedOnMe`：拿物品砸背包 = 直接塞进去，不用开界面。
  SC 的 `InventorySlotWidget` 拖放语义可以做等价交互。
- `IStashStorageItem.getItemStashable`：判断"这个背包愿不愿意收这件物品"——
  依据是**背包里已有该物品** 或 **命中记忆槽位**。这正是"智能存入"的判据。

## 3. Tom's Simple Storage —— 存储网络

- `InventoryConnectorBlockEntity` 扫描相邻/经 `InventoryCable` 连通的容器，聚合成 `NetworkInventory`；
  `MultiInventoryAccess` 做统一读写，`InventoryChangeTracker` 做增量刷新（不是每帧全扫）。
- 终端 `StorageTerminalMenu`：服务端持有聚合列表，客户端只拿 `TerminalItemStack` 列表做搜索/排序。
- 搜索语法（`AbstractStorageTerminalScreen.updateSearch`）：
  `|` 或、空格与、`@namespace`、`#tag`、`$组件`、默认匹配显示名 + tooltip，正则回退到字面量。
- 排序：`ComparatorName / ComparatorAmount / ComparatorMod / ComparatorID / ComparatorSpaceEfficiency`。
- 其余部件：合成终端（终端里直接合成 + 自动补料）、无线终端、等级发射器（比较器式信号）、
  库存漏斗、库存代理、过滤器物品（普通/标签/多态）、配置器（设置优先级和面）。

## 4. IronChest —— 分级箱子

极简：`IronChestsTypes` 枚举 = (容量, 每行格数, GUI 尺寸, 贴图)：
铜 45 / 铁 54 / 金 81 / 钻石 108 / 水晶 108（透明可视）/ 黑曜石 108 / 泥土 1（玩笑档）。
`ChestUpgradeItem.onItemUseFirst`：对着箱子右键 → 校验源类型 → 换方块 → **搬运内容和自定义名** → 消耗升级件。
"原地升级、内容不丢"是全部要点。

## 5. Inventory Profiles Next —— 整理

因为是**纯客户端**模组，它必须把整理翻译成一串合法的原版点击：
`inventory/sandbox/ContainerSandbox` + `diffcalculator` 先在沙盒里算出目标布局，再求最小点击序列。
**我们不需要这套**——SC 服务端就在同一进程/由我们自己的包驱动，可以直接落地布局。
但"先算目标布局、再算最小操作差分"的结构值得保留（网络包小、可回滚）。

值得抄的功能面（`config/Features` + `Hotkeys`）：
排序并移动、**锁定槽位**、**自动补货**（含 NBT 匹配策略、阈值）、高亮聚焦物品、
**配置文件 profiles**（按容器类型记住布局）、滚轮移动物品、村民交易辅助、连续合成。
排序方式：`DEFAULT / ITEM_NAME / ITEM_ID / RAW_ID / 数量升降 / CUSTOM`，
CUSTOM 走一套 ANTLR 规则 DSL（`gen/RulesParser`），支持按类别、标签自定义优先级。
另有"按列/按行分组"整理（`GroupInColumnsCalculator`）——对 SC 的小网格特别实用。

---

## 6. 这四个之外，还缺什么（回答"组合是否还能更强"）

按"SC 上收益 / 实现成本"排序：

1. **Storage Drawers（抽屉）** —— 四个模组都没覆盖"单品超大容量 + 一眼看清"。
   SC 的槽位天然就是 (value, count)，抽屉几乎零成本，且能和存储网络串起来。**建议纳入 v1 规划**。
2. **Ender Storage（末影箱/绑定箱）** —— 联机版尤其刚需：跨地点共享同一份内容，用染料/颜色编码配对。
   我们已经有"ID 在 data 位 + 世界注册表"的地基，实现代价极低。**建议纳入 v1**。
3. **死亡墓碑（Corail Tombstone / Gravestone）** —— 死亡时把物品收进一个墓碑箱而不是撒一地。
   与用户已有的 SCTM 死亡标记天然联动。**建议 v1.x**。
4. **Chest Transporter（整箱搬运）** —— 带内容搬箱子；分级箱子做出来之后代价很小。
5. **Refined Storage / AE2 的按需自动合成** —— 存储网络的天花板，但工作量最大，放 v2。
6. **Quark / Inventory Tweaks 的交互糖** —— 双击补满、shift 一键存入、拖拽扫过多格。
   SC 是触屏优先，`InventorySlotWidget` 已有 drag/hold/split 语义，做手势成本低、体感提升大。
7. **Jade/WAILA 式"看一眼箱子就知道装了什么"** —— 准星指向容器时 HUD 预览前 N 项。
8. **Bibliocraft 式标签/命名** —— 给箱子起名并显示在 UI 标题和终端里；配合网络终端才有意义。

明确**不建议**移植的：管道/导管（SC 没有这套物理与渲染基建，成本极高，且电线方案更 SC）、
流体/能量升级（SC 没有对应系统）、JEI 兼容层（SC 没有 JEI）。
