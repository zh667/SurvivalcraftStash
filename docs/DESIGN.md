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
  Storage/    StashRegistry（玩家背包账本 / 储物袋注册表，含世界身份校验）
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

### 2.1 容器内容注册表（A 方案的玩家账本 / B 方案的实例 ID 共用同一份存储）

```
data 位（18 bit）= StashId（1..262143，0 = 未分配）
世界目录/SurvivalcraftStash.json:
{
  worldId: <世界 GUID>, seed: <种子>,         // 防止 SC 回收世界文件夹导致串档
  nextId: 1234,
  players: { "<玩家GUID>": { backpack: { tier:2, slots:[[value,count]...], upgrades:[...], settings:{...} } } },
  stashes: { "17": { kind:"pouch", slots:[...] } }        // B 方案（可转手储物袋）后补时启用
}
```

A 方案（当前）：背包内容按**玩家 GUID** 索引，不需要实例 ID，也就没有复制/回收问题。
穿脱背包只改变"可用格数"，超出新档位格数的物品在脱下/降档时退还给玩家（放不下则掉落在脚下）。

B 方案（后补储物袋）才需要实例 ID，届时的生命周期：
- 合成出来的储物袋 `StashId = 0`；**首次打开时才分配 ID**（避免存档里堆一堆空记录）。
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

**材料链按 SC 实际矿物定**。已核实：SC 原版**没有金**（`BlocksData.txt` 里不存在任何 Gold 条目），
可用的金属/宝石只有：煤、铜（孔雀石冶炼）、铁、硫磺、硝石、锗、钻石。

| 档 | 槽位 | 升级材料 | 色调 |
|---|---|---|---|
| 木箱（原版） | 16 | — | 原色 `#806030` |
| 铜箱 | 32 | 铜锭（孔雀石冶炼所得） | `#C8A060` 暖铜 |
| 铁箱 | 48 | 铁锭 | `#A0A0A0` 灰 |
| 钻石箱 | 80（10 列 × 8 行） | 钻石 | `#50A0F0` 亮蓝 |

> 实现时把钻石箱从 96 降到 80：界面是并排布局（容器网格 + 玩家 4×4），
> 96 格要么列数太多把面板撑过屏幕，要么行数太多超出高度。80 格用 48px 格子正好收在原版面板尺寸附近。

色值来自**实测原版图集**（用 Engine.dll 解码 `Textures/Blocks.webp`，取对应贴图格的众数色）：
铁锭 `#A0A0A0`、铜锭 `#C0B0A0`、钻石块 `#50A0F0`、玻璃 `#C0D0F0`、箱子木色 `#806030`。
其中铜锭实测偏灰米（`#C0B0A0`），与铁的灰太接近，故做暖化处理为 `#C8A060`——这是唯一一处偏离实测值的地方，理由是辨识度。

> 锗（Germanium）在 SC 里是电路元件材料，用来做箱子语义上别扭，暂不设锗箱。

**渲染方案：染色，不碰图集。** 抄原版 `PaintedCubeBlock` 的做法——
`GenerateTerrainVertices` 和 `DrawBlock` 都是"同一张贴图 × 一个 Color"，
所以分级箱子复用原版箱子的图集槽位（25/26/27/42），按档位乘不同色调即可。
**零新贴图、零图集冲突、两版完全一致**，之前的"可选图集扩展包"方案作废。
（原版 `PaintedCubeBlock` 的颜色取自 16 色调色板，我们不受此限制——那一层只是它的取色来源，
`GenerateCubeVertices` 接受任意 `Color`。）

- 每档 = 一个 `Block`（`static int Index`）+ 一个 EntityTemplate（.xdb）+ 一个 BlockBehavior（照抄 92 行的原版箱子行为）。
- UI 用一个可变行列数的 `StashChestWidget`（原版 `ChestWidget` 只有 52 行，扩起来很轻）。
- 升级件"原地升级不丢内容"是硬需求；升级前校验目标档位、失败要给明确提示。

