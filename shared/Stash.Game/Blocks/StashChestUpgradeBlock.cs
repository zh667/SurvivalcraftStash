using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 升级件（物品，不可放置）。data 值 = 升级链上的位置：
/// 0 木→铜、1 铜→铁、2 铁→钻石。
///
/// 同样不新增贴图：复用铁锭的图集槽位，按目标档位上色。
/// </summary>
public class StashChestUpgradeBlock : CubeBlock
{
    public static int Index = StashChestTiers.UpgradeItemIndex;

    /// <summary>铁锭在原版图集里的槽位（实测 #A0A0A0 的那格）。</summary>
    public const int IngotTextureSlot = 196;

    public override int GetFaceTextureSlot(int face, int value) => IngotTextureSlot;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z)
    {
        // 不可放置，理论上不会进地形；留个安全实现免得别的 Mod 强行放置时崩。
        generator.GenerateCubeVertices(this, value, x, y, z, StashChestTiers.UpgradeTint(Terrain.ExtractData(value)), geometry.OpaqueSubsetsByFace);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData)
    {
        Color tinted = color * StashChestTiers.UpgradeTint(Terrain.ExtractData(value));
        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, tinted, tinted, environmentData);
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        for (int data = 0; data < StashChestTiers.UpgradeChain.Count; data++)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, data);
        }
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value) =>
        StashText.UpgradeName(Terrain.ExtractData(value));


    public override string GetCategory(int value) => "Items";
}
