using Engine;

namespace Game;

/// <summary>
/// 顶掉原版的 <see cref="ClothingBlock"/>（索引 203），**只为了改背包在物品栏里的取景方向**。
///
/// 背景：所有衣物共用这一个方块索引，图标是拿人物躯干网格现渲的，
/// 相机位置来自 <c>Block.GetIconViewOffset</c>——那是**按方块**取的，原版给的是 (-1, 1, -1)，
/// 正好看到胸前。背包画在背面，于是物品栏里只看得见两条肩带（实机反馈"应该是包在前面"）。
///
/// <c>GetIconViewOffset</c> 是 virtual 且**带 value 参数**的，所以只要能换掉 203 这个实例，
/// 就能只对我们那三档背包把相机挪到背后，别的衣物一个字都不改。
///
/// 换实例是安全的：<c>BlocksManager.Initialize</c> 按 mod 顺序写
/// <c>m_blocks[block.BlockIndex] = block</c>，原版是 system mod 排在最前，我们在后面覆盖。
/// 万一哪天顺序变了，最坏结果只是这个取景没生效——背包照常能穿能开，不会崩。
///
/// 其余行为全部继承原版：贴图、模型、耐久、染色、显示名，一律不碰。
///
/// **只放在联机版**：插件版的方块注册走的是另一条路（<c>BlocksManager.InitializeBlocks</c>），
/// 我没法在这台机器上验证它对"重复索引"的处理，不值得为一个取景角度冒让插件版加载失败的风险。
/// 插件版的背包图标仍然是从正面看（只看得见肩带），等有条件实测再说。
/// </summary>
public class StashClothingBlock : ClothingBlock
{
    /// <summary>必须和原版一样是 203：靠"同索引后加载"来顶替。</summary>
    public new static int Index = ClothingBlock.Index;

    /// <summary>从背后看的机位。原版是 (-1, 1, -1)，水平方向取反就是背面。</summary>
    private static readonly Vector3 FromBehind = new(1f, 1f, 1f);

    public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData)
    {
        int clothingIndex = GetClothingIndex(Terrain.ExtractData(value));
        return Stash.Game.StashBackpackTiers.ByClothingIndex(clothingIndex) != null
            ? FromBehind
            : base.GetIconViewOffset(value, environmentData);
    }
}
