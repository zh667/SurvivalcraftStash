# Stash 设计文档

目标：把"背包 / 箱子 / 整理 / 存储网络"做成**一个连贯的收纳与物流套件**，
而不是把四个 MC 模组各翻译一遍。前置事实见 [SC-PLATFORM.md](SC-PLATFORM.md)，
参考来源见 [MC-REFERENCE.md](MC-REFERENCE.md)。

## 0. 设计原则

1. **原版优先**：能用原版机制（电线连通图、方块实体、`.cr` 合成、拖放语义）就不发明新机制。
2. **服务端权威**：联机版所有槽位变更由服务端执行，客户端只发意图。单人版走同一条代码路径（本地"服务端"）。
3. **分层可裁剪**：核心算法不依赖游戏程序集；平台差异（贴图、衣物槽、Harmony）用能力开关降级，而不是分叉功能。
4. **不破坏存档**：所有新状态都可丢弃——卸载 Mod 后世界仍能开，最多是新方块变空气、背包内容留在 JSON 里。

## 1. 分层架构

```
shared/Stash.Shared          纯 C#，不引用 Game/Engine，可单测
  Items/      ItemValue（contents/data 位运算）、ItemKey、IItemCatalog
  Inventory/  IStashInventory（最小库存抽象）、InventoryOps（插入/抽取/部分插入）
  Sorting/    SortMethod、SlotSnapshot、StashSorter（纯函数：快照 → 目标布局 → 最小移动差分）
  Filters/    ItemFilter（白/黑名单、按类别/按物品、记忆槽位匹配）
  Storage/    StashRegistry（背包/绑定箱内容注册表，含世界身份校验）
  Upgrades/   升级能力接口 + 调度器（tick / 拾取 / 入库 / 溢出）
  Network/    存储网络图（节点聚合、增量刷新、搜索与排序查询）

src/SurvivalcraftStash        联机版 netmod：适配器 + 自定义包 + 服务端执行器
plugin/SurvivalcraftStash     插件版 scmod：适配器 + 自定义贴图/衣物槽/Harmony 增强
tests/SurvivalcraftStash.Tests xunit，只测 shared
```

共享代码用 `Stash.Shared.props` 以 `<Compile Include>` 方式**编译进两个 Mod**，
而不是像 SCTM 那样两边各存一份（那边已经出现"两版文件不再逐字节相同"的漂移）。

平台适配器要提供的东西（`IStashPlatform`）：
`ExecuteSlotPlan(计划)`（联机版发包 / 单人版直接落地）、`OpenPanel(widget)`、`WorldIdentity`（世界 GUID + 种子）、
`Log`、`Capabilities`（是否支持自定义贴图 / 衣物槽 / 网络包）。

## 2. 数据模型

### 2.1 容器内容注册表（背包、绑定箱共用）

```
data 位（18 bit）= StashId（1..262143，0 = 未分配）
世界目录/SurvivalcraftStash.json:
{
  worldId: <世界 GUID>, seed: <种子>,         // 防止 SC 回收世界文件夹导致串档
  nextId: 1234,
  stashes: { "17": { kind:"backpack", tier:2, slots:[[value,count]...], upgrades:[...], settings:{...} } }
}
```

生命周期：
- 合成出来的背包 `StashId = 0`；**首次打开时才分配 ID**（避免仓库里堆一堆空记录）。
- `GetMaxStacking = 1`：带 ID 的容器物品永不堆叠，data 位不会因合并丢失。
- **写时复制**：打开时若发现同一 ID 被多个物品实例引用（创造模式复制/给予），为当前实例复制一份新 ID。
- **回收**：世界加载时扫一遍"没有任何物品/方块引用"的记录，超过 N 天后清理（先写日志，不默认删）。

### 2.2 分级容器（箱子/抽屉）

内容仍走原版：方块实体 Entity + `ComponentStashChest`（继承 `ComponentInventoryBase`），
槽位数来自方块实体模板；升级件对着箱子使用时**换方块 + 搬内容 + 保留名字**（照抄 IronChest 的 `onItemUseFirst`）。

### 2.3 每容器设置

