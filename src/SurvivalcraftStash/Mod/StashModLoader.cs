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

        // 钩子必须显式注册，否则 override 根本不会被调用（官方示例模组里写明了这一点）。
        // 之前漏了这一步 → 整理按钮、准星预览、世界数据加载全部没生效。
        ModsManager.RegisterHook("OnModalPanelWidgetSet", this);
        ModsManager.RegisterHook("UpdateInput", this);
        ModsManager.RegisterHook("OnLoadingFinished", this);

        // 一个一个注册：之前三个写在同一个 try 里，第一个撞号后面两个就再也没注册上。
        s_platform.PackageAvailable = Register(new StashOpPackage());
        Register(new StashOpenChestPackage());
        Register(new StashOpenTerminalPackage());
        Register(new StashWirelessPackage());

        Log.Information($"[Stash] netmod {Version} 已加载");
    }

    /// <summary>注册一个包；撞号只影响这一个功能，不连累其它包。</summary>
    private static bool Register(IPackage package)
    {
        try
        {
            PackageManager.RegisterPackage(package);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 数据包 {package.GetType().Name} 注册失败（与其他 Mod 撞号）：{exception.Message}");
            return false;
        }
    }

    public override void UpdateInput(ComponentInput componentInput, WidgetInput widgetInput) =>
        StashHotkeys.Update(componentInput, widgetInput);

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
