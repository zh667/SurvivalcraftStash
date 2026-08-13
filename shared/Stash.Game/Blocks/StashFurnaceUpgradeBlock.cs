using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 熔炉升级件（物品，不可放置）。data 值 = 升级链上的位置：
/// 0 原版熔炉→铜、1 铜→铁、2 铁→钻石。
///
/// 和箱子升级件分开做成两种物品，而不是共用一种加 data 段：
/// 玩家物品栏里两种升级件会同时存在，图标必须一眼分得清点哪个——
/// 箱子升级件是**向上的箭头**，熔炉升级件是**火焰**。
/// </summary>
public class StashFurnaceUpgradeBlock : CubeBlock
{
    public static int Index = StashFurnaceTiers.RequestedFurnaceUpgradeIndex;

    /// <summary>拿不到自家贴图时退回铁锭那格（和箱子升级件同一个退路）。</summary>
    public const int IngotTextureSlot = StashChestUpgradeBlock.IngotTextureSlot;

    private static bool HasTexture => StashBlockTextures.Texture != null;

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (!HasTexture)
        {
            return IngotTextureSlot;
        }

        int data = MathUtils.Clamp(Terrain.ExtractData(value), 0, StashFurnaceTiers.UpgradeChain.Count - 1);
        return StashBlockTextures.FurnaceUpgradeFirst + data;
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

        generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.OpaqueSubsetsByFace);
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
            // 平面物品画法（DrawFlatBlock 走 GetFaceTextureSlot(-1, value)，上面已统一处理）。
            // 当立方体画的话六面同图，拿在手里像块方糖，和原版的锭/工具不是一个语言。
            BlocksManager.DrawFlatBlock(
                primitivesRenderer, value, size, ref matrix, texture, color, isEmissive: false, environmentData);
            return;
        }

        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, color, color, environmentData);
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        for (int data = 0; data < StashFurnaceTiers.UpgradeChain.Count; data++)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, data);
        }
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value) =>
        StashText.FurnaceUpgradeName(Terrain.ExtractData(value));

    public override string GetCategory(int value) => "Items";
}