抄 SophisticatedCore 的两类，落到容器记录里：
- `memory`：槽位 → 记住的物品 value（空着也只收它，自动存入优先喂它）
- `nosort`：不参与整理的槽位
另加 `label`（容器命名，供终端搜索）。

## 3. 五大支柱

### P1 整理（IPN + Sophisticated 的记忆/免排序）

- **整理按钮注入所有库存界面**：靠 `ModLoader.OnWidgetConstruct` / `OnModalPanelWidgetSet` 识别
  `ChestWidget / FullInventoryWidget / CraftingTableWidget / FurnitureInventoryPanel` 等，
  在容器网格旁挂一排按钮（整理 / 一键存入 / 一键取出 / 按列分组）。**不改游戏代码，不依赖 Harmony**，两版通用。
- **排序方式**：类别 → 名称（默认）、名称、方块索引、数量降/升、自定义规则。
  自定义规则先用 JSON 配置（`priority: ["ores","tools",...]`），不上 ANTLR。
- **锁定槽位**：长按槽位加锁（借用现有 hold 语义），锁定槽位不参与整理，也是"记忆槽位"的入口。
- **自动补货**：快捷栏物品用光时，从背包/身上容器补同类；阈值、匹配严格度（同 value / 同 contents）可配。
- **一键存入 / 取出**：存入只送"目标容器里已有的物品 + 命中记忆槽位的物品"（Sophisticated 的 stashable 判据），
  另有"全部存入"作为显式选项。
- **触屏手势**：拖过多个槽位连续搬运、双击补满一栈（ItemScroller/MouseTweaks 的体感），SC 触屏优先，收益高。
- **执行路径**：客户端只算 `SlotPlan`（目标布局 + 最小移动序列），发 `StashOpPackage` → 服务端校验容器可达性
  与所有权后落地。单人/插件版同一份 `StashSorter`，直接执行。

### P2 分级箱子（IronChest）

| 档 | 槽位 | 合成方向 |
|---|---|---|
| 木箱（原版） | 16 | — |
| 铜箱 | 32 | 木箱 + 铜锭升级件 |
| 铁箱 | 48 | 铜箱 + 铁锭升级件 |
| 金箱 | 72 | 铁箱 + 金锭升级件 |
| 钻石箱 | 96 | 金箱 + 钻石升级件 |
| 水晶箱 | 96 + 内容可视 | 钻石箱 + 玻璃升级件 |

- 每档 = 一个 `Block`（`static int Index`）+ 一个 EntityTemplate（.xdb）+ 一个 BlockBehavior（照抄 92 行的原版箱子行为）。
- UI 用一个可变行列数的 `StashChestWidget`（原版 `ChestWidget` 只有 52 行，扩起来很轻）。
- **贴图策略按平台降级**：插件版走 `Block.GetDefaultTexture` 给独立贴图；
  联机版先复用原版箱子图集槽位，靠名称/UI/粒子区分，另提供**可选**的图集扩展包（与材质包冲突，默认关）。
- 升级件"原地升级不丢内容"是硬需求；升级前校验目标档位、失败要给明确提示。

### P3 背包（SophisticatedBackpacks + Core）

- 背包物品三档：布背包 16 / 皮革背包 32 / 铁扣背包 48，另加 4 个升级槽（随档位增长）。
- **携带方式**：
  - 插件版：`ClothingSlot.AddClothingSlot("Backpack")` 新增躯干后的背包槽，可穿戴、模型可见（后续）。
  - 联机版：无法加衣物槽 → 退化为"快捷栏里的背包物品"，用交互键/UI 按钮打开；
    同时支持"放到地上就是箱子"（`BackpackBlock` 思路），两版一致。
- **升级系统**（能力接口，按 Sophisticated 的分类做，不做 if-else）：
  第一批：拾取、磁铁（含 SC 的可采集物 `Pickable`）、过滤、堆叠倍率、自动存入、自动补货、垃圾（void）、记忆槽位。
  第二批：压缩、熔炼（挂 SC 熔炉配方）、进食（接 `ComponentVitalStats`）、工具替换、套娃（限一层）。
