using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 我们自己的方块图集：<c>Assets/Textures/Stash/Blocks.png</c>，256×256 = 8×8 格，每格 32×32。
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
/// UV 是 <c>BlocksManager</c> 按 <c>格号 % 列数 / 格号 / 列数</c> 算的，
/// 所以 <see cref="Block.GetTextureSlotCount"/> 必须返回 <see cref="SlotCount"/>，
/// <c>GetFaceTextureSlot</c> 返回下面这些格号。
///
/// 贴图本身是 <c>tools/gen_textures.py</c> 生成的，改配色/形状去改那个脚本，别手改 PNG。
/// </summary>
public static class StashBlockTextures
{
    /// <summary>图集一行几格。改这个要同步改 gen_textures.py 里的 COLS。</summary>
    public const int SlotCount = 8;

    public const int CopperChestFront = 0;
    public const int CopperChestSide = 1;
    public const int CopperChestTop = 2;

    public const int IronChestFront = 8;
    public const int IronChestSide = 9;
    public const int IronChestTop = 10;

    public const int DiamondChestFront = 16;
    public const int DiamondChestSide = 17;
    public const int DiamondChestTop = 18;

    public const int HubFront = 24;
    public const int HubSide = 25;
    public const int HubTop = 26;

    /// <summary>升级件三档，格号 = 32 + 升级链下标。</summary>
    public const int UpgradeFirst = 32;

    public const int WirelessUnbound = 40;
    public const int WirelessBound = 41;

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
