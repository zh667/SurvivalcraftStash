using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 无线终端类物品的共同部分：绑定信息存在 data 位里，按"绑没绑"换一格贴图，平面画法。
///
/// 目前两种：普通无线终端（远程开存储终端）和无线合成终端（远程开存储终端 + 带合成格）。
///
/// **每个具体子类必须自己声明 <c>public static int Index</c>**，不能只靠基类一个。
/// 插件版（SCAPI）会按类型去找那个静态字段并写回真实索引；
/// 两个方块共用一个字段的话，它们会抢同一个索引，后果是其中一个彻底认不出来。
/// 分级箱子（抽象基类 + 三个各带 Index 的子类）已经验证过这个写法。
/// </summary>
public abstract class StashWirelessBlockBase : CubeBlock
{
    /// <summary>拿不到自家图集时的退路：煤块那格（实测 #101010 的深色）。</summary>
    public const int DarkTextureSlot = 62;

    /// <summary>没绑定时用的图集格号。</summary>
    protected abstract int UnboundSlot { get; }

    /// <summary>绑定后用的图集格号。</summary>
    protected abstract int BoundSlot { get; }

    /// <summary>没绑定时的名字。</summary>
    protected abstract string UnboundName { get; }

    /// <summary>绑定后的名字，参数是存储终端的名字。</summary>
    protected abstract string BoundName(string hubName);

    /// <summary>拿不到贴图时按绑定状态乘的色调。</summary>
    protected virtual Color BoundTint => new(120, 230, 235);

    protected virtual Color UnboundTint => new(190, 90, 90);

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (StashBlockTextures.Texture == null)
        {
            return DarkTextureSlot;
        }

        return GetBoundHubId(value) > 0 ? BoundSlot : UnboundSlot;
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

        generator.GenerateCubeVertices(this, value, x, y, z, TintOf(value), geometry.OpaqueSubsetsByFace);
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
            BlocksManager.DrawFlatBlock(
                primitivesRenderer, value, size, ref matrix, texture, color, isEmissive: false, environmentData);
            return;
        }

        Color tinted = color * TintOf(value);
        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, tinted, tinted, environmentData);
    }

    private Color TintOf(int value) => GetBoundHubId(value) > 0 ? BoundTint : UnboundTint;

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 0);
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
    {
        int hubId = GetBoundHubId(value);
        if (hubId <= 0)
        {
            return UnboundName;
        }

        return BoundName(StashHubNaming.Find(hubId)?.Name ?? StashText.DefaultHubName(hubId));
    }

    public override string GetCategory(int value) => "Items";

    /// <summary>data 位存的就是存储终端编号，0 = 未绑定。</summary>
    public static int GetBoundHubId(int value) => Terrain.ExtractData(value);

    public static int Bind(int value, int hubId) => Terrain.ReplaceData(value, hubId);

    /// <summary>这个方块索引是不是某种无线终端。</summary>
    public static bool IsWireless(int blockIndex) =>
        blockIndex == StashWirelessTerminalBlock.Index
        || blockIndex == StashWirelessCraftingTerminalBlock.Index;

    /// <summary>这一种带不带合成格。</summary>
    public static bool HasCraftingGrid(int blockIndex) =>
        blockIndex == StashWirelessCraftingTerminalBlock.Index;
}
