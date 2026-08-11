using Game;
using Stash.Shared.Items;

namespace Stash.Game;

/// <summary>
/// 用 <c>BlocksManager</c> 实现共享层的物品元数据接口。
/// 两个平台的 <c>Block.GetMaxStacking / GetDisplayName / GetCategory / GetDisplayOrder</c>
/// 签名一致，所以这一份可以两版共用。
/// </summary>
public sealed class BlocksManagerCatalog : IItemCatalog
{
    private readonly SubsystemTerrain? m_subsystemTerrain;

    public BlocksManagerCatalog(SubsystemTerrain? subsystemTerrain = null)
    {
        m_subsystemTerrain = subsystemTerrain;
    }

    private static Block GetBlock(int value) => BlocksManager.Blocks[ItemValue.Contents(value)];

    public int GetMaxStacking(int value)
    {
        try
        {
            return Math.Max(1, GetBlock(value).GetMaxStacking(value));
        }
        catch
        {
            return 1;
        }
    }

    public string GetDisplayName(int value)
    {
        try
        {
            return GetBlock(value).GetDisplayName(m_subsystemTerrain!, value) ?? string.Empty;
        }
        catch
        {
            // 某些方块的显示名依赖地形子系统；拿不到就退回类型名，排序仍然稳定。
            return GetBlock(value).GetType().Name;
        }
    }

    public string GetCategory(int value)
    {
        try
        {
            return GetBlock(value).GetCategory(value) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public int GetDisplayOrder(int value)
    {
        try
        {
            return GetBlock(value).GetDisplayOrder(value);
        }
        catch
        {
            return int.MaxValue;
        }
    }
}
