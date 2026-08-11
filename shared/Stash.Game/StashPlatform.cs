using Game;
using Stash.Shared.Inventory;

namespace Stash.Game;

/// <summary>一次操作要落地的所有槽位变更，按库存分组。</summary>
public sealed class StashPlan
{
    public List<(IInventory Inventory, IReadOnlyList<SlotAssignment> Assignments)> Parts { get; } = new();

    public bool IsEmpty => Parts.Count == 0 || Parts.TrueForAll(p => p.Assignments.Count == 0);

    public void Add(IInventory inventory, IReadOnlyList<SlotAssignment> assignments)
    {
        if (assignments.Count > 0)
        {
            Parts.Add((inventory, assignments));
        }
    }
}

/// <summary>
/// 平台差异都收在这一个接口里：联机版要把计划发给服务端执行，插件版直接落地。
/// 共享层与 UI 只认这个接口。
/// </summary>
public interface IStashPlatform
{
    /// <summary>当前进程能不能直接改槽位（单人版、或联机版的服务端一侧）。</summary>
    bool IsAuthoritative { get; }

    /// <summary>执行计划。非权威端的实现应当把计划发给服务端，而不是本地改。</summary>
    void Execute(StashPlan plan);

    /// <summary>当前操作者的标识（联机版用玩家 GUID，单人版固定值）。</summary>
    string CurrentPlayerKey { get; }

    void Log(string message);
}

public static class StashPlatform
{
    private static IStashPlatform? s_current;

    public static IStashPlatform Current =>
        s_current ?? throw new InvalidOperationException("Stash 平台适配器尚未注册。");

    public static bool IsReady => s_current != null;

    public static void Register(IStashPlatform platform) => s_current = platform;

    public static void Reset() => s_current = null;
}
