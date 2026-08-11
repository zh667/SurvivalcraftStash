using Engine;

namespace Game;

/// <summary>
/// 联机版（netmod）入口。
/// 现阶段只做加载自检：确认程序集能被 ModsManager 装起来、共享层可用。
/// 功能按 docs/ROADMAP.md 的里程碑逐步接进来。
/// </summary>
public class StashModLoader : ModLoader
{
    public const string Version = "0.1.0";

    public override void __ModInitialize()
    {
        Log.Information($"[Stash] netmod {Version} 已加载");

        // M1 起：在这里注册自定义包（PackageManager.RegisterPackage），
        //        并在 OnWidgetConstruct / OnModalPanelWidgetSet 里注入整理按钮。
    }
}
