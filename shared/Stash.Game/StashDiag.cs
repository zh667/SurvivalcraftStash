using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 诊断日志。所有"实机才能验证"的路径都往这里打，统一前缀 <c>[Stash诊断]</c>，
/// 玩家只要 grep 这一个词就能把一局里所有可疑事件拉出来。
///
/// **打日志的规矩**（血的教训：上一版有一处每帧打一行，Game.log 十分钟涨到 200MB）：
/// <list type="bullet">
/// <item>只在**状态发生变化**时打，不在 Update 里无条件打。</item>
/// <item>每条都要带上"判断对错所需的全部数字"——光说"失败了"等于没打。</item>
/// <item>重复的同类事件用 <see cref="Once"/> 压掉，只留第一条。</item>
/// </list>
/// </summary>
public static class StashDiag
{
    private static readonly HashSet<string> s_seen = new();

    private static readonly Dictionary<string, int> s_counts = new();

    public static void Log(string message) => Engine.Log.Information($"[Stash诊断] {message}");

    public static void Warn(string message) => Engine.Log.Warning($"[Stash诊断] {message}");

    /// <summary>同一个 key 只打一次。用于"每次都会发生但只需要知道一次"的事实。</summary>
    public static void Once(string key, string message)
    {
        if (s_seen.Add(key))
        {
            Log(message);
        }
    }

    /// <summary>
    /// 采样：同一个 key 只放行前 <paramref name="max"/> 次。
    ///
    /// 给"随游戏时间不断发生"的事件用（<see cref="Once"/> 太少，不限又会淹了日志）。
    /// 典型是熔炉烧完一件：钻石炉 0.8 秒一件，**一台就是每小时 630 KB**，
    /// 十台能在两小时内把联机版 10 MB 的日志上限撑爆——而联机版满了是**直接清空、不留备份**，
    /// 等于把玩家真正要报的那个 bug 的现场一起冲掉。
    /// 验证倍率对不对，每档取几条样本就够了。
    /// </summary>
    /// <returns>是否放行；<c>false</c> 表示这次不要打。</returns>
    public static bool Sample(string key, int max)
    {
        int used = s_counts.GetValueOrDefault(key);
        if (used >= max)
        {
            return false;
        }

        s_counts[key] = used + 1;
        return true;
    }

    /// <summary>某个 key 是不是刚好用完最后一次配额——用来在末尾补一句"后面不打了"。</summary>
    public static bool JustExhausted(string key, int max) => s_counts.GetValueOrDefault(key) == max;

    /// <summary>换世界时清掉，新的一局重新打一遍。</summary>
    public static void Reset()
    {
        s_seen.Clear();
        s_counts.Clear();
    }

    /// <summary>
    /// 把一块库存的内容压成一行，形如 <c>3:铁锭×5, 7:煤×12</c>。
    /// 升级、搬运这类"前后要对得上"的操作，前后各打一行就能一眼看出丢没丢东西。
    /// </summary>
    public static string Describe(IInventory? inventory, int firstSlot = 0, int slotCount = int.MaxValue)
    {
        if (inventory == null)
        {
            return "(空)";
        }

        var parts = new List<string>();
        int total = 0;
        int last = MathUtils.Min(inventory.SlotsCount, firstSlot + slotCount);

        for (int slot = firstSlot; slot < last; slot++)
        {
            int count = inventory.GetSlotCount(slot);
            if (count <= 0)
            {
                continue;
            }

            total += count;
            parts.Add($"{slot}:{Name(inventory.GetSlotValue(slot))}×{count}");
        }

        return parts.Count == 0 ? "(空)" : $"{string.Join(", ", parts)}（共 {total} 件）";
    }

    /// <summary>
    /// 库存的网络编号。<c>IInventory.Id</c> 是**联机版独有的**
    /// （插件版是单机，没有"把库存发给服务端"这回事，接口里根本没这个成员）。
    /// 插件版统一返回 -1。
    /// </summary>
    public static int InventoryId(IInventory inventory)
    {
#if STASH_SCMOD
        return -1;
#else
        return inventory?.Id ?? -1;
#endif
    }

    /// <summary>物品值 → 可读名字。取不到就退回原始数值，绝不抛异常。</summary>
    public static string Name(int value)
    {
        try
        {
            int contents = Terrain.ExtractContents(value);
            if (contents <= 0 || contents >= BlocksManager.Blocks.Length)
            {
                return $"?{value}";
            }

            return BlocksManager.Blocks[contents].GetDisplayName(null!, value) ?? $"?{value}";
        }
        catch
        {
            return $"?{value}";
        }
    }
}
