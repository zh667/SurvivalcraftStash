namespace Stash.Shared.Crafting;

/// <summary>
/// 配方里的一格配料，形如 <c>"planks"</c> 或 <c>"planks:2"</c>。
///
/// 规则照抄原版 <c>CraftingRecipesManager.CompareIngredients</c>：
/// <list type="bullet">
/// <item>配方**没写** data → 该 craftingId 的**任何** data 都算数（通配）。</item>
/// <item>配方**写了** data → 必须一模一样。</item>
/// </list>
/// </summary>
public readonly record struct CraftIngredient(string CraftingId, int? Data)
{
    public static CraftIngredient Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new CraftIngredient(string.Empty, null);
        }

        int colon = text.IndexOf(':');
        if (colon < 0)
        {
            return new CraftIngredient(text, null);
        }

        string id = text.Substring(0, colon);
        return int.TryParse(text.Substring(colon + 1), out int data)
            ? new CraftIngredient(id, data)
            : new CraftIngredient(id, null);
    }

    /// <summary>写了 data 的更"挑"。分配物品时要先满足挑剔的，见 <see cref="CraftFillPlanner"/>。</summary>
    public bool IsSpecific => Data.HasValue;

    public bool Matches(string craftingId, int data) =>
        CraftingId == craftingId && (!Data.HasValue || Data.Value == data);

    public override string ToString() => Data.HasValue ? $"{CraftingId}:{Data.Value}" : CraftingId;
}
