using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 准星指着分级箱子时，在屏幕上提示里面装了几种东西、占了多少格。
/// 不用挨个打开就能大致知道哪个箱子还有空位。
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
        StashChestTier? tier = StashChestTiers.ByBlockIndex(contents);
        if (tier == null)
        {
            return string.Empty;
        }

        SubsystemBlockEntities? blockEntities = player.Project.FindSubsystem<SubsystemBlockEntities>();
        ComponentBlockEntity? blockEntity = blockEntities?.GetBlockEntity(
            result.CellFace.X, result.CellFace.Y, result.CellFace.Z);
        var chest = blockEntity?.Entity.FindComponent<ComponentStashChest>(throwOnError: false);
        if (chest == null)
        {
            return string.Empty;
        }

        int used = 0;
        long total = 0;
        for (int slot = 0; slot < chest.SlotsCount; slot++)
        {
            int count = chest.GetSlotCount(slot);
            if (count > 0)
            {
                used++;
                total += count;
            }
        }

        return used == 0
            ? StashText.ChestEmpty(tier)
            : StashText.ChestSummary(tier, used, chest.SlotsCount, total);
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
