using Stash.Shared.Search;

namespace Stash.Shared.Network;

/// <summary>
/// 终端搜索。语法照着 Tom's Simple Storage 改成 SC 能对上的东西：
///
/// <list type="bullet">
/// <item><c>|</c> 分隔多个条件，任一命中即可（或）</item>
/// <item>空格分隔的词必须全部命中（与）</item>
/// <item><c>#词</c> 匹配类别（对应 BlocksManager 的 Category）</item>
/// <item>其余词匹配显示名、**显示名的拼音**、以及物品的**英文标识**</item>
/// </list>
///
/// 拼音是连写不带声调的全拼（"圆石" → "yuanshi"），子串匹配，所以打半截也能中。
/// 不做首字母缩写：那个歧义太大（"yh" 能撞上一大片），玩家反而要多翻几屏。
///
/// 英文标识用的是方块的 <c>CraftingId</c> / 类名（"copperingot"、"CopperIngotBlock"）——
/// 游戏运行时只加载当前语言，拿不到英文显示名，而这两个是现成的、并且就是英文单词拼出来的。
///
/// 原版那套里还有 <c>@命名空间</c> 和 <c>$组件</c>，SC 没有对应概念，去掉。
/// 匹配一律不区分大小写，且是"包含"而不是"相等"——玩家打半个词就该能搜到。
/// </summary>
public static class NetworkSearch
{
    public sealed record Term(string Text, bool IsCategory);

    public sealed class Query
    {
        /// <summary>外层是"或"，内层是"与"。空表示不过滤。</summary>
        public List<List<Term>> Alternatives { get; } = new();

        public bool IsEmpty => Alternatives.Count == 0;

        /// <param name="englishId">物品的英文标识（CraftingId / 类名），可空。</param>
        public bool Matches(string displayName, string category, string? englishId = null)
        {
            if (IsEmpty)
            {
                return true;
            }

            foreach (List<Term> terms in Alternatives)
            {
                if (MatchesAll(terms, displayName, category, englishId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAll(List<Term> terms, string displayName, string category, string? englishId)
        {
            foreach (Term term in terms)
            {
                if (term.IsCategory)
                {
                    if (!Contains(category, term.Text))
                    {
                        return false;
                    }

                    continue;
                }

                // 中文直搜 → 拼音 → 英文标识，任一命中就算这个词过了。
                bool hit = Contains(displayName, term.Text)
                    || Contains(StashPinyin.Of(displayName), term.Text)
                    || Contains(englishId, term.Text);

                if (!hit)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(string? haystack, string needle) =>
            haystack != null && haystack.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
    }

    public static Query Parse(string? text)
    {
        var query = new Query();
        if (string.IsNullOrWhiteSpace(text))
        {
            return query;
        }

        foreach (string alternative in text.Split('|'))
        {
            var terms = new List<Term>();
            foreach (string word in alternative.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.StartsWith('#'))
                {
                    if (word.Length > 1)
                    {
                        terms.Add(new Term(word[1..], IsCategory: true));
                    }
                }
                else
                {
                    terms.Add(new Term(word, IsCategory: false));
                }
            }

            if (terms.Count > 0)
            {
                query.Alternatives.Add(terms);
            }
        }

        return query;
    }
}