**合成表与图鉴**：写进 `.cr` 文件（格式同 `CraftingRecipes.xml`），类别沿用原版箱子的 `Items`。
已核实图鉴机制：`RecipaediaScreen` 按 `BlocksManager.Categories` + 各方块 `GetCreativeValues()` 列条目，
`RecipaediaRecipesScreen` 用 `CraftingRecipesManager.Recipes.Where(r => r.ResultValue == value)` 取配方。
所以只要类别、创造值、`.cr` 三者对上，**帮助 → 合成配方**里会自动出现箱子和升级件的条目与配方图，不需要额外代码。

### P3 背包（SophisticatedBackpacks + Core）—— 采用 A 方案：穿戴式

**为什么是 A**：`ClothingBlock` 的 18 位 data 已被"衣物类型(8) + 耐久(4) + 颜色"占掉大半，
**塞不下背包实例 ID**，所以"能穿戴"和"内容跟物品实例走"二者只能取其一。先做 A（穿戴），B 后补。

- **背包 = 一件衣物**（在 `.clo` 里加 `ClothingData` 条目，`Slot="Torso"`、`IsOuter="True"`、`Layer` 排在最外）。
  好处全部已核实：
  - 不占方块索引（所有衣物共用 `ClothingBlock` 索引 203，靠 data 区分）
  - **贴图独立**（`TextureName="Textures/Clothing/..."`，不走方块图集）→ **联机版也能有自己的贴图**
  - **拖放穿脱白送**：`ClothingWidget` 里的槽位本来就是 `InventorySlotWidget.AssignInventorySlot(ComponentClothing, i)`，
    拖到人物身上即穿戴、拖出来即脱下，不需要写任何代码
  - 同一槽位可叠穿多件（`ComponentClothing.m_clothes` 是 `List<int>`），不挤占玩家的衣服
- **外观**：先做贴图版，颜色沿用 P2 的档位色（布=原色、铜扣=`#C8A060`、铁扣=`#A0A0A0`）。
  原版衣物只换贴图不换几何（`ComponentOuterClothingModel` 用固定人形网格），
  想真的"背上鼓一个包"要用 `OnModelRendererDrawExtra` 自己挂模型 → 放 v1.x。
- **内容归属**：跟玩家走。每个玩家一份背包账本（按档位决定可用格数），存世界目录 JSON。
  脱下背包 = 内容仍在你名下，重新穿上还在；换更高档位背包只增加可用格数。
- **格子容量按原版算**：背包内每格上限 = `GetMaxStacking(value)`，不做倍率（倍率交给"堆叠升级"，见 P5 附）。
- 三档：布背包 16 / 皮革背包 24 / 铁扣背包 32。升级槽尚未实现（见路线图 M3 剩余项）。
- 实现细节：内容挂成玩家实体上的 `ComponentStashBackpack`（32 格），当前档位决定可用几格；
  降档时超出的东西在打开时退还给玩家，放不下就掉在脚下。这样存档与联机同步全部走原版机制。
- **升级系统**（能力接口，按 Sophisticated 的分类做，不做 if-else）：
  第一批：拾取、磁铁（含 SC 的可采集物 `Pickable`）、过滤、自动存入、自动补货、垃圾（void）、记忆槽位。
  第二批：压缩、熔炼（挂 SC 熔炉配方）、进食（接 `ComponentVitalStats`）、工具替换。
- **打开方式**：`ClothingWidgetOpen` 钩子在衣物界面加"打开背包"按钮；快捷键/长按槽位同效。

> **B 方案（后补）**：可转手的"储物袋"——独立方块物品，18 位 data 存实例 ID，内容跟物品走，
> 能给别人、能塞进箱子、放地上变临时箱子。届时复用 2.1 的注册表，不与 A 冲突（两者可共存）。

### P4 存储网络（Tom's Simple Storage 思路）

- **枢纽（Storage Hub）**：从枢纽出发做 BFS，把**面对面贴着**的容器（原版木箱、分级箱子、抽屉）
  连成一片，上限 128 个。不需要新管道、新贴图。
  *原设计是沿原版电线遍历（`IElectricElementBlock`）；实现时改成相邻聚类，理由见 ROADMAP M4 的偏差说明。*
