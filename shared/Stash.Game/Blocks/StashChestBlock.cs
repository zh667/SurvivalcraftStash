using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 分级箱子方块。
///
/// 贴图走**我们自己的图集**（见 <see cref="StashBlockTextures"/>），不再是"原版箱子 × 一个色调"。
/// 色调那套的问题是它把木头也一起染了，三档看着像同一个箱子换了滤镜；
/// 现在每档有自己的一套面：木箱本体不动，四边包一圈该档金属的边框加铆钉——
/// 这是 IronChest 的做法，一眼能看出等级，又没脱离 SC 的木箱语言。
///
/// 拿不到贴图时（资源没打进包之类）自动退回原版箱子的槽位，只是难看，不会崩。
///
/// data 位沿用原版箱子的用法：0~3 表示朝向。
/// </summary>
public abstract class StashChestBlock : CubeBlock
{
    public abstract StashChestTier Tier { get; }

    /// <summary>这一档的三个面在自家图集里的格号。</summary>
    protected abstract (int Front, int Side, int Top) Slots { get; }

    private static bool HasTexture => StashBlockTextures.Texture != null;


    public override int GetFaceTextureSlot(int face, int value)
    {
        if (!HasTexture)
        {
            return VanillaFaceSlot(face, value);
        }

        (int front, int side, int top) = Slots;
        if (face is 4 or 5)
        {
            return top;
        }

        // 正面朝向由 data 决定，其余四面用侧面那格。
        int data = Terrain.ExtractData(value);
        int frontFace = data switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
        return face == frontFace ? front : side;
    }

    /// <summary>退路：原版 ChestBlock 的槽位（顶/底 42，正面 27，右侧 26，其余 25）。</summary>
    private static int VanillaFaceSlot(int face, int value)
    {
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
        int z)
    {
        if (StashBlockTextures.Texture is { } texture)
        {
            // 关键：把顶点写进"绑着我们贴图"的那一批，而不是默认那批。
            generator.GenerateCubeVertices(
                this, value, x, y, z, Color.White,
                geometry.GetGeometry(texture).OpaqueSubsetsByFace);
            return;
        }

        generator.GenerateCubeVertices(this, value, x, y, z, Tier.Tint, geometry.OpaqueSubsetsByFace);
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

    protected override (int Front, int Side, int Top) Slots => (
        StashBlockTextures.CopperChestFront,
        StashBlockTextures.CopperChestSide,
        StashBlockTextures.CopperChestTop);
}

public class StashIronChestBlock : StashChestBlock
{
    public static int Index = StashChestTiers.IronChestIndex;

    public override StashChestTier Tier => StashChestTiers.Iron;

    protected override (int Front, int Side, int Top) Slots => (
        StashBlockTextures.IronChestFront,
        StashBlockTextures.IronChestSide,
        StashBlockTextures.IronChestTop);
}

public class StashDiamondChestBlock : StashChestBlock
{
    public static int Index = StashChestTiers.DiamondChestIndex;

    public override StashChestTier Tier => StashChestTiers.Diamond;

    protected override (int Front, int Side, int Top) Slots => (
        StashBlockTextures.DiamondChestFront,
        StashBlockTextures.DiamondChestSide,
        StashBlockTextures.DiamondChestTop);
}

