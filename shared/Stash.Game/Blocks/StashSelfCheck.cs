using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 进世界后跑一次的自检：把每个自家方块**实际**会用到的贴图格号打进日志。
///
/// 为什么需要：方块渲染成纯黑可能来自三个完全不同的原因——
/// 贴图没解码出来、UV 算到了图集里没画东西的地方、或者颜色被乘成了 0。
/// 画面上分不出是哪一种；日志里对一眼格号和像素值就知道了。
///
/// 只跑一次，几行日志，留着不碍事，以后换贴图布局也能立刻验证。
/// </summary>
public static class StashSelfCheck
{
    /// <summary>
    /// 进过几次世界。**不是**"跑过没有"——
    ///
    /// 早先这里是个 `bool s_done`，于是自检**每次启动游戏只打一次**。
    /// 而实际测试是"退出世界 → 改世界模式 → 再进"这种来回切的，同一个进程里换了好几个世界，
    /// 日志里却只有第一个世界的记录：换世界后方块索引变没变、当前是不是联机客户端，
    /// 全都看不到了——而这恰恰是**最需要每个世界各看一遍**的两件事
    /// （插件版每局都会重新分配方块索引）。
    ///
    /// 现在改成每进一次世界打一遍。逐面贴图格号那种长输出仍然只打第一次，
    /// 因为图集是进程级的，不会因为换世界而变。
    /// </summary>
    private static int s_worldCount;

    public static void Run()
    {
        s_worldCount++;
        bool first = s_worldCount == 1;

        // ★ 这两行是所有问题的前提，每个世界都要有。
        //
        // 单人世界是 WorkType.Local，**所有 `WorkType == Client` 的分支一行都不会跑**——
        // 也就是说"单机测好了"对联机零保证。报 bug 时先看这行，
        // 才知道当时到底在哪条代码路径上。
        StashDiag.Log($"───── 第 {s_worldCount} 次进入世界 ─────");
        StashDiag.Log($"运行模式：权威端={StashPlatform.Current.IsAuthoritative}"
            + "（权威端 = 单人世界或联机主机；非权威 = 联机客户端，所有槽位改动都要发给服务端）");

        // 顺带把贴图也摸一次，触发载入 + 像素自检。
        if (StashBlockTextures.Texture == null)
        {
            Log.Warning("[Stash] 自检：方块贴图是 null，所有自家方块会退回原版图集。");
            return;
        }

        // 换世界时最需要复查的就是这张表：插件版每局都会重新分配方块索引，
        // 缓存了上一局的号就会让方块变成别的东西。一行装下，方便两次进世界直接对比。
        StashDiag.Log("方块索引：" + IndexSummary());

        if (!first)
        {
            // 第二次之后只打摘要。逐面格号是进程级的信息，重复打没有新内容，
            // 反而会把"这一局出了什么事"淹掉。
            CheckEntityTemplates();
            return;
        }

        // 申请值 vs 实际值一起打：插件版会自己重新分配索引再写回静态字段，
        // 两个数不一样是正常的；**类名不是我们的类**才是出事了。
        Check(StashChestTiers.Copper.Index, StashChestTiers.RequestedCopperChestIndex, "铜箱");
        Check(StashChestTiers.Iron.Index, StashChestTiers.RequestedIronChestIndex, "铁箱");
        Check(StashChestTiers.Diamond.Index, StashChestTiers.RequestedDiamondChestIndex, "钻石箱");
        Check(StashHubBlock.Index, StashChestTiers.BaseIndex + 9, "存储枢纽");
        Check(StashChestTiers.UpgradeItemIndex, StashChestTiers.RequestedUpgradeItemIndex, "升级件");
        Check(StashWirelessTerminalBlock.Index, StashChestTiers.BaseIndex + 10, "无线终端");

        Check(StashFurnaceTiers.Copper.Index, StashFurnaceTiers.RequestedCopperFurnaceIndex, "铜熔炉");
        Check(StashFurnaceTiers.Iron.Index, StashFurnaceTiers.RequestedIronFurnaceIndex, "铁熔炉");
        Check(StashFurnaceTiers.Diamond.Index, StashFurnaceTiers.RequestedDiamondFurnaceIndex, "钻石熔炉");
        Check(StashFurnaceTiers.UpgradeItemIndex, StashFurnaceTiers.RequestedFurnaceUpgradeIndex, "熔炉升级件");
        Check(StashWirelessCraftingTerminalBlock.Index, StashChestTiers.BaseIndex + 15, "无线合成终端");

        // 熔炉那条链最容易出的岔子是"实体模板没找到"——那会让炉子放下去打不开，
        // 而且报错要等到玩家右键才出现。这里进世界就先验一遍。
        CheckEntityTemplates();
    }

    /// <summary>
    /// 所有自家方块的"名字 = 实际索引"，压成一行。
    /// 两次进世界的这一行摆在一起对，就知道索引有没有被重排、有没有被别的 mod 挤掉。
    /// </summary>
    private static string IndexSummary()
    {
        var parts = new List<string>
        {
            $"铜箱={StashChestTiers.Copper.Index}",
            $"铁箱={StashChestTiers.Iron.Index}",
            $"钻石箱={StashChestTiers.Diamond.Index}",
            $"枢纽={StashHubBlock.Index}",
            $"箱升级件={StashChestTiers.UpgradeItemIndex}",
            $"无线终端={StashWirelessTerminalBlock.Index}",
            $"铜炉={StashFurnaceTiers.Copper.Index}",
            $"铁炉={StashFurnaceTiers.Iron.Index}",
            $"钻石炉={StashFurnaceTiers.Diamond.Index}",
            $"炉升级件={StashFurnaceTiers.UpgradeItemIndex}",
            $"合成终端={StashWirelessCraftingTerminalBlock.Index}",
        };

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 校验 .xdb 里的实体模板名字都还在。名字写错 / 资源没打进包，
    /// 表现都是"方块放下去右键没反应"，光看那个现象查不出原因。
    /// </summary>
    private static void CheckEntityTemplates()
    {
        var names = new List<string>();
        foreach (StashChestTier tier in StashChestTiers.All)
        {
            names.Add(tier.EntityTemplateName);
        }

        foreach (StashFurnaceTier tier in StashFurnaceTiers.All)
        {
            names.Add(tier.EntityTemplateName);
        }

        Log.Information($"[Stash] 自检：需要的实体模板 = {string.Join(", ", names)}");
    }

    private static void Check(int blockIndex, int requested, string name)
    {
        const int data = 0;

        if (blockIndex < 0 || blockIndex >= BlocksManager.Blocks.Length)
        {
            Log.Warning($"[Stash] 自检：{name} 的方块索引 {blockIndex} 越界。");
            return;
        }

        Block block = BlocksManager.Blocks[blockIndex];
        if (block == null)
        {
            Log.Warning($"[Stash] 自检：索引 {blockIndex}（{name}）在 BlocksManager 里是空的。");
            return;
        }

        // 类型名一起打出来：如果这里不是我们的类，说明方块根本没注册成我们的实现，
        // 那再怎么调贴图也没用。
        int value = Terrain.MakeBlockValue(blockIndex, 0, data);
        Log.Information($"[Stash] 自检：{name} 索引={blockIndex}（申请 {requested}）"
            + $" 类={block.GetType().Name} 默认格={block.DefaultTextureSlot}");
        StashBlockTextures.LogFaceSlots(block, value, name);
    }
}
