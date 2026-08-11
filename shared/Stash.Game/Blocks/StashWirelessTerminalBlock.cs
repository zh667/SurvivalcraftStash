using Engine;
using Engine.Graphics;
using Game;

namespace Stash.Game;

/// <summary>
/// 无线终端（物品，不可放置）。
///
/// data 位存**绑定的存储终端编号**（0 = 未绑定）：
/// 拿着它右键一个存储终端方块就绑上，之后对着空处右键就能远程打开那一个终端。
/// 编号存在世界登记簿里（<see cref="StashHubNaming"/>），玩家可以给终端改名。
/// </summary>
public class StashWirelessTerminalBlock : CubeBlock
{
    // 取 710 而不是填补 703/705~708 那些空号：那几个是已删掉的观景箱和抽屉，
    // 玩家存档里可能还放着，复用索引会让旧方块直接变成无线终端。
    public static int Index = StashChestTiers.BaseIndex + 10;

    private static readonly Color BoundTint = new(140, 210, 160);
    private static readonly Color UnboundTint = new(150, 150, 150);

    public override int GetFaceTextureSlot(int face, int value) => StashChestUpgradeBlock.IngotTextureSlot;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z) =>
        generator.GenerateCubeVertices(this, value, x, y, z, TintOf(value), geometry.OpaqueSubsetsByFace);

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData)
    {
        Color tinted = color * TintOf(value);
        BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, tinted, tinted, environmentData);
    }

    private static Color TintOf(int value) => GetBoundHubId(value) > 0 ? BoundTint : UnboundTint;

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 0);
    }

    public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
    {
        int hubId = GetBoundHubId(value);
        if (hubId <= 0)
        {
            return StashText.WirelessTerminalUnbound;
        }

        string hubName = StashHubNaming.Find(hubId)?.Name ?? StashText.DefaultHubName(hubId);
        return StashText.WirelessTerminalBound(hubName);
    }

    public override string GetCategory(int value) => "Items";

    public static int GetBoundHubId(int value) => Terrain.ExtractData(value);

    public static int Bind(int value, int hubId) => Terrain.ReplaceData(value, hubId);
}
