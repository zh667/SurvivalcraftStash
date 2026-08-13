# Stash — Survivalcraft 收纳与物流套件

把**行囊、分级箱子、一键整理、存储网络、分级熔炉、配方浏览器**做成一套连贯的东西，
而不是几个互不相干的功能堆在一起。

设计参考了 Minecraft 的 Sophisticated Backpacks / Core、Tom's Simple Storage、Iron Chests、
Inventory Profiles Next、JEI（均已反编译研读，见 [docs/MC-REFERENCE.md](docs/MC-REFERENCE.md)），
但**实现完全按 Survivalcraft 自己的机制来做**——SC 没有 NBT、没有管道、是服务端权威的，
照搬只会做出四不像。踩过的平台坑都记在 [docs/SC-PLATFORM.md](docs/SC-PLATFORM.md)。

> **当前状态：[v0.1.0](https://github.com/zh667/SurvivalcraftStash/releases/latest) 已发布。**
> 两版均可构建，86 个单测通过，**单人模式已在真机反复验证**（联机版和插件版都测过）。
> **联机客户端那条代码路径尚未实机验证**——原因见下面的「联机注意事项」。

## 安装

到 [Releases](https://github.com/zh667/SurvivalcraftStash/releases/latest) 下载：

| 文件 | 放哪 |
|---|---|
| `SurvivalcraftStash.netmod` | 联机版游戏目录的 `NetMods/` |
| `SurvivalcraftStash.scmod` | 插件版游戏目录的 `Mods/`（需要 SCAPI 1.9.2.1） |

## 功能

| | 内容 | 灵感来源 |
|---|---|---|
| **行囊** | 穿在躯干最外层，铜 16 / 铁 24 / 钻石 32 格。可与护甲共存、不磨损，`B` 键打开 | Sophisticated Backpacks |
| **分级箱子** | 铜 32 / 铁 48 / 钻石 80 格，每格堆叠上限同时放大 ×2 / ×4 / ×16。升级件就地升级，内容不丢 | Iron Chests + Storage Drawers |
| **一键整理** | 物品栏 / 行囊 / 箱子界面上的整理按钮，锁定槽位、记忆位置、一键存入取出、撤销 | Inventory Profiles Next |
| **存储网络** | 存储终端把贴着它的容器连成一片，统一检索取放。支持拼音和英文搜索 | Tom's Simple Storage |
| **无线终端** | 右键一次存储终端就绑定，之后对着空处右键远程打开 | Tom's Simple Storage |
| **分级熔炉** | 铜 / 铁 / 钻石三档，冶炼速度 2 / 4 / 8 倍。**耗燃料和原版一样**，只是等得短了 | — |
| **配方浏览器** | 列出全部物品、可搜索；点材料能逐层跳进去看它怎么做并返回；**一键填材料** | JEI |
| **无线合成终端** | 无线终端 + 3×3 合成格，取料范围是**整个存储网络 + 物品栏 + 行囊**。合成时保留原来的绑定 | JEI + Tom's |

### 还没做

- 行囊的升级槽与功能件（拾取 / 磁铁 / 过滤 / 垃圾）、背上的鼓包模型
- 库存漏斗、等级发射器等自动化件
- 终端的增量刷新（现在是打开界面后每 0.4 秒重扫一次）
- 容器命名 UI

细节见 [docs/ROADMAP.md](docs/ROADMAP.md)。

## ⚠️ 联机注意事项

**联机路径没有经过实机测试。** 单人世界和联机主机走的是同一套代码、已经测透了；
但「联机客户端」是**另一条代码路径**，作者没有第二台设备去验证它。

- 单人世界在引擎里是 `WorkType.Local`，所有「我是客户端」的分支**一行都不会执行**，
  所以「单机没问题」对联机不构成任何保证。
- 涉及的功能：**填材料、一键整理、一键存取、无线合成终端的 3×3**。这几样在客户端上都是
  「把计划发给服务端，服务端校验守恒后落地」——链路本身有防作弊校验（只许重排、不许凭空增减），
  逻辑上安全，但没在真实网络环境下跑过。
- 分级熔炉在联机下**不会有火焰粒子、方块也不发光**。这是设计取舍不是 bug：
  原版的火焰认死了原版熔炉的方块编号，换过去整个炉子会被原版逻辑改回普通熔炉
  （详见 [docs/SC-PLATFORM.md](docs/SC-PLATFORM.md) 5.11）。

服务器上线前建议先在测试房试一遍填材料和合成终端。

## 反馈

Mod 会往 `Game.log` 写诊断信息，统一前缀 `[Stash诊断]`，只在状态变化时打
（熔炉那条每档取 3 条样本就闭嘴，一小时正常游玩大概几十 KB）。

提 issue 时请附**整份 `Game.log`** + 截图 + 一句话说明当时在干什么。
日志里记了进出世界、方块索引、填材料的完整计划、熔炉升级前后的内容、
合成终端取产物时材料有没有被扣——绝大多数问题靠这些能直接定位，不用再复现一遍。

> 联机版的日志超过 10 MB 会被游戏**直接清空且不留备份**，发现问题请尽早拷出来。

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
dotnet build src/SurvivalcraftStash/SurvivalcraftStash.csproj -c Release
# 或指定目录
dotnet build src/SurvivalcraftStash/SurvivalcraftStash.csproj -c Release -p:SurvivalcraftDir=/path/to/game/

# 插件版：走 NuGet，构建后自动打包出 .scmod
dotnet build plugin/SurvivalcraftStash/SurvivalcraftStash.csproj -c Release
```

打包产物统一落在 `artifacts/`。方块贴图是 `tools/gen_textures.py` 生成的
（纯标准库，不依赖 Pillow），**改配色和形状去改那个脚本，别手改 PNG**。

## 目录

```
artifacts/  构建出来的 .netmod / .scmod（不入库）
docs/       设计与调研：平台事实(SC-PLATFORM) / 参考模组笔记(MC-REFERENCE)
            / 设计(DESIGN) / 路线图(ROADMAP) / 实机测试清单(TESTING)
shared/     Stash.Shared（纯逻辑，可单测）、Stash.Game（依赖游戏但两版通用）
src/        联机版 netmod
plugin/     插件版 scmod
tests/      单测（只吃 Stash.Shared）
tools/      贴图生成脚本
```

## 许可

木兰宽松许可证第 2 版（Mulan PSL v2），见 [LICENSE](LICENSE)。
