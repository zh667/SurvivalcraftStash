using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 我们自己的方块图集：<c>Assets/Textures/Stash/Blocks.png</c>，512×512 = 16×16 格，每格 32×32——
/// **和原版图集同规格**。
///
/// **为什么可以有自己的图集**（一开始我以为不行，翻了本体才发现能）：
/// 地形几何是**按贴图分批**的——
/// <code>
/// public Dictionary&lt;Texture2D, TerrainGeometry[]&gt; Draws;
/// public TerrainGeometry GetGeometry(Texture2D texture);
/// </code>
/// 方块在 <c>GenerateTerrainVertices</c> 里把顶点写进 <c>geometry.GetGeometry(Texture)</c> 的子集，
/// 那批就用我们的贴图渲染。联机版和插件版（SCAPI 1.9.2.1）都有这个方法。
///
/// 所以不用去动全局的 <c>Textures/Blocks</c>——那张图玩家的材质包也在改，碰了必然打架。
///
/// UV 是 <c>BlocksManager</c> 按 <c>格号 % 列数 / 格号 / 列数</c> 算的，列数取自
/// <c>Block.GetTextureSlotCount()</c>，默认 16。
///
/// **一定要用 16 列。** 第一版做成 8 列 + 覆写 <c>GetTextureSlotCount</c>，实机六个方块全黑：
/// 只要有任何一条路径没走到那个覆写（例如按 BlocksData 里的 <c>DefaultTextureSlot</c> 取格），
/// 采样点就落到图集里没画东西的地方，而**透明像素在不透明批次里就是纯黑**。
/// 现在列数与原版一致、覆写全部去掉，并且每个方块的 <c>DefaultTextureSlot</c> 直接指向自己的正面格，
/// 就算 <c>GetFaceTextureSlot</c> 因故没生效，也只是六面同图，不会变黑。
///
/// 贴图本身是 <c>tools/gen_textures.py</c> 生成的，改配色/形状去改那个脚本，别手改 PNG。
/// </summary>
public static class StashBlockTextures
{
    /// <summary>图集一行几格。必须等于原版默认值 16，改这个要同步改 gen_textures.py 的 COLS。</summary>
    public const int SlotCount = 16;

    public const int CopperChestFront = 0;
    public const int CopperChestSide = 1;
    public const int CopperChestTop = 2;

    public const int IronChestFront = 3;
    public const int IronChestSide = 4;
    public const int IronChestTop = 5;

    public const int DiamondChestFront = 6;
    public const int DiamondChestSide = 7;
    public const int DiamondChestTop = 8;

    public const int HubFront = 9;
    public const int HubSide = 10;
    public const int HubTop = 11;

    /// <summary>升级件三档，格号 = 12 + 升级链下标。</summary>
    public const int UpgradeFirst = 12;

    public const int WirelessUnbound = 15;
    public const int WirelessBound = 16;

    private static Texture2D? s_texture;

    /// <summary>
    /// 懒加载。资源在 .dll 之前就注册进 ContentManager 了（看日志的加载顺序），
    /// 但方块的 Initialize 时机不保证，用到再取最稳。
    /// </summary>
    public static Texture2D? Texture
    {
        get
        {
            if (s_texture != null)
            {
                return s_texture;
            }

            try
            {
                s_texture = ContentManager.Get<Texture2D>("Textures/Stash/Blocks");
                Log.Information($"[Stash] 方块贴图已载入：{s_texture?.Width}x{s_texture?.Height}");
            }
            catch (Exception exception)
            {
                // 取不到就退回 null，调用方会走原版图集——难看，但不会崩。
                Log.Warning($"[Stash] 载入方块贴图失败，退回原版图集：{exception.Message}");
            }

            return s_texture;
        }
    }
}
