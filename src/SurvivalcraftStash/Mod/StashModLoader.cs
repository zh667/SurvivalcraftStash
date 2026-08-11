using Engine;
using Game.NetWork;
using GameEntitySystem;
using Stash.Game;

namespace Game;

/// <summary>
/// 联机版（netmod）入口。
/// </summary>
public class StashModLoader : ModLoader
{
    public const string Version = "0.1.0";

    private static readonly NetmodStashPlatform s_platform = new();

    public override void __ModInitialize()
    {
        StashPlatform.Register(s_platform);

        try
        {
            PackageManager.RegisterPackage(new StashOpPackage());
            PackageManager.RegisterPackage(new StashOpenChestPackage());
        }
        catch (Exception exception)
        {
            // 包号被别的 Mod 占了。单人/主机侧仍然完全可用，只有作为客户端连别人服务器时不可用。
            s_platform.PackageAvailable = false;
            Log.Warning($"[Stash] 数据包注册失败（可能与其他 Mod 撞号）：{exception.Message}");
        }

        Log.Information($"[Stash] netmod {Version} 已加载");
    }

    public override void OnModalPanelWidgetSet(ComponentGui gui, Widget oldWidget, Widget newWidget) =>
        StashUiInjector.OnModalPanelChanged(gui, oldWidget, newWidget);

    public override void OnLoadingFinished(List<Action> actions)
    {
        actions.Add(() =>
        {
            SubsystemGameInfo? gameInfo = GameManager.Project?.FindSubsystem<SubsystemGameInfo>();
            if (gameInfo != null)
            {
                StashStore.Load(gameInfo);
                StashUiInjector.Reset();
            }
        });

        // 联机版的 ModLoader 没有"世界保存"钩子，所以退出世界时收尾。
        // 平时的改动（锁定槽位等）在改的时候就落盘，文件很小，代价可以忽略。
        GameManager.ProjectDisposed -= OnProjectDisposed;
        GameManager.ProjectDisposed += OnProjectDisposed;
    }

    private static void OnProjectDisposed(Project project)
    {
        StashStore.Save();
        StashStore.Unload();
        StashUiInjector.Reset();
    }

    public override void ModDispose()
    {
        GameManager.ProjectDisposed -= OnProjectDisposed;
        StashStore.Save();
        StashStore.Unload();
        StashUiInjector.Reset();
    }
}
