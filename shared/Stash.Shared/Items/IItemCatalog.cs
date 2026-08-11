namespace Stash.Shared.Items;

/// <summary>
/// 共享层需要的物品元数据。由各平台适配器用 <c>BlocksManager</c> 实现。
/// </summary>
public interface IItemCatalog
{
    /// <summary>该物品一格最多堆多少个。</summary>
    int GetMaxStacking(int value);

    /// <summary>显示名，用于按名称排序与终端搜索。</summary>
    string GetDisplayName(int value);

    /// <summary>类别（对应 <c>BlocksManager.Categories</c>），用于分组排序与 <c>#类别</c> 搜索。</summary>
    string GetCategory(int value);

    /// <summary>创造栏展示顺序，作为"默认排序"的稳定次序来源。</summary>
    int GetDisplayOrder(int value);
}
