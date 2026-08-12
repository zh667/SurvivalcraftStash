using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 存储枢纽（终端）。贴着它的容器连成一片，点它打开终端界面统一检索取放。
///
/// 外观照 Tom's Simple Storage 的 terminal_front：深色机箱、顶上一条青色屏幕、
/// 下面一排物品槽；侧面是散热栅，顶上一颗指示灯。木箱系全是暖色，它是冷色，远看就分得开。
/// </summary>
public class StashHubBlock : CubeBlock
{
    public static int Index = StashChestTiers.BaseIndex + 9;

    /// <summary>拿不到自家贴图时的退路色调（配原版铁锭那格）。</summary>
    private static readonly Color HubTint = new(140, 200, 220);

    private static bool HasTexture => StashBlockTextures.Texture != null;

    public override int GetTextureSlotCount(int value) =>
        HasTexture ? StashBlockTextures.SlotCount : base.GetTextureSlotCount(value);

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (!HasTexture)
        {
            return face is 4 or 5 ? 42 : StashChestUpgradeBlock.IngotTextureSlot;
        }

        return face switch
        {
            4 or 5 => StashBlockTextures.HubTop,
            // 四个侧面都用"正面"那格：枢纽没有朝向，哪一面走过来都该看见屏幕。
            _ => StashBlockTextures.HubFront,
        };
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z)
    {
        if (StashBlockTextures.Texture is { } texture)
        {
            generator.GenerateCubeVertices(
                this, value, x, y, z, Color.White, geometry.GetGeometry(texture).OpaqueSubsetsByFace);
            return;
        }

        generator.GenerateCubeVertices(this, value, x, y, z, HubTint, geometry.OpaqueSubsetsByFace);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData)
    {
        if (StashBlockTextures.Texture is { } texture)
        {
            BlocksManager.DrawCubeBlock(
                primitivesRenderer, value, new Vector3(size), ref matrix, color, color, environmentData, texture);
            return;
        }

        Color tinted = color * HubTint;
        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, tinted, tinted, environmentData);
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value) => StashText.HubName;
}
