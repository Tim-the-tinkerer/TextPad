using System.Text.RegularExpressions;

namespace TextPad.Services;

public readonly record struct TextSearchMatch(int Start, int Length);

public readonly record struct TextSearchOptions(
    bool MatchCase = false,
    bool WholeWord = false,
    bool UseRegex = false);

public static class EditorTextSearch
{
    public static IReadOnlyList<TextSearchMatch> FindAll(string text, string query, bool matchCase) =>
        FindAll(text, query, new TextSearchOptions(MatchCase: matchCase));

    public static IReadOnlyList<TextSearchMatch> FindAll(string text, string query, TextSearchOptions options)
    {
        var matches = new List<TextSearchMatch>();
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
            return matches;

        if (options.UseRegex)
        {
            var regexOptions = options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            try
            {
                foreach (Match match in Regex.Matches(text, query, regexOptions, TimeSpan.FromSeconds(2)))
                {
                    if (match.Success)
                        matches.Add(new TextSearchMatch(match.Index, match.Length));
                }
            }
            catch (RegexParseException)
            {
            }

            return matches;
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(query, index, comparison);
            if (found < 0)
                break;

            if (!options.WholeWord || IsWholeWord(text, found, query.Length))
                matches.Add(new TextSearchMatch(found, query.Length));

            index = found + Math.Max(1, query.Length);
        }

        return matches;
    }

    public static int FindNext(string text, string query, int start, bool matchCase, bool wrap, out TextSearchMatch match) =>
        FindNext(text, query, start, new TextSearchOptions(MatchCase: matchCase), wrap, out match);

    public static bool MatchesAt(string text, string query, int start, int length, TextSearchOptions options)
    {
        if (start < 0 || length <= 0 || start + length > text.Length || string.IsNullOrEmpty(query))
            return false;

        if (options.UseRegex)
        {
            var regexOptions = options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            try
            {
                var slice = text.Substring(start, length);
                return Regex.IsMatch(slice, $"^{query}$", regexOptions, TimeSpan.FromSeconds(2));
            }
            catch (RegexParseException)
            {
                return false;
            }
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (length != query.Length)
            return false;

        return string.Compare(text, start, query, 0, query.Length, comparison) == 0 &&
               (!options.WholeWord || IsWholeWord(text, start, query.Length));
    }

    public static int FindNext(string text, string query, int start, TextSearchOptions options, bool wrap, out TextSearchMatch match)
    {
        match = default;
        var matches = FindAll(text, query, options);
        if (matches.Count == 0)
            return -1;

        foreach (var candidate in matches)
        {
            if (candidate.Start >= start)
            {
                match = candidate;
                return candidate.Start;
            }
        }

        if (!wrap)
            return -1;

        match = matches[0];
        return matches[0].Start;
    }

    public static int FindPrevious(string text, string query, int start, bool matchCase, bool wrap, out TextSearchMatch match) =>
        FindPrevious(text, query, start, new TextSearchOptions(MatchCase: matchCase), wrap, out match);

    public static int FindPrevious(string text, string query, int start, TextSearchOptions options, bool wrap, out TextSearchMatch match)
    {
        match = default;
        var matches = FindAll(text, query, options);
        if (matches.Count == 0)
            return -1;

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Start < start)
            {
                match = matches[i];
                return matches[i].Start;
            }
        }

        if (!wrap)
            return -1;

        match = matches[^1];
        return matches[^1].Start;
    }

    private static bool IsWholeWord(string text, int index, int length)
    {
        var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
        var afterIndex = index + length;
        var after = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
        return before && after;
    }
}