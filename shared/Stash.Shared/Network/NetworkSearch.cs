namespace Stash.Shared.Network;

/// <summary>
/// 终端搜索。语法照着 Tom's Simple Storage 改成 SC 能对上的东西：
///
/// <list type="bullet">
/// <item><c>|</c> 分隔多个条件，任一命中即可（或）</item>
/// <item>空格分隔的词必须全部命中（与）</item>
/// <item><c>#词</c> 匹配类别（对应 BlocksManager 的 Category）</item>
/// <item>其余词匹配显示名</item>
/// </list>
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

        public bool Matches(string displayName, string category)
        {
            if (IsEmpty)
            {
                return true;
            }

            foreach (List<Term> terms in Alternatives)
            {
                if (MatchesAll(terms, displayName, category))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAll(List<Term> terms, string displayName, string category)
        {
            foreach (Term term in terms)
            {
                string haystack = term.IsCategory ? category : displayName;
                if (haystack == null || !haystack.Contains(term.Text, StringComparison.CurrentCultureIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
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
