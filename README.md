# Stash — Survivalcraft 收纳与物流套件

把**背包、分级箱子、一键整理、存储网络**做成一套连贯的东西，而不是四个互不相干的功能堆在一起。
设计参考了 Minecraft 的 Sophisticated Backpacks / Core、Tom's Simple Storage、Iron Chests、
Inventory Profiles Next（均已反编译研读，见 [docs/MC-REFERENCE.md](docs/MC-REFERENCE.md)），
但**实现完全按 Survivalcraft 自己的机制来做**——SC 没有 NBT、没有管道、是服务端权威的，照搬只会做出四不像。

> 当前状态：**五大支柱都已落地**（整理 / 分级箱子 / 背包 / 抽屉 / 存储网络）。
> 两版均可构建，45 个单测通过；但**尚未在真机游戏里跑过**（开发机是 Linux，SC 只跑 Windows/安卓），
> 所以一律标注为"待真机验证"。剩余项与已知偏差见 [docs/ROADMAP.md](docs/ROADMAP.md)。

| 支柱 | 状态 |
|---|---|
| 整理（整理/一键存取/锁定槽位/撤销） | ✅ 已实现 |
| 分级箱子（铜/铁/钻石/观景 + 原地升级） | ✅ 已实现 |
| 背包（穿戴式，三档） | ✅ 主体已实现，升级件未做 |
| 抽屉（四档 + 准星预览） | ✅ 已实现 |
| 存储网络（枢纽 + 终端搜索取放） | ✅ 已实现，自动化件未做 |

## 计划中的五大支柱

| 支柱 | 内容 | 灵感来源 |
|---|---|---|
| 整理 | 所有库存界面的整理按钮、锁定槽位、记忆槽位、自动补货、一键存入/取出、撤销 | Inventory Profiles Next + Sophisticated 的 memory/nosort |
| 分级箱子 | 木/铜/铁/钻石/观景 五档（按 SC 真实矿物，原版没有金），升级件原地升级、内容不丢 | Iron Chests |
| 背包 | 三档可穿戴背包（拖到人身上即穿戴）+ 升级槽（拾取/磁铁/过滤/自动存取/垃圾…） | Sophisticated Backpacks + Core |
| 存储网络 | 枢纽把贴着的容器聚成一片、终端搜索取放（合成终端/无线终端待做） | Tom's Simple Storage |
| 抽屉 | 单品大容量（最高 4096 组）、正面显示数量、接入网络 | Storage Drawers（四个参考模组都没覆盖的空白） |

外加：容器命名与搜索、容器预览 HUD。

## 两个版本

| | 联机版 `.netmod` | 插件版 `.scmod` |
|---|---|---|
| 目标 | Survivalcraft 联机版，NetMod API `1.44` | SurvivalcraftApi `1.9.2.1` |
| 工程 | `src/SurvivalcraftStash` | `plugin/SurvivalcraftStash` |
| 特有能力 | 服务端权威执行、自定义网络包 | 逐方块自定义贴图、自定义衣物槽、Harmony |

两版共用 `shared/` 下的核心代码（**编译进两边**，不是各存一份），能力差异用降级策略处理，
完整对照见 [docs/DESIGN.md](docs/DESIGN.md) 的能力矩阵。

## 构建

```bash
# 单测（纯 C#，不需要游戏程序集）
dotnet test tests/SurvivalcraftStash.Tests/SurvivalcraftStash.Tests.csproj

# 联机版：需要游戏程序集，默认探测 ~/sc-libs/
dotnet build src/SurvivalcraftStash/SurvivalcraftStash.csproj
# 或指定目录
dotnet build src/SurvivalcraftStash/SurvivalcraftStash.csproj -p:SurvivalcraftDir=/path/to/game/

# 插件版：走 NuGet，构建后自动打包出 .scmod
dotnet build plugin/SurvivalcraftStash/SurvivalcraftStash.csproj
```

## 目录

```
docs/       设计与调研（平台事实 / 参考模组笔记 / 设计 / 路线图）
shared/     Stash.Shared（纯逻辑，可单测）、Stash.Game（依赖游戏但两版通用）
src/        联机版 netmod
plugin/     插件版 scmod
tests/      单测（只吃 Stash.Shared）
```

## 许可

木兰宽松许可证第 2 版（Mulan PSL v2），见 [LICENSE](LICENSE)。