- **交互糖**：拿一摞物品砸到背包槽 = 直接塞进去（对应 `overrideOtherStackedOnMe`）。

### P4 存储网络（Tom's，但跑在原版电线上）

- **枢纽（Storage Hub）**：`IElectricElementBlock`。用 `SubsystemElectricity.GetAllConnectedNeighbors`
  沿**原版电线**遍历，收集连通的容器方块 → 聚合成一个虚拟库存。不需要新管道、新贴图。
- **终端（Terminal）**：搜索 + 排序 + 取放。搜索语法按 SC 改造：
  `空格` 与、`|` 或、`#类别`（BlocksManager 的 Category）、`@容器名`（容器 label）、其余匹配显示名。
  排序沿用 P1 的 `SortMethod`。
- **合成终端**：终端里放 3×3 合成网格，配方直接查 `CraftingRecipesManager`，缺料从网络自动抽取。
- **增量刷新**：抄 `InventoryChangeTracker`——容器变更打脏标记，终端按脏标记增量下发，不做每帧全扫
  （SCTM 的性能教训：每帧全量扫描是灾难）。
- **无线终端**：手持物品打开最近已注册枢纽的终端，受距离/维度限制。
- **自动化件**：库存漏斗（按过滤器在两个容器间搬运，走电路信号触发）、等级发射器（数量阈值 → 电信号）。

### P5 抽屉（新增，补四个模组的空白）

- 单品大容量：1×1 抽屉 = 一种物品 × 4 组容量；2×2 抽屉 = 4 种 × 各 2 组。
- 正面显示物品图标 + 数量（复用 `BlockIconWidget` 的绘制路径，或用 `GuiDraw` 叠加）。
- 右键放入 / 拿取一栈；连上枢纽即进入网络。
- 升级：容量升级、抽取锁定（防止拿空后类型丢失）、虚空（超出上限直接销毁）。

## 4. 跨切能力

- **绑定箱（末影箱）**：颜色三元组配对（用染料合成决定），内容存注册表；联机版按玩家或按队伍隔离，可配。
- **容器命名**：给箱子/抽屉起名，终端可搜。
- **容器预览 HUD**：准星指向容器时显示前 N 项（Jade 式），可关。
- **死亡收纳**：死亡时生成一个"遗物箱"收走物品，配合 SCTM 死亡标记（v1.x）。

## 5. 能力矩阵（两版差异一览）

| 功能 | netmod | scmod |
|---|---|---|
| 整理/锁定/补货/一键存取 | ✅（服务端执行） | ✅ |
| 分级箱子 | ✅（贴图复用/可选图集包） | ✅（独立贴图） |
| 背包物品 + 升级 | ✅（快捷栏/放置） | ✅ |
| 背包穿戴槽 | ❌（降级为快捷栏） | ✅ |
| 存储网络 + 终端 | ✅ | ✅ |
| 抽屉 | ✅ | ✅ |
| 绑定箱 | ✅（可按队伍） | ✅（个人） |

## 6. 风险与对策

| 风险 | 对策 |
|---|---|
| 方块索引与其他 Mod 撞车 | 集中在一个 `BlockIndices.cs`，留一段连续区（拟 700–740），并在设置里可整体偏移 |
| 联机版贴图受限，分级箱子不好看 | 先靠 UI/命名区分；图集扩展包做成可选项；先验证图集空格数量 |
| 世界文件夹回收导致注册表串档 | 记录 worldId + seed，不匹配则拒绝加载并另存备份（SCTM 已踩过这个坑） |
| 创造模式复制背包导致内容共享 | 打开时写时复制；同 ID 多引用检测 |
| 网络包体积/频率 | 只发差分（SlotPlan），终端增量刷新，搜索在客户端本地做 |
| 服务端反作弊踢人 | 所有操作走自定义包并复用原版校验（所有权、槽位范围、创造物品） |
| 排序被误用（弄乱玩家布局） | 锁定槽位 + 记忆槽位 + 撤销一次（保留上一次布局快照） |

## 7. 明确不做

管道/导管物理、流体与能量系统、JEI 兼容层、跨维度无限无线（SC 没有维度概念）、
自动合成 CPU（v2 再议）。
