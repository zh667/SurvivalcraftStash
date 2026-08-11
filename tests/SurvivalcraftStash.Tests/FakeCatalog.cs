using Stash.Shared.Items;

namespace SurvivalcraftStash.Tests;

/// <summary>测试用的物品目录：按 contents 给定堆叠上限、名称、类别。</summary>
internal sealed class FakeCatalog : IItemCatalog
{
    private readonly Dictionary<int, (int Stack, string Name, string Category, int Order)> m_items = new();

    public FakeCatalog Add(int contents, int stack, string name, string category = "Items", int order = 0)
    {
        m_items[contents] = (stack, name, category, order);
        return this;
    }

    private (int Stack, string Name, string Category, int Order) Get(int value) =>
        m_items.TryGetValue(ItemValue.Contents(value), out var info)
            ? info
            : (1, $"block{ItemValue.Contents(value)}", "Items", 0);

    public int GetMaxStacking(int value) => Get(value).Stack;

    public string GetDisplayName(int value) => Get(value).Name;

    public string GetCategory(int value) => Get(value).Category;

    public int GetDisplayOrder(int value) => Get(value).Order;
}
