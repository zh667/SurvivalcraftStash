using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 准星指着容器时，在屏幕上提示里面装了什么。
///
/// 抽屉最需要这个——它的意义就是"不开界面也知道装了什么"。
/// 在方块正面直接画图标和数量要自己搭一套世界空间渲染（原版告示牌那套有 550 行），
/// 先用这个成本低得多的方式达到同样的目的，正面渲染留到后面做。
/// </summary>
public static class StashAimPreview
{
    /// <summary>提示节流：每隔这么久才重新算一次。</summary>
    private const double IntervalSeconds = 0.35;

    private static double s_nextCheckTime;
    private static string s_lastMessage = string.Empty;

    public static void Update(ComponentGui gui)
    {
        if (gui == null)
        {
            return;
        }

        ComponentPlayer? player = gui.m_componentPlayer;
        if (player?.ComponentMiner == null)
        {
            return;
        }

        // 打开着界面时不提示，免得和界面里的信息打架。
        if (gui.ModalPanelWidget != null)
        {
            s_lastMessage = string.Empty;
            return;
        }

        double now = player.Project.FindSubsystem<SubsystemTime>()?.GameTime ?? 0.0;
        if (now < s_nextCheckTime)
        {
            return;
        }

        s_nextCheckTime = now + IntervalSeconds;

        string message = Describe(player);
        if (string.IsNullOrEmpty(message) || message == s_lastMessage)
        {
            s_lastMessage = message;
            return;
        }

        s_lastMessage = message;
        gui.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
    }

    private static string Describe(ComponentPlayer player)
    {
        Matrix rotation = Matrix.CreateFromQuaternion(player.ComponentCreatureModel.EyeRotation);
        var ray = new Ray3(player.ComponentCreatureModel.EyePosition, rotation.Forward);

        if (player.ComponentMiner.Raycast(ray, RaycastMode.Interaction, raycastTerrain: true, raycastBodies: false, raycastMovingBlocks: false)
            is not TerrainRaycastResult result)
        {
            return string.Empty;
        }

        int contents = Terrain.ExtractContents(result.Value);
        if (!StashDrawerTiers.IsDrawer(contents))
        {
            return string.Empty;
        }

        SubsystemBlockEntities? blockEntities = player.Project.FindSubsystem<SubsystemBlockEntities>();
        ComponentBlockEntity? blockEntity = blockEntities?.GetBlockEntity(
            result.CellFace.X, result.CellFace.Y, result.CellFace.Z);
        var drawer = blockEntity?.Entity.FindComponent<ComponentStashDrawer>(throwOnError: false);
        if (drawer == null)
        {
            return string.Empty;
        }

        if (drawer.StoredCount <= 0)
        {
            return StashText.DrawerEmpty;
        }

        Block block = BlocksManager.Blocks[Terrain.ExtractContents(drawer.StoredValue)];
        string name = block.GetDisplayName(player.Project.FindSubsystem<SubsystemTerrain>(), drawer.StoredValue);
        return $"{name} × {FormatCount(drawer.StoredCount)} / {FormatCount(drawer.StoredCapacity)}";
    }

    /// <summary>抽屉数量能到十几万，原样显示会占掉半行，所以大数缩写。</summary>
    public static string FormatCount(int count)
    {
        if (count < 10_000)
        {
            return count.ToString();
        }

        if (count < 1_000_000)
        {
            return (count / 1000f).ToString("0.#") + "k";
        }

        return (count / 1_000_000f).ToString("0.##") + "M";
    }

    public static void Reset()
    {
        s_nextCheckTime = 0;
        s_lastMessage = string.Empty;
    }
}
