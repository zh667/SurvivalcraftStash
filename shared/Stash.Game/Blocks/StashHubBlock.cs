using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 存储枢纽（终端）。贴着它的容器连成一片，点它打开终端界面统一检索取放。
/// 和其它方块一样复用原版贴图 + 色调，不新增贴图。
/// </summary>
public class StashHubBlock : CubeBlock
{
    public static int Index = StashChestTiers.BaseIndex + 9;

    /// <summary>用铁锭的图集槽位配一个偏冷的色，和箱子区分开。</summary>
    private static readonly Color HubTint = new(140, 200, 220);

    public override int GetFaceTextureSlot(int face, int value) =>
        face is 4 or 5 ? 42 : StashChestUpgradeBlock.IngotTextureSlot;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z) =>
        generator.GenerateCubeVertices(this, value, x, y, z, HubTint, geometry.OpaqueSubsetsByFace);

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData)
    {
        Color tinted = color * HubTint;
        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, tinted, tinted, environmentData);
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value) => StashText.HubName;
}
