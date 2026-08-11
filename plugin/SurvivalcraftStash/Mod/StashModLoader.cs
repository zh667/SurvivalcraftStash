using Engine;

namespace Game;

/// <summary>
/// 插件版（scmod，SurvivalcraftApi 1.9.2.1）入口。
/// 现阶段只做加载自检。插件版独有的能力（自定义方块贴图、自定义衣物槽、Harmony）
/// 在对应里程碑接入，见 docs/DESIGN.md 的能力矩阵。
/// </summary>
public class StashModLoader : ModLoader
{
    public const string Version = "0.1.0";

    public override void __ModInitialize()
    {
        Log.Information($"[Stash] scmod {Version} 已加载");
    }
}
