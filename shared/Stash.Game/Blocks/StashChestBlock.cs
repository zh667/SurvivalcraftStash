using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 分级箱子方块。
///
/// **不新增任何贴图**：复用原版箱子的图集槽位（25/26/27/42），按档位乘一个色调。
/// 这条路子是照着原版 <c>PaintedCubeBlock</c> 来的——它就是"同一张贴图 × 一个 Color"，
/// 联机版没有逐方块贴图接口，这样两版才能长得一样，也不会被玩家选的方块材质包冲掉。
///
/// data 位沿用原版箱子的用法：0~3 表示朝向。
/// </summary>
public abstract class StashChestBlock : CubeBlock
{
    public abstract StashChestTier Tier { get; }

    public override int GetFaceTextureSlot(int face, int value)
    {
        // 与原版 ChestBlock 完全一致：顶/底用 42，正面 27，右侧 26，其余 25，按 data 旋转。
        if (face is 4 or 5)
        {
            return 42;
        }

        int data = Terrain.ExtractData(value);
        return data switch
        {
            0 => face switch { 0 => 27, 2 => 26, _ => 25 },
            1 => face switch { 1 => 27, 3 => 26, _ => 25 },
            2 => face switch { 2 => 27, 0 => 26, _ => 25 },
            _ => face switch { 3 => 27, 1 => 26, _ => 25 },
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
        // 朝向逻辑照抄原版箱子：面朝玩家。
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
        StashText.ChestName(Tier);
}

public class StashCopperChestBlock : StashChestBlock
{
    public static int Index = StashChestTiers.CopperChestIndex;

    public override StashChestTier Tier => StashChestTiers.Copper;
}

public class StashIronChestBlock : StashChestBlock
{
    public static int Index = StashChestTiers.IronChestIndex;

    public override StashChestTier Tier => StashChestTiers.Iron;
}

public class StashDiamondChestBlock : StashChestBlock
{
    public static int Index = StashChestTiers.DiamondChestIndex;

    public override StashChestTier Tier => StashChestTiers.Diamond;
}

