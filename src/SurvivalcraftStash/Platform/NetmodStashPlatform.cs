using Engine;
using Game.NetWork;
using Stash.Game;
using Stash.Shared.Storage;

namespace Game;

/// <summary>
/// 联机版平台适配。
///
/// 判据是 <c>CommonLib.WorkType</c>：
/// - <c>Server</c> / <c>Local</c>：本进程就是权威方，直接落地（单人世界也走这条）。
/// - <c>Client</c>：把计划发给服务端，等原版的 InventorySync 推回结果。
/// </summary>
public sealed class NetmodStashPlatform : IStashPlatform
{
    /// <summary>包注册失败（撞号）时置为 false，此时客户端只能提示不可用，而不是偷偷本地改。</summary>
    public bool PackageAvailable { get; set; } = true;

    public bool IsAuthoritative => CommonLib.WorkType != WorkType.Client;

    public string CurrentPlayerKey
    {
        get
        {
            try
            {
                ComponentPlayer player = CommonLib.MainPlayer;
                string? guid = player?.PlayerGuid.ToString();
                return string.IsNullOrEmpty(guid) ? StashWorldData.SinglePlayerKey : guid;
            }
            catch
            {
                return StashWorldData.SinglePlayerKey;
            }
        }
    }

    public void Execute(StashPlan plan)
    {
        if (plan.IsEmpty)
        {
            return;
        }

        if (IsAuthoritative)
        {
            foreach ((IInventory inventory, var assignments) in plan.Parts)
            {
                GameInventory.Apply(inventory, assignments);
            }

            return;
        }

        if (!PackageAvailable)
        {
            Engine.Log.Warning("[Stash] 数据包不可用（包号冲突），联机模式下无法执行整理。");
            return;
        }

        CommonLib.Net.QueuePackage(new StashOpPackage(plan));
    }

    public void Log(string message) => Engine.Log.Information($"[Stash] {message}");
}
