using System.Xml.Linq;
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
        ModsManager.RegisterHook("OnXdbLoad", this);
        ModsManager.RegisterHook("OnModalPanelWidgetSet", this);
        ModsManager.RegisterHook("UpdateInput", this);
        ModsManager.RegisterHook("OnLoadingFinished", this);
        ModsManager.RegisterHook("GuiUpdate", this);

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

    /// <summary>把背包组件挂到玩家实体上。走代码而不是 .xdb，原因见 StashDatabaseInjector。</summary>
    public override void OnXdbLoad(XElement xElement) => StashDatabaseInjector.Inject(xElement);

    public override void UpdateInput(ComponentInput componentInput, WidgetInput widgetInput) =>
        StashHotkeys.Update(componentInput, widgetInput);

    /// <summary>给背包格子改妆：图标从背后取景、不显示耐久条。内部有节流，不是每帧都扫。</summary>
    public override void GuiUpdate(ComponentGui componentGui) => StashSlotDresser.Update(componentGui);

    public override void OnModalPanelWidgetSet(ComponentGui gui, Widget oldWidget, Widget newWidget) =>
        StashUiInjector.OnModalPanelChanged(gui, oldWidget, newWidget);

    /// <summary>
    /// ─────────────────────────────────────────────────────────────────────────
    /// **<c>OnLoadingFinished</c> 不是"进入世界"，是"应用启动时 mod 加载完毕"，
    /// 一个进程只触发一次。**
    ///
    /// 实机日志戳穿了这一点：同一次启动里进了 5 次世界（`Entered screen "Game"` 五次），
    /// 但我们的每局初始化只跑了 1 次。而且这个时刻 <c>Project</c> 根本还没建起来——
    /// 紧随其后的是 `Entered screen "MainMenu"`，所以
    /// `[Stash] OnLoadingFinished 时还拿不到 SubsystemGameInfo` 那条警告每次启动都出现，
    /// 它不是偶发，是必然。
    ///
    /// 后果不止是日志少几行：<c>StashUiInjector.Reset()</c>（连带清配方索引、清残留浮层）
    /// 和 <c>StashStore.Load()</c> 都挂在这里，换世界时**一次都不会重来**。
    ///
    /// 真正的每局钩子是 <c>GameManager.ProjectLoaded</c>——它在 Project 构造完成、
    /// 子系统和实体都装好之后触发（GameManager.cs:186 / 219，单机和联机客户端两条路都有）。
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public override void OnLoadingFinished(List<Action> actions)
    {
        GameManager.ProjectLoaded -= OnProjectLoaded;
        GameManager.ProjectLoaded += OnProjectLoaded;

        // 联机版的 ModLoader 没有"世界保存"钩子，所以退出世界时收尾。
        // 平时的改动（锁定槽位等）在改的时候就落盘，文件很小，代价可以忽略。
        GameManager.ProjectDisposed -= OnProjectDisposed;
        GameManager.ProjectDisposed += OnProjectDisposed;
    }

    private static void OnProjectLoaded(Project project)
    {
        SubsystemGameInfo? gameInfo = project.FindSubsystem<SubsystemGameInfo>(throwOnError: false);
        if (gameInfo != null)
        {
            StashStore.Load(gameInfo);
        }
        else
        {
            // StashStore 会在第一次读写时自己补读，这里只留个记号。
            Log.Warning("[Stash] 进世界时拿不到 SubsystemGameInfo，世界数据推迟到首次读写时再读。");
        }

        StashUiInjector.Reset();
        StashSelfCheck.Run();
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
