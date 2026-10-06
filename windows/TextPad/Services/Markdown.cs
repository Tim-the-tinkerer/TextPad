using System.Net;
using System.Text;

namespace TextPad.Services;

/// <summary>
/// A practical Markdown subset shared by preview and HTML export.
/// Headings, paragraphs, lists, quotes, fences, tables, emphasis, links, and images.
/// Raw HTML in the source is escaped. Unsafe link schemes are dropped.
/// </summary>
public static class Markdown
{
    public readonly record struct PageStyle(
        string Background,
        string Foreground,
        string Muted,
        string CodeBackground,
        string Accent,
        string Border)
    {
        public static PageStyle Export { get; } = new(
            "#ffffff",
            "#1a1a1f",
            "#5c6570",
            "#f3f4f6",
            "#0b57d0",
            "#e4e4ea");
    }

    public static string HtmlDocument(string markdown, string title, PageStyle style)
    {
        var safeTitle = Escape(title ?? "Document");
        var body = HtmlBody(markdown ?? string.Empty);
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{safeTitle}}</title>
            <style>
            body { background:{{style.Background}}; color:{{style.Foreground}}; font:16px/1.55 "Segoe UI",Helvetica,Arial,sans-serif; margin:0; padding:2rem 1.25rem 3rem; }
            main { max-width:42rem; margin:0 auto; }
            h1,h2,h3,h4,h5,h6 { line-height:1.25; margin:1.4em 0 0.4em; }
            p,ul,ol,blockquote,pre,table { margin:0.8em 0; }
            a { color:{{style.Accent}}; }
            code,pre { font-family:Consolas,"Cascadia Mono",monospace; font-size:0.92em; }
            code { background:{{style.CodeBackground}}; padding:0.1em 0.35em; border-radius:4px; }
            pre { background:{{style.CodeBackground}}; padding:0.85em 1em; overflow:auto; border-radius:8px; }
            pre code { background:none; padding:0; }
            blockquote { margin-left:0; padding-left:1em; border-left:3px solid {{style.Border}}; color:{{style.Muted}}; }
            hr { border:0; border-top:1px solid {{style.Border}}; margin:1.5em 0; }
            img { max-width:100%; }
            table { border-collapse:collapse; width:100%; }
            th,td { border:1px solid {{style.Border}}; padding:0.35em 0.6em; text-align:left; vertical-align:top; }
            ul,ol { padding-left:1.4em; }
            li { margin:0.2em 0; }
            </style>
            </head>
            <body>
            <main>
            {{body}}
            </main>
            </body>
            </html>
            """;
    }

    public static string HtmlBody(string markdown)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        return string.Join("\n", RenderBlocks(lines));
    }

    public static string? SafeUrl(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;
        foreach (var scalar in trimmed)
        {
            if (scalar < 32 || scalar == 127)
                return null;
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower.StartsWith("http://") || lower.StartsWith("https://") || lower.StartsWith("mailto:"))
            return Escape(trimmed);
        if (lower.Contains(':') && !lower.StartsWith("./") && !lower.StartsWith("../"))
        {
            var scheme = lower.Split(':', 2)[0];
            if (scheme.Length > 0 && scheme.All(ch => char.IsLetterOrDigit(ch) || ch is '+' or '.' or '-'))
                return null;
        }

        if (lower.StartsWith("//"))
            return null;
        return Escape(trimmed);
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }

    private static List<string> RenderBlocks(string[] lines)
    {
        var index = 0;
        var blocks = new List<string>();
        while (index < lines.Length)
        {
            if (IsBlank(lines[index]))
            {
                index++;
                continue;
            }

            if (ReadFence(lines, ref index) is { } fence)
            {
                blocks.Add(fence);
                continue;
            }

            if (AtxHeading(lines[index]) is { } heading)
            {
                blocks.Add(heading);
                index++;
                continue;
            }

            if (IsTableStart(lines, index))
            {
                blocks.Add(ReadTable(lines, ref index));
                continue;
            }

            if (IsThematicBreak(lines[index]))
            {
                blocks.Add("<hr>");
                index++;
                continue;
            }

            if (IsQuoteLine(lines[index]))
            {
                blocks.Add(ReadQuote(lines, ref index));
                continue;
            }

            if (ListMarker(lines[index]) is not null && IndentColumns(lines[index]) < 4)
            {
                blocks.Add(ReadList(lines, ref index, IndentColumns(lines[index])));
                continue;
            }

            if (IndentColumns(lines[index]) >= 4)
            {
                blocks.Add(ReadIndentedCode(lines, ref index));
                continue;
            }

            blocks.Add(ReadParagraph(lines, ref index));
        }

        return blocks;
    }

    private readonly record struct FenceOpen(char Marker, int Count, string Info);

    private static string? ReadFence(string[] lines, ref int index)
    {
        var open = FenceStart(lines[index]);
        if (open is null)
            return null;

        var start = index + 1;
        var end = start;
        while (end < lines.Length && !FenceClose(lines[end], open.Value.Marker, open.Value.Count))
            end++;

        var body = string.Join("\n", lines[start..Math.Min(end, lines.Length)]);
        index = Math.Min(end + 1, lines.Length);
        var language = open.Value.Info.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var className = language.Length > 0 && language.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '+')
            ? language
            : "";
        var classAttr = className.Length == 0 ? "" : $" class=\"language-{Escape(className)}\"";
        return $"<pre><code{classAttr}>{Escape(body)}</code></pre>";
    }

    private static FenceOpen? FenceStart(string line)
    {
        var (indent, rest) = SplitIndent(line);
        if (indent >= 4 || rest.Length == 0)
            return null;
        var marker = rest[0];
        if (marker is not ('`' or '~'))
            return null;
        var count = 0;
        while (count < rest.Length && rest[count] == marker)
            count++;
        if (count < 3)
            return null;
        var info = rest[count..].Trim();
        if (marker == '`' && info.Contains('`'))
            return null;
        return new FenceOpen(marker, count, info);
    }

    private static bool FenceClose(string line, char marker, int minimum)
    {
        var (indent, rest) = SplitIndent(line);
        if (indent >= 4)
            return false;
        var trimmed = rest.Trim();
        return trimmed.Length >= minimum && trimmed.All(ch => ch == marker);
    }

    private static string? AtxHeading(string line)
    {
        var (indent, rest) = SplitIndent(line);
        if (indent >= 4 || rest.Length == 0 || rest[0] != '#')
            return null;
        var level = 0;
        while (level < rest.Length && level < 6 && rest[level] == '#')
            level++;
        if (level == 0)
            return null;
        if (level < rest.Length && !char.IsWhiteSpace(rest[level]))
            return null;
        var text = rest[level..].Trim().TrimEnd('#').Trim();
        return $"<h{level}>{RenderInline(text)}</h{level}>";
    }

    private static bool IsTableStart(string[] lines, int index)
    {
        if (index + 1 >= lines.Length)
            return false;
        return lines[index].Contains('|')
               && IsTableSeparator(lines[index + 1])
               && SplitRow(lines[index]).Count >= 1
               && SplitRow(lines[index + 1]).Count >= 1;
    }

    private static bool IsTableSeparator(string line)
    {
        var cells = SplitRow(line);
        if (cells.Count == 0)
            return false;
        return cells.All(cell =>
        {
            var trimmed = cell.Trim();
            return trimmed.Contains('-') && trimmed.All(ch => ch is '-' or ':' or ' ' or '\t');
        });
    }

    private static string ReadTable(string[] lines, ref int index)
    {
        var header = SplitRow(lines[index]);
        index += 2;
        var rows = new List<List<string>>();
        while (index < lines.Length && !IsBlank(lines[index]) && lines[index].Contains('|'))
        {
            rows.Add(SplitRow(lines[index]));
            index++;
        }

        var html = new StringBuilder("<table>\n<thead><tr>");
        foreach (var cell in header)
            html.Append("<th>").Append(RenderInline(cell)).Append("</th>");
        html.Append("</tr></thead>\n<tbody>\n");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            for (var column = 0; column < header.Count; column++)
            {
                var cell = column < row.Count ? row[column] : "";
                html.Append("<td>").Append(RenderInline(cell)).Append("</td>");
            }
            html.Append("</tr>\n");
        }

        html.Append("</tbody></table>");
        return html.ToString();
    }

    private static List<string> SplitRow(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|'))
            text = text[1..];
        if (text.EndsWith('|') && !text.EndsWith("\\|"))
            text = text[..^1];

        var cells = new List<string>();
        var current = new StringBuilder();
        var escaped = false;
        foreach (var character in text)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (character == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static string ReadQuote(string[] lines, ref int index)
    {
        var inner = new List<string>();
        while (index < lines.Length)
        {
            if (IsBlank(lines[index]))
            {
                if (index + 1 < lines.Length && IsQuoteLine(lines[index + 1]))
                {
                    inner.Add("");
                    index++;
                    continue;
                }

                break;
            }

            if (!IsQuoteLine(lines[index]))
                break;
            inner.Add(StripQuote(lines[index]));
            index++;
        }

        return "<blockquote>\n" + string.Join("\n", RenderBlocks(inner.ToArray())) + "\n</blockquote>";
    }

    private static bool IsQuoteLine(string line)
    {
        var (indent, rest) = SplitIndent(line);
        return indent < 4 && rest.StartsWith('>');
    }

    private static string StripQuote(string line)
    {
        var (_, rest) = SplitIndent(line);
        if (rest.StartsWith('>'))
            rest = rest[1..];
        if (rest.StartsWith(' ') || rest.StartsWith('\t'))
            rest = rest[1..];
        return rest;
    }

    private readonly record struct ListMark(int Indent, bool Ordered, int Number, string Content, string? Task);

    private static ListMark? ListMarker(string line)
    {
        var (indent, rest) = SplitIndent(line);
        if (rest.Length == 0)
            return null;
        var first = rest[0];
        if (first is '-' or '*' or '+')
        {
            if (rest.Length < 2 || (rest[1] != ' ' && rest[1] != '\t'))
                return null;
            var content = rest[2..];
            string? task = null;
            if (content.StartsWith("[ ] ") || content.StartsWith("[x] ") || content.StartsWith("[X] "))
            {
                task = content.StartsWith("[ ] ") ? "☐" : "☑";
                content = content[4..];
            }

            return new ListMark(indent, false, 1, content, task);
        }

        var digits = 0;
        var idx = 0;
        while (idx < rest.Length && char.IsDigit(rest[idx]) && digits < 9)
        {
            digits++;
            idx++;
        }

        if (digits == 0 || idx >= rest.Length || (rest[idx] != '.' && rest[idx] != ')'))
            return null;
        idx++;
        if (idx >= rest.Length || (rest[idx] != ' ' && rest[idx] != '\t'))
            return null;
        idx++;
        var token = rest[..digits];
        var number = int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 1;
        return new ListMark(indent, true, number, rest[idx..], null);
    }

    private static string ReadList(string[] lines, ref int index, int baseIndent)
    {
        var first = ListMarker(lines[index]);
        if (first is null)
            return "";
        var ordered = first.Value.Ordered;
        var items = new List<string>();
        while (index < lines.Length)
        {
            if (IsBlank(lines[index]))
                break;
            var mark = ListMarker(lines[index]);
            if (mark is null || mark.Value.Indent < baseIndent)
                break;
            if (mark.Value.Indent > baseIndent)
            {
                var nested = ReadList(lines, ref index, mark.Value.Indent);
                if (items.Count == 0)
                {
                    items.Add(nested);
                }
                else
                {
                    var last = items[^1];
                    if (last.EndsWith("</li>", StringComparison.Ordinal))
                        last = last[..^5] + "\n" + nested + "</li>";
                    else
                        last += "\n" + nested;
                    items[^1] = last;
                }

                continue;
            }

            if (mark.Value.Ordered != ordered)
                break;
            var content = RenderInline(mark.Value.Content);
            if (mark.Value.Task is not null)
                content = mark.Value.Task + " " + content;
            index++;
            while (index < lines.Length && !IsBlank(lines[index]) && ListMarker(lines[index]) is null)
            {
                if (IndentColumns(lines[index]) < baseIndent + 2)
                    break;
                content += " " + RenderInline(lines[index].Trim());
                index++;
            }

            items.Add("<li>" + content + "</li>");
        }

        var tag = ordered ? "ol" : "ul";
        var start = ordered && first.Value.Number != 1 ? $" start=\"{first.Value.Number}\"" : "";
        return $"<{tag}{start}>\n" + string.Join("\n", items) + $"\n</{tag}>";
    }

    private static string ReadIndentedCode(string[] lines, ref int index)
    {
        var body = new List<string>();
        while (index < lines.Length)
        {
            if (IsBlank(lines[index]))
            {
                if (index + 1 < lines.Length && !IsBlank(lines[index + 1]) && IndentColumns(lines[index + 1]) >= 4)
                {
                    body.Add("");
                    index++;
                    continue;
                }

                break;
            }

            if (IndentColumns(lines[index]) < 4)
                break;
            body.Add(StripIndent(lines[index], 4));
            index++;
        }

        return $"<pre><code>{Escape(string.Join("\n", body))}</code></pre>";
    }

    private static string ReadParagraph(string[] lines, ref int index)
    {
        var collected = new List<string> { lines[index] };
        index++;
        while (index < lines.Length)
        {
            if (IsBlank(lines[index]))
                break;
            if (IsSetextUnderline(lines[index]))
            {
                var level = lines[index].Contains('=') ? 1 : 2;
                var text = string.Join(" ", collected);
                index++;
                return $"<h{level}>{RenderInline(text)}</h{level}>";
            }

            if (FenceStart(lines[index]) is not null
                || AtxHeading(lines[index]) is not null
                || IsThematicBreak(lines[index])
                || IsQuoteLine(lines[index])
                || (ListMarker(lines[index]) is not null && IndentColumns(lines[index]) < 4)
                || IsTableStart(lines, index))
                break;
            collected.Add(lines[index]);
            index++;
        }

        return RenderParagraph(collected);
    }

    private static string RenderParagraph(List<string> lines)
    {
        var chunks = new List<string> { "" };
        for (var offset = 0; offset < lines.Count; offset++)
        {
            var line = lines[offset];
            var hard = false;
            if (line.EndsWith('\\') && !line[..^1].EndsWith('\\'))
            {
                hard = true;
                line = line[..^1];
            }

            var spaces = 0;
            while (line.EndsWith(' '))
            {
                spaces++;
                line = line[..^1];
            }

            if (spaces >= 2)
                hard = true;
            if (chunks[^1].Length > 0)
                chunks[^1] += " ";
            chunks[^1] += line;
            if (hard && offset < lines.Count - 1)
                chunks.Add("");
        }

        return "<p>" + string.Join("<br>\n", chunks.Select(RenderInline)) + "</p>";
    }

    private static bool IsSetextUnderline(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 1 && (trimmed.All(ch => ch == '=') || trimmed.All(ch => ch == '-'));
    }

    private static bool IsThematicBreak(string line)
    {
        var (indent, rest) = SplitIndent(line);
        if (indent >= 4)
            return false;
        var trimmed = rest.Trim();
        if (trimmed.Length < 3)
            return false;
        var marker = trimmed[0];
        if (marker is not ('-' or '*' or '_'))
            return false;
        return trimmed.All(ch => ch == marker || ch is ' ' or '\t') && trimmed.Count(ch => ch == marker) >= 3;
    }

    private static string RenderInline(string text)
    {
        var result = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\\' && index + 1 < text.Length && IsAsciiPunctuation(text[index + 1]))
            {
                result.Append(Escape(text[index + 1].ToString()));
                index += 2;
                continue;
            }

            if (text[index] == '`' && CodeSpan(text, index) is { } span)
            {
                result.Append("<code>").Append(Escape(span.Code)).Append("</code>");
                index = span.End;
                continue;
            }

            if ((text[index] == '!' || text[index] == '[') && LinkOrImage(text, index) is { } link)
            {
                result.Append(link.Html);
                index = link.End;
                continue;
            }

            if (text[index] == '<' && AutoLink(text, index) is { } auto)
            {
                result.Append(auto.Html);
                index = auto.End;
                continue;
            }

            if (Emphasis(text, index) is { } wrapped)
            {
                result.Append(wrapped.Html);
                index = wrapped.End;
                continue;
            }

            result.Append(Escape(text[index].ToString()));
            index++;
        }

        return result.ToString();
    }

    private readonly record struct SpanHit(string Code, int End);
    private readonly record struct HtmlHit(string Html, int End);

    private static SpanHit? CodeSpan(string text, int start)
    {
        var count = 0;
        var index = start;
        while (index < text.Length && text[index] == '`')
        {
            count++;
            index++;
        }

        if (count == 0)
            return null;
        var scan = index;
        while (scan < text.Length)
        {
            if (text[scan] == '`')
            {
                var close = 0;
                var end = scan;
                while (end < text.Length && text[end] == '`')
                {
                    close++;
                    end++;
                }

                if (close == count)
                {
                    var code = text[index..scan].Replace("\n", " ");
                    if (code.StartsWith(' ') && code.EndsWith(' ') && code.Length > 2)
                        code = code[1..^1];
                    return new SpanHit(code, end);
                }

                scan = end;
                continue;
            }

            scan++;
        }

        return null;
    }

    private static HtmlHit? LinkOrImage(string text, int start)
    {
        var index = start;
        var image = text[index] == '!';
        if (image)
        {
            index++;
            if (index >= text.Length || text[index] != '[')
                return null;
        }

        var labelEnd = ClosingBracket(text, index + 1);
        if (labelEnd < 0)
            return null;
        var label = text[(index + 1)..labelEnd];
        var destStart = labelEnd + 1;
        if (destStart >= text.Length || text[destStart] != '(')
            return null;
        destStart++;
        var destEnd = ClosingParen(text, destStart);
        if (destEnd < 0)
            return null;
        var destination = text[destStart..destEnd].Trim();
        var space = destination.IndexOfAny([' ', '\t', '\n']);
        if (space >= 0)
            destination = destination[..space];
        if (destination.StartsWith('<') && destination.EndsWith('>') && destination.Length >= 2)
            destination = destination[1..^1];
        var after = destEnd + 1;
        var labelHtml = RenderInline(label);
        var href = SafeUrl(destination);
        if (href is null)
            return new HtmlHit(labelHtml, after);
        if (image)
            return new HtmlHit($"<img src=\"{href}\" alt=\"{Escape(StripTags(labelHtml))}\">", after);
        return new HtmlHit($"<a href=\"{href}\">{labelHtml}</a>", after);
    }

    private static HtmlHit? AutoLink(string text, int start)
    {
        var close = text.IndexOf('>', start + 1);
        if (close < 0)
            return null;
        var inner = text[(start + 1)..close];
        if (inner.Length == 0 || inner.Contains(' ') || inner.Contains('<'))
            return null;
        var end = close + 1;
        var lower = inner.ToLowerInvariant();
        if (lower.StartsWith("http://") || lower.StartsWith("https://") || lower.StartsWith("mailto:"))
        {
            var href = SafeUrl(inner);
            return href is null ? null : new HtmlHit($"<a href=\"{href}\">{Escape(inner)}</a>", end);
        }

        var parts = inner.Split('@');
        if (parts.Length == 2 && !inner.Contains(':') && parts.All(part => part.Length > 0))
        {
            var href = SafeUrl("mailto:" + inner);
            return href is null ? null : new HtmlHit($"<a href=\"{href}\">{Escape(inner)}</a>", end);
        }

        return null;
    }

    private static HtmlHit? Emphasis(string text, int start)
    {
        var character = text[start];
        if (character is not ('*' or '_' or '~'))
            return null;
        var run = RunLength(text, start, character);
        int count;
        string tag;
        if (character == '~')
        {
            if (run < 2)
                return null;
            count = 2;
            tag = "del";
        }
        else if (run >= 2)
        {
            count = 2;
            tag = "strong";
        }
        else
        {
            count = 1;
            tag = "em";
        }

        var contentStart = start + count;
        if (contentStart >= text.Length)
            return null;
        if (character != '~' && char.IsWhiteSpace(text[contentStart]))
            return null;
        if (character == '_' && !CanOpenUnderscore(text, start))
            return null;
        var close = FindCloser(text, contentStart, character, count);
        if (close < 0)
            return null;
        if (character == '_' && !CanCloseUnderscore(text, close))
            return null;
        var inner = text[contentStart..close];
        if (inner.Length == 0)
            return null;
        return new HtmlHit($"<{tag}>{RenderInline(inner)}</{tag}>", close + count);
    }

    private static int FindCloser(string text, int start, char character, int count)
    {
        var index = start;
        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += index + 1 < text.Length ? 2 : 1;
                continue;
            }

            if (text[index] == '`')
            {
                var span = CodeSpan(text, index);
                if (span is not null)
                {
                    index = span.Value.End;
                    continue;
                }
            }

            if (text[index] == character)
            {
                var run = RunLength(text, index, character);
                if (run == count && (character == '~' || !char.IsWhiteSpace(text[index - 1])))
                    return index;
                index += run;
                continue;
            }

            index++;
        }

        return -1;
    }

    private static int RunLength(string text, int start, char character)
    {
        var count = 0;
        while (start + count < text.Length && text[start + count] == character)
            count++;
        return count;
    }

    private static bool CanOpenUnderscore(string text, int index)
    {
        if (index <= 0)
            return true;
        var previous = text[index - 1];
        return !(char.IsLetter(previous) || char.IsDigit(previous));
    }

    private static bool CanCloseUnderscore(string text, int index)
    {
        var after = index + RunLength(text, index, '_');
        if (after >= text.Length)
            return true;
        var next = text[after];
        return !(char.IsLetter(next) || char.IsDigit(next));
    }

    private static int ClosingBracket(string text, int start)
    {
        var escaped = false;
        for (var index = start; index < text.Length; index++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (text[index] == '\\')
            {
                escaped = true;
                continue;
            }

            if (text[index] == ']')
                return index;
            if (text[index] == '[')
                return -1;
        }

        return -1;
    }

    private static int ClosingParen(string text, int start)
    {
        var depth = 0;
        var escaped = false;
        for (var index = start; index < text.Length; index++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (text[index] == '\\')
            {
                escaped = true;
                continue;
            }

            if (text[index] == '(')
                depth++;
            if (text[index] == ')')
            {
                if (depth == 0)
                    return index;
                depth--;
            }
        }

        return -1;
    }

    private static bool IsBlank(string line) => line.All(ch => ch is ' ' or '\t');

    private static (int Indent, string Remainder) SplitIndent(string line)
    {
        var columns = 0;
        var index = 0;
        while (index < line.Length && columns <= 32)
        {
            if (line[index] == ' ')
                columns++;
            else if (line[index] == '\t')
                columns += 4;
            else
                break;
            index++;
        }

        return (columns, line[index..]);
    }

    private static int IndentColumns(string line) => SplitIndent(line).Indent;

    private static string StripIndent(string line, int columns)
    {
        var left = columns;
        var index = 0;
        while (index < line.Length && left > 0)
        {
            if (line[index] == ' ')
                left--;
            else if (line[index] == '\t')
                left -= 4;
            else
                break;
            index++;
        }

        return line[index..];
    }

    private static bool IsAsciiPunctuation(char character)
    {
        var value = (int)character;
        return (value >= 33 && value <= 47) || (value >= 58 && value <= 64) || (value >= 91 && value <= 96) || (value >= 123 && value <= 126);
    }

    private static string StripTags(string html)
    {
        var result = new StringBuilder();
        var inside = false;
        foreach (var character in html)
        {
            if (character == '<')
            {
                inside = true;
                continue;
            }

            if (character == '>')
            {
                inside = false;
                continue;
            }

            if (!inside)
                result.Append(character);
        }

        return WebUtility.HtmlDecode(result.ToString());
    }
}
