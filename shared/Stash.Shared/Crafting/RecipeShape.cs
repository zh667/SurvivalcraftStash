namespace Stash.Shared.Crafting;

/// <summary>
/// 配方形状：能不能塞进 N×N 的合成格，以及每一格该摆在哪个槽位。
///
/// 原版把配方存成**规范的 3×3**（<c>CraftingRecipe.Ingredients</c> 是 <c>string[9]</c>，
/// 下标 = 列 + 行*3）。匹配时 <c>CraftingRecipesManager.MatchRecipe</c> 会
/// **穷举所有平移（±3）和左右翻转**再逐格比对：
/// <code>
/// for (int num = 0; num &lt; 2; num++)                  // 翻不翻
///   for (int num2 = -3; num2 &lt;= 3; num2++)           // 竖向平移
///     for (int num3 = -3; num3 &lt;= 3; num3++)         // 横向平移
///       if (TransformRecipe(array, requiredIngredients, num3, num2, flip)) …
/// </code>
///
/// 这给了两个很实用的结论：
/// <list type="number">
/// <item>**"配方太大"的判定是精确的**：只要非空格的包围盒不超过 N×N 就一定摆得下，
/// 因为平移是自由的。2×2 的自带合成格判 2，工作台判 3。</item>
/// <item>**摆放很简单**：把包围盒挪到左上角，格 (列,行) 放进槽位 <c>列 + 行*N</c>——
/// 这正是 <c>ComponentCraftingTable</c> 的槽位编号方式
/// （<c>int slotIndex = i + j * m_craftingGridSize;</c>）。</item>
/// </list>
/// </summary>
public static class RecipeShape
{
    /// <summary>原版配方永远存成 3×3。</summary>
    public const int CanonicalSize = 3;

    /// <summary>非空格的包围盒尺寸。全空返回 (0, 0)。</summary>
    public static (int Width, int Height) Extent(IReadOnlyList<string?> ingredients)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (int i = 0; i < CanonicalSize * CanonicalSize && i < ingredients.Count; i++)
        {
            if (string.IsNullOrEmpty(ingredients[i]))
            {
                continue;
            }

            int x = i % CanonicalSize;
            int y = i / CanonicalSize;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return maxX < 0 ? (0, 0) : (maxX - minX + 1, maxY - minY + 1);
    }

    public static bool Fits(IReadOnlyList<string?> ingredients, int gridSize) =>
        Fits(ingredients, gridSize, gridSize);

    /// <summary>
    /// 摆得下吗。**格子不一定是正方形**：工作台 3×3、按 E 的自带合成格 2×2，
    /// 而熔炉是 <b>N×1</b>——它的原料槽是一条直线
    /// （<c>ComponentFurnace.FindSmeltingRecipe</c> 里 <c>m_matchedIngredients[i]</c>
    /// 只填 <c>i &lt; m_furnaceSize</c>，也就是规范 3×3 的第一行）。
    /// </summary>
    public static bool Fits(IReadOnlyList<string?> ingredients, int columns, int rows)
    {
        (int width, int height) = Extent(ingredients);
        return width > 0 && width <= columns && height <= rows;
    }

    /// <summary>
    /// 把配方摆进 <paramref name="gridSize"/>×<paramref name="gridSize"/> 的合成格，
    /// 返回 (槽位下标, 配料串) 列表。摆不下返回 false。
    /// </summary>
    public static bool TryPlace(
        IReadOnlyList<string?> ingredients,
        int gridSize,
        out List<(int Slot, string Ingredient)> placements) =>
        TryPlace(ingredients, gridSize, gridSize, out placements);

    /// <summary>
    /// 把配方摆进 <paramref name="columns"/>×<paramref name="rows"/> 的格子，
    /// 返回 (槽位下标, 配料串) 列表。摆不下返回 false。
    /// 槽位编号 = <c>列 + 行 * 列数</c>，和 <c>ComponentCraftingTable</c> 一致；
    /// 熔炉 rows=1，编号就退化成"第几个原料槽"。
    /// </summary>
    public static bool TryPlace(
        IReadOnlyList<string?> ingredients,
        int columns,
        int rows,
        out List<(int Slot, string Ingredient)> placements)
    {
        placements = new List<(int, string)>();

        if (columns <= 0 || rows <= 0 || !Fits(ingredients, columns, rows))
        {
            return false;
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        for (int i = 0; i < CanonicalSize * CanonicalSize && i < ingredients.Count; i++)
        {
            if (string.IsNullOrEmpty(ingredients[i]))
            {
                continue;
            }

            minX = Math.Min(minX, i % CanonicalSize);
            minY = Math.Min(minY, i / CanonicalSize);
        }

        for (int i = 0; i < CanonicalSize * CanonicalSize && i < ingredients.Count; i++)
        {
            string? ingredient = ingredients[i];
            if (string.IsNullOrEmpty(ingredient))
            {
                continue;
            }

            // 归一化到左上角，再按合成格的行宽重新编号。
            int x = i % CanonicalSize - minX;
            int y = i / CanonicalSize - minY;
            placements.Add((x + y * columns, ingredient));
        }

        return placements.Count > 0;
    }
}
