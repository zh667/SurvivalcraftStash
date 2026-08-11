using Engine;
using GameEntitySystem;
using Stash.Game;

namespace Game;

/// <summary>
/// 插件版（scmod，SurvivalcraftApi 1.9.2.1）入口。
/// </summary>
public class StashModLoader : ModLoader
{
    public const string Version = "0.1.0";

    public override void __ModInitialize()
    {
        StashPlatform.Register(new ScmodStashPlatform());

        // 钩子必须显式注册，否则 override 不会被调用。
        ModsManager.RegisterHook("OnModalPanelWidgetSet", this);
        ModsManager.RegisterHook("OnProjectLoaded", this);
        ModsManager.RegisterHook("OnProjectDisposed", this);
        ModsManager.RegisterHook("ClothingWidgetOpen", this);
        Log.Information($"[Stash] scmod {Version} 已加载");
    }

    public override void ClothingWidgetOpen(ComponentGui componentGui, ClothingWidget clothingWidget) =>
        StashClothingButton.Attach(componentGui, clothingWidget);

    public override void OnModalPanelWidgetSet(ComponentGui componentGui, Widget oldWidget, Widget newWidget) =>
        StashUiInjector.OnModalPanelChanged(componentGui, oldWidget, newWidget);

    public override void OnProjectLoaded(Project project)
    {
        SubsystemGameInfo? gameInfo = project?.FindSubsystem<SubsystemGameInfo>();
        if (gameInfo != null)
        {
            StashStore.Load(gameInfo);
        }

        StashUiInjector.Reset();
    }

    public override void OnProjectDisposed()
    {
        StashStore.Save();
        StashStore.Unload();
        StashUiInjector.Reset();
    }
}
