using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 抽屉方块。和分级箱子一样复用原版贴图 + 档位色调，不新增贴图。
/// 用原版箱子的正面/侧面纹理，视觉上是"一个带面板的柜子"。
/// </summary>
public abstract class StashDrawerBlock : CubeBlock
{
    public abstract StashDrawerTier Tier { get; }

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (face is 4 or 5)
        {
            return 42;
        }

        int data = Terrain.ExtractData(value);
        return data switch
        {
            0 => face == 0 ? 27 : 25,
            1 => face == 1 ? 27 : 25,
            2 => face == 2 ? 27 : 25,
            _ => face == 3 ? 27 : 25,
        };
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z) =>
        generator.GenerateCubeVertices(this, value, x, y, z, Tier.Tint, geometry.OpaqueSubsetsByFace);

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData)
    {
        Color tinted = color * Tier.Tint;
        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, tinted, tinted, environmentData);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult)
    {
        Vector3 forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
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
        StashText.DrawerName(Tier);
}

public class StashWoodDrawerBlock : StashDrawerBlock
{
    public static int Index = StashDrawerTiers.WoodDrawerIndex;

    public override StashDrawerTier Tier => StashDrawerTiers.Wood;
}

public class StashCopperDrawerBlock : StashDrawerBlock
{
    public static int Index = StashDrawerTiers.CopperDrawerIndex;

    public override StashDrawerTier Tier => StashDrawerTiers.Copper;
}

public class StashIronDrawerBlock : StashDrawerBlock
{
    public static int Index = StashDrawerTiers.IronDrawerIndex;

    public override StashDrawerTier Tier => StashDrawerTiers.Iron;
}

public class StashDiamondDrawerBlock : StashDrawerBlock
{
    public static int Index = StashDrawerTiers.DiamondDrawerIndex;

    public override StashDrawerTier Tier => StashDrawerTiers.Diamond;
}
