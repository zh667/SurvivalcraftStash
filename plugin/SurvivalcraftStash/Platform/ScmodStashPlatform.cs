using Stash.Game;
using Stash.Shared.Storage;

namespace Game;

/// <summary>
/// 插件版平台适配。单人游戏，本进程就是权威方，计划直接落地。
/// </summary>
public sealed class ScmodStashPlatform : IStashPlatform
{
    public bool IsAuthoritative => true;

    public string CurrentPlayerKey => StashWorldData.SinglePlayerKey;

    public void Execute(StashPlan plan)
    {
        foreach ((IInventory inventory, var assignments) in plan.Parts)
        {
            GameInventory.Apply(inventory, assignments);
        }
    }

    public void Log(string message) => Engine.Log.Information($"[Stash] {message}");
}
