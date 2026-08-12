using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 升级件（物品，不可放置）。data 值 = 升级链上的位置：
/// 0 木→铜、1 铜→铁、2 铁→钻石。
///
/// 每档一格自己的贴图（一块该档金属的板 + 一个朝上的箭头），
/// 而且是**平面物品**画法而不是立方体——之前当立方体画，六个面全是同一张图，
/// 拿在手里像块方糖，跟原版的锭/工具完全不是一个语言。
/// </summary>
public class StashChestUpgradeBlock : CubeBlock
{
    public static int Index = StashChestTiers.UpgradeItemIndex;

    /// <summary>铁锭在原版图集里的槽位（实测 #A0A0A0 的那格）。拿不到自家贴图时用。</summary>
    public const int IngotTextureSlot = 196;

    private static bool HasTexture => StashBlockTextures.Texture != null;

    public override int GetTextureSlotCount(int value) =>
        HasTexture ? StashBlockTextures.SlotCount : base.GetTextureSlotCount(value);

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (!HasTexture)
        {
            return IngotTextureSlot;
        }

        int data = MathUtils.Clamp(Terrain.ExtractData(value), 0, StashChestTiers.UpgradeChain.Count - 1);
        return StashBlockTextures.UpgradeFirst + data;
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z)
    {
        // 不可放置，理论上不会进地形；留个安全实现免得别的 Mod 强行放置时崩。
        if (StashBlockTextures.Texture is { } texture)
        {
            generator.GenerateCubeVertices(
                this, value, x, y, z, Color.White, geometry.GetGeometry(texture).OpaqueSubsetsByFace);
            return;
        }

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
        if (StashBlockTextures.Texture is { } texture)
        {
            // DrawFlatBlock 走的是 GetFaceTextureSlot(-1, value)，上面已经统一处理了。
            BlocksManager.DrawFlatBlock(
                primitivesRenderer, value, size, ref matrix, texture, color, isEmissive: false, environmentData);
            return;
        }

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