- **终端（Terminal）**：搜索 + 排序 + 取放。搜索语法按 SC 改造：
  `空格` 与、`|` 或、`#类别`（BlocksManager 的 Category）、`@容器名`（容器 label）、其余匹配显示名。
  排序沿用 P1 的 `SortMethod`。
- **合成终端**：终端里放 3×3 合成网格，配方直接查 `CraftingRecipesManager`，缺料从网络自动抽取。
- **刷新**：终端打开期间每 0.4 秒重算一次汇总（不是每帧——SCTM 的性能教训是每帧全量扫描会要命）。
  更省的增量刷新（抄 `InventoryChangeTracker` 的脏标记）留到有真机性能数据之后再做。
- **无线终端**：手持物品打开最近已注册枢纽的终端，受距离/维度限制。
- **自动化件**：库存漏斗（按过滤器在两个容器间搬运，走电路信号触发）、等级发射器（数量阈值 → 电信号）。

### P5 大容量（原"抽屉"方案已废弃）

一开始按 Storage Drawers 做了单品大容量的抽屉方块。实机试完的结论是**不需要多一种方块**——
真正想要的只是"一个格子能装更多"。所以抽屉整套删掉，改成**箱子每格容量随档位放大**：

| 档 | 每格容量 | 折合圆石/格 |
|---|---|---|
| 木箱（原版） | ×1 | 40 |
| 铜箱 | ×2 | 80 |
| 铁箱 | ×4 | 160 |
| 钻石箱 | ×16 | 640 |

钻石箱 80 格 × 640 = 51200 个圆石一箱，比原来的抽屉更省地方，也不用记两套东西。

实现只是 `ComponentStashChest.GetSlotCapacity` 乘一个模板参数（`StackMultiplier`），
原版的插入/取出、联机同步、我们自己的整理与终端全都自动按这个容量走。
不可堆叠的物品（工具、衣服，`MaxStacking == 1`）不放大：耐久存在 data 位里，两把用过的镐本来就是不同物品。


## 4. 跨切能力

- **容器命名**：给箱子/抽屉起名，终端可搜。
- **容器预览 HUD**：准星指向容器时显示前 N 项（Jade 式），可关。

## 5. 能力矩阵（两版差异一览）

| 功能 | netmod | scmod |
|---|---|---|
| 整理/锁定/补货/一键存取 | ✅（服务端执行） | ✅ |
| 分级箱子（染色，零新贴图） | ✅ | ✅ |
| 背包（衣物式，可穿戴，独立贴图） | ✅ | ✅ |
| 背包鼓包模型 | v1.x（额外渲染） | v1.x（额外渲染） |
| 存储网络 + 终端 | ✅ | ✅ |
| 抽屉 | ✅ | ✅ |

## 6. 风险与对策

| 风险 | 对策 |
|---|---|
| 方块索引与其他 Mod 撞车 | 集中在一个 `BlockIndices.cs`，留一段连续区（拟 700–740），并在设置里可整体偏移 |
| 数量过大导致显示/掉落异常 | 抽屉容量封顶；UI 用 k/M 缩写；破坏时分批生成掉落物 |
| 世界文件夹回收导致注册表串档 | 记录 worldId + seed，不匹配则拒绝加载并另存备份（SCTM 已踩过这个坑） |
| 创造模式复制储物袋导致内容共享（B 方案） | 打开时写时复制；同 ID 多引用检测 |
| 网络包体积/频率 | 只发差分（SlotPlan），终端增量刷新，搜索在客户端本地做 |
| 服务端反作弊踢人 | 所有操作走自定义包并复用原版校验（所有权、槽位范围、创造物品） |
| 排序被误用（弄乱玩家布局） | 锁定槽位 + 记忆槽位 + 撤销一次（保留上一次布局快照） |

## 7. 明确不做

管道/导管物理、流体与能量系统、JEI 兼容层、跨维度无限无线（SC 没有维度概念）、
自动合成 CPU（v2 再议）。
