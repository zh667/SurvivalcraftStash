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
    private static bool s_done;

    public static void Run()
    {
        if (s_done)
        {
            return;
        }

        s_done = true;

        // 顺带把贴图也摸一次，触发载入 + 像素自检。
        if (StashBlockTextures.Texture == null)
        {
            Log.Warning("[Stash] 自检：方块贴图是 null，所有自家方块会退回原版图集。");
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
