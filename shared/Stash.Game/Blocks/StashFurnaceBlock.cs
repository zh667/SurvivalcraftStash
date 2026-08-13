using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 分级熔炉方块。
///
/// 和原版熔炉不同，这里用 <see cref="CubeBlock"/> 而不是模型方块——
/// 原版走 <c>Models/Furnace</c>，我们没有建模管线，而分级箱子那套"自家图集 + 立方体"
/// 已经实机跑通了，照抄能少一整类风险（贴图批次、图标取景、拖拽渲染都是现成的）。
///
/// 贴图走 <see cref="StashBlockTextures"/> 自己的图集：石砌炉体 + 该档金属的边框 + 正面一个亮着火的炉口。
/// 拿不到贴图时退回原版熔炉的槽位，只是看不出档位，不会变黑。
///
/// data 位沿用原版熔炉/箱子的用法：0~3 表示朝向。
/// </summary>
public abstract class StashFurnaceBlock : CubeBlock
{
    /// <summary>原版熔炉在原版图集里的槽位：正面 44，其余面 1（石头）。拿不到自家贴图时用。</summary>
    private const int VanillaFurnaceFrontSlot = 44;
    private const int VanillaStoneSlot = 1;

    public abstract StashFurnaceTier Tier { get; }

    private static bool HasTexture => StashBlockTextures.Texture != null;

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (!HasTexture)
        {
            return VanillaFaceSlot(face, value);
        }

        if (face is 4 or 5)
        {
            return Tier.TopSlot;
        }

        // 正面朝向由 data 决定，其余三面用侧面那格。
        int data = Terrain.ExtractData(value);
        int frontFace = data switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
        return face == frontFace ? Tier.FrontSlot : Tier.SideSlot;
    }

    private static int VanillaFaceSlot(int face, int value)
    {
        if (face is 4 or 5)
        {
            return VanillaStoneSlot;
        }

        int data = Terrain.ExtractData(value);
        int frontFace = data switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
        return face == frontFace ? VanillaFurnaceFrontSlot : VanillaStoneSlot;
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
            // 关键：写进"绑着我们贴图"的那一批，而不是默认那批。见 StashBlockTextures 的类注释。
            generator.GenerateCubeVertices(
                this, value, x, y, z, Color.White,
                geometry.GetGeometry(texture).OpaqueSubsetsByFace);
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
            BlocksManager.DrawCubeBlock(
                primitivesRenderer, value, new Vector3(size), ref matrix, color, color, environmentData, texture);
            return;
        }

        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, color, color, environmentData);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult)
    {
        // 朝向逻辑和分级箱子一致：炉口面朝玩家。
        Vector3 forward = Matrix.CreateFromQuaternion(
            componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
        float toZ = Vector3.Dot(forward, Vector3.UnitZ);
        float toX = Vector3.Dot(forward, Vector3.UnitX);
        float toNegZ = Vector3.Dot(forward, -Vector3.UnitZ);
        float toNegX = Vector3.Dot(forward, -Vector3.UnitX);

        int data;
        if (toZ == MathUtils.Max(toZ, toX, toNegZ, toNegX))
        {
            data = 2;
        }
        else if (toX == MathUtils.Max(toX, toNegZ, toNegX))
        {
            data = 3;
        }
        else
        {
            data = toNegZ >= toNegX ? 0 : 1;
        }

        return new BlockPlacementData
        {
            Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data),
            CellFace = raycastResult.CellFace,
        };
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value) =>
        StashText.FurnaceName(Tier);
}

public class StashCopperFurnaceBlock : StashFurnaceBlock
{
    public static int Index = StashFurnaceTiers.RequestedCopperFurnaceIndex;

    public override StashFurnaceTier Tier => StashFurnaceTiers.Copper;
}

public class StashIronFurnaceBlock : StashFurnaceBlock
{
    public static int Index = StashFurnaceTiers.RequestedIronFurnaceIndex;

    public override StashFurnaceTier Tier => StashFurnaceTiers.Iron;
}

public class StashDiamondFurnaceBlock : StashFurnaceBlock
{
    public static int Index = StashFurnaceTiers.RequestedDiamondFurnaceIndex;

    public override StashFurnaceTier Tier => StashFurnaceTiers.Diamond;
}
