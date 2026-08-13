using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 物品目录 + 配方查询。建一次缓存住，之后都是查表。
///
/// **正查**（"这东西怎么做"）原版图鉴就有：<c>RecipaediaRecipesScreen</c> 里
/// <c>Recipes.Where(r =&gt; r.ResultValue == value)</c>。
///
/// 已知盲区：<c>Block.GetAdHocCraftingRecipe</c> 那类**动态配方**（染色、修理之类）
/// 是按合成格里的实际内容现算的，**枚举不出来**，所以这些物品会显示"没有配方"。
/// 原版图鉴有一模一样的盲区，不算倒退。
/// </summary>
public static class StashRecipeIndex
{
    /// <summary>一个物品在搜索里要比对的三样东西，建索引时算好。</summary>
    public readonly record struct SearchableItem(int Value, string Name, string Category, string English);

    private static List<int>? s_items;
    private static List<SearchableItem>? s_searchable;
    private static Dictionary<int, List<CraftingRecipe>>? s_byResult;


    /// <summary>创造物品栏里能拿到的所有物品值，按原版的显示顺序排好。</summary>
    public static IReadOnlyList<int> AllItems
    {
        get
        {
            EnsureBuilt();
            return s_items!;
        }
    }

    /// <summary>
    /// 搜索用的元数据，和 <see cref="AllItems"/> 一一对应、同序。
    ///
    /// 预先算好是有必要的：目录有一千多个物品，而搜索框**每敲一个字**都要重新过滤一遍。
    /// 现算的话每次都要调一千多次 <c>GetDisplayName</c> / <c>GetCraftingId</c>
    /// （都可能走语言表查询），够卡出一帧。
    /// </summary>
    public static IReadOnlyList<SearchableItem> Searchable
    {
        get
        {
            EnsureBuilt();
            return s_searchable!;
        }
    }

    /// <summary>
    /// 做出这个物品的所有配方。**按整个 value 精确匹配**，和原版图鉴一致
    /// （<c>RecipaediaRecipesScreen</c>：<c>Recipes.Where(r =&gt; r.ResultValue == value)</c>）。
    ///
    /// 曾经试过"精确匹配不到就退回只按方块索引匹配"，想照顾 data 位对不上的情况——
    /// **这是个坏主意，实机直接翻车**：所有衣物共用方块索引 203，
    /// 于是随便点一件染色衣服都会"找到 37 条配方"，而且面板标题显示成别的衣服；
    /// 同理所有蛋共用一个索引，煮熟的鸽子蛋会显示成煮熟的海鸥蛋的配方。
    /// data 位本来就是用来区分这些东西的，不能忽略。
    /// </summary>
    public static IReadOnlyList<CraftingRecipe> RecipesFor(int value)
    {
        EnsureBuilt();
        return s_byResult!.TryGetValue(value, out List<CraftingRecipe>? list)
            ? list
            : Array.Empty<CraftingRecipe>();
    }

    /// <summary>换世界 / 重载 Mod 后调一次，免得拿着上一局的方块索引。</summary>
    public static void Invalidate()
    {
        s_items = null;
        s_searchable = null;
        s_byResult = null;
    }

    private static void EnsureBuilt()
    {
        if (s_items != null)
        {
            return;
        }

        s_items = new List<int>();
        s_searchable = new List<SearchableItem>();
        s_byResult = new Dictionary<int, List<CraftingRecipe>>();

        try
        {
            BuildItems();
            BuildRecipes();
            Log.Information($"[Stash] 配方索引：{s_items.Count} 个物品，"
                + $"{s_byResult.Count} 种结果值有配方");
        }
        catch (Exception exception)
        {
            // 索引建不起来最坏是浏览器空着，不该把世界带崩。
            Log.Warning($"[Stash] 建配方索引失败：{exception.Message}");
        }
    }

    private static void BuildItems()
    {
        // 和原版图鉴同一套枚举方式（RecipaediaScreen.PopulateBlocksList）。
        var ordered = new List<(int Category, int Order, int Value, SearchableItem Info)>();

        // 分类顺序按 BlocksManager.Categories 的原始顺序，没登记的排最后。
        var categoryOrder = new Dictionary<string, int>();
        try
        {
            int i = 0;
            foreach (string category in BlocksManager.Categories)
            {
                categoryOrder[category] = i++;
            }
        }
        catch
        {
            // 拿不到分类表就退化成"只按 DisplayOrder 排"，不影响能用。
        }

        foreach (Block block in BlocksManager.Blocks)
        {
            if (block == null)
            {
                continue;
            }

            try
            {
                foreach (int value in block.GetCreativeValues())
                {
                    SearchableItem info = Describe(value);
                    int category = categoryOrder.TryGetValue(info.Category, out int index) ? index : int.MaxValue;
                    ordered.Add((category, block.GetDisplayOrder(value), value, info));
                }
            }
            catch
            {
                // 个别方块的 GetCreativeValues 依赖别的子系统，跳过就是了。
            }
        }

        // **必须先按分类分组再按 DisplayOrder 排。**
        // DisplayOrder 只在**同一分类内**有意义：原版图鉴是先选分类再列表的。
        // 拉平成一个大列表只按 DisplayOrder 排的话，衣物（DisplayIndex 39~41）会插到
        // 泥土、树叶中间——实机看到的就是"一堆行囊夹在草方块里"。
        ordered.Sort((a, b) =>
        {
            if (a.Category != b.Category)
            {
                return a.Category.CompareTo(b.Category);
            }

            return a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Value.CompareTo(b.Value);
        });

        foreach ((int _, int _, int value, SearchableItem info) in ordered)
        {
            s_items!.Add(value);
            s_searchable!.Add(info);
        }
    }

    private static SearchableItem Describe(int value)
    {
        try
        {
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];

            // 英文标识 = CraftingId + 类型名。玩家看过 Wiki 或别的模组，
            // 习惯直接打 "copper" / "chest" 这种词。
            return new SearchableItem(
                value,
                block.GetDisplayName(null!, value) ?? string.Empty,
                block.GetCategory(value) ?? string.Empty,
                (block.GetCraftingId(value) ?? string.Empty) + " " + block.GetType().Name);
        }
        catch
        {
            // 个别方块的显示名依赖地形子系统，拿不到就只留下类型名，起码英文还能搜。
            return new SearchableItem(value, string.Empty, string.Empty, string.Empty);
        }
    }

    private static List<CraftingRecipe> Bucket(Dictionary<int, List<CraftingRecipe>> map, int key)
    {
        if (!map.TryGetValue(key, out List<CraftingRecipe>? list))
        {
            list = new List<CraftingRecipe>();
            map[key] = list;
        }

        return list;
    }

    private static void BuildRecipes()
    {
        foreach (CraftingRecipe recipe in CraftingRecipesManager.Recipes)
        {
            if (recipe == null || recipe.ResultValue == 0)
            {
                continue;
            }

            Bucket(s_byResult!, recipe.ResultValue).Add(recipe);
        }
    }
}
