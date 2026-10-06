using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace TextPad.Services;

/// <summary>
/// Writes a rich-text document as Markdown. Plain text is converted by PlainTextMarkdown.
/// </summary>
public static class RichTextMarkdown
{
    public static string Export(FlowDocument document)
    {
        var blocks = new List<BlockPiece>();
        foreach (var block in document.Blocks)
            ExportBlock(block, blocks, depth: 0);

        return Join(blocks);
    }

    private readonly record struct BlockPiece(string Text, bool IsList, bool Ordered = false, int Level = 0);

    private readonly record struct Piece(
        string Text,
        bool Bold,
        bool Italic,
        bool Strike,
        bool Underline,
        string? Link,
        bool HardBreak)
    {
        public static Piece Break { get; } = new("", false, false, false, false, null, true);
    }

    private static void ExportBlock(Block block, List<BlockPiece> output, int depth)
    {
        switch (block)
        {
            case Paragraph paragraph:
                var text = Render(Collect(paragraph.Inlines), inTable: false);
                if (text.Trim().Length == 0)
                {
                    if (output.Count > 0)
                        output.Add(new BlockPiece("", false));
                    return;
                }

                output.Add(new BlockPiece(text, false));
                break;
            case Section section:
                foreach (var child in section.Blocks)
                    ExportBlock(child, output, depth);
                break;
            case List list:
                ExportList(list, output, depth);
                break;
            case Table table:
                var markdown = ExportTable(table);
                if (markdown.Length > 0)
                    output.Add(new BlockPiece(markdown, false));
                break;
        }
    }

    private static void ExportList(List list, List<BlockPiece> output, int depth)
    {
        var ordered = list.MarkerStyle is TextMarkerStyle.Decimal
            or TextMarkerStyle.LowerLatin
            or TextMarkerStyle.UpperLatin
            or TextMarkerStyle.LowerRoman
            or TextMarkerStyle.UpperRoman;
        var number = Math.Max(1, list.StartIndex);
        foreach (ListItem item in list.ListItems)
        {
            var indent = new string(' ', depth * 2);
            var marker = ordered ? $"{number}. " : "- ";
            number++;
            var wroteMarker = false;
            foreach (var child in item.Blocks)
            {
                switch (child)
                {
                    case Paragraph paragraph:
                        var text = Render(Collect(paragraph.Inlines), inTable: false);
                        if (!wroteMarker)
                        {
                            output.Add(new BlockPiece(indent + marker + text, true, ordered, depth));
                            wroteMarker = true;
                        }
                        else if (text.Trim().Length > 0)
                        {
                            output.Add(new BlockPiece(indent + "  " + text, true, ordered, depth));
                        }
                        break;
                    case List nested:
                        ExportList(nested, output, depth + 1);
                        wroteMarker = true;
                        break;
                    default:
                        ExportBlock(child, output, depth + 1);
                        wroteMarker = true;
                        break;
                }
            }

            if (!wroteMarker)
                output.Add(new BlockPiece(indent + marker.TrimEnd(), true, ordered, depth));
        }
    }

    private static string ExportTable(Table table)
    {
        var rows = new List<List<string>>();
        var columns = 0;
        foreach (var group in table.RowGroups)
        {
            foreach (var row in group.Rows)
            {
                var cells = new List<string>();
                foreach (var cell in row.Cells)
                {
                    var text = new StringBuilder();
                    foreach (var child in cell.Blocks)
                    {
                        if (child is not Paragraph paragraph)
                            continue;
                        if (text.Length > 0)
                            text.Append(' ');
                        text.Append(Render(Collect(paragraph.Inlines), inTable: true).Replace("\n", " "));
                    }

                    var span = Math.Max(1, cell.ColumnSpan);
                    cells.Add(text.ToString());
                    for (var extra = 1; extra < span; extra++)
                        cells.Add("");
                }

                columns = Math.Max(columns, cells.Count);
                rows.Add(cells);
            }
        }

        if (rows.Count == 0)
            return "";

        var width = Math.Max(columns, 1);
        string Line(List<string> cells)
        {
            var padded = new List<string>(cells);
            while (padded.Count < width)
                padded.Add("");
            return "| " + string.Join(" | ", padded) + " |";
        }

        var lines = new List<string> { Line(rows[0]), "| " + string.Join(" | ", Enumerable.Repeat("---", width)) + " |" };
        for (var index = 1; index < rows.Count; index++)
            lines.Add(Line(rows[index]));
        return string.Join("\n", lines);
    }

    private static List<Piece> Collect(InlineCollection inlines)
    {
        var pieces = new List<Piece>();
        foreach (var inline in inlines)
            CollectInline(inline, null, pieces);
        return Merge(pieces);
    }

    private static void CollectInline(Inline inline, string? link, List<Piece> pieces)
    {
        var nextLink = inline is Hyperlink hyperlink
            ? hyperlink.NavigateUri?.ToString() ?? ""
            : link;
        switch (inline)
        {
            case Hyperlink hyperlink:
                foreach (var child in hyperlink.Inlines)
                    CollectInline(child, nextLink, pieces);
                break;
            case Run run:
                var parts = (run.Text ?? string.Empty).Split('\u2028');
                for (var index = 0; index < parts.Length; index++)
                {
                    if (index > 0)
                        pieces.Add(Piece.Break);
                    pieces.Add(new Piece(parts[index], IsBold(run), IsItalic(run), IsStrikethrough(run), IsUnderline(run), nextLink, false));
                }
                break;
            case Span span:
                foreach (var child in span.Inlines)
                    CollectInline(child, nextLink, pieces);
                break;
            case LineBreak:
                pieces.Add(Piece.Break);
                break;
        }
    }

    private static List<Piece> Merge(List<Piece> pieces)
    {
        var merged = new List<Piece>();
        foreach (var piece in pieces)
        {
            if (piece.HardBreak || merged.Count == 0)
            {
                merged.Add(piece);
                continue;
            }

            var last = merged[^1];
            if (!last.HardBreak && last.Bold == piece.Bold && last.Italic == piece.Italic &&
                last.Strike == piece.Strike && last.Underline == piece.Underline && last.Link == piece.Link)
            {
                merged[^1] = last with { Text = last.Text + piece.Text };
            }
            else
            {
                merged.Add(piece);
            }
        }

        return merged;
    }

    private static string Render(List<Piece> pieces, bool inTable)
    {
        var output = new StringBuilder();
        var atLineStart = true;
        foreach (var piece in pieces)
        {
            if (piece.HardBreak)
            {
                output.Append(inTable ? " " : "  \n");
                atLineStart = true;
                continue;
            }

            output.Append(Wrap(piece, atLineStart && !inTable, inTable));
            atLineStart = false;
        }

        return output.ToString();
    }

    private static string Wrap(Piece piece, bool atLineStart, bool inTable)
    {
        if (piece.Text.Length == 0)
            return "";
        if (string.IsNullOrWhiteSpace(piece.Text))
            return piece.Text;

        var text = Escape(piece.Text, atLineStart, inTable);
        if (piece.Bold && piece.Italic)
            text = $"***{text}***";
        else if (piece.Bold)
            text = $"**{text}**";
        else if (piece.Italic)
            text = $"*{text}*";
        if (piece.Strike)
            text = $"~~{text}~~";
        if (piece.Underline)
            text = $"<u>{text}</u>";
        if (!string.IsNullOrEmpty(piece.Link))
            text = $"[{text}]({LinkDestination(piece.Link)})";
        return text;
    }

    private static string LinkDestination(string link)
    {
        if (link.Contains(' ') || link.Contains('(') || link.Contains(')'))
            return "<" + link.Replace(">", "%3E") + ">";
        return link;
    }

    private static string Escape(string text, bool atLineStart, bool inTable) =>
        PlainTextMarkdown.Escape(text, atLineStart, inTable);

    private static string Join(List<BlockPiece> blocks)
    {
        var output = new StringBuilder();
        BlockPiece? previous = null;
        foreach (var block in blocks)
        {
            if (!block.IsList && block.Text.Length == 0)
            {
                previous = block;
                continue;
            }

            if (output.Length == 0)
                output.Append(block.Text);
            else if (ContinuesList(block, previous))
                output.Append('\n').Append(block.Text);
            else
                output.Append("\n\n").Append(block.Text);
            previous = block;
        }

        if (output.Length == 0)
            return "";
        if (output[^1] != '\n')
            output.Append('\n');
        return output.ToString();
    }

    /// <summary>
    /// Same-list items stay on consecutive lines, including a nested item under its parent.
    /// A bullet list followed by a numbered list is a new list, so it gets a blank line.
    /// </summary>
    private static bool ContinuesList(BlockPiece block, BlockPiece? previous)
    {
        if (previous is not { IsList: true } prior || !block.IsList)
            return false;
        if (block.Level > prior.Level)
            return true;
        return block.Ordered == prior.Ordered;
    }

    private static bool IsBold(TextElement element) =>
        element.GetValue(TextElement.FontWeightProperty) is FontWeight weight && weight >= FontWeights.Bold;

    private static bool IsItalic(TextElement element) =>
        element.GetValue(TextElement.FontStyleProperty) is FontStyle style &&
        (style == FontStyles.Italic || style == FontStyles.Oblique);

    private static bool IsUnderline(TextElement element) => HasDecoration(element, TextDecorations.Underline);

    private static bool IsStrikethrough(TextElement element) => HasDecoration(element, TextDecorations.Strikethrough);

    private static bool HasDecoration(TextElement element, TextDecorationCollection target)
    {
        if (element.GetValue(Inline.TextDecorationsProperty) is not TextDecorationCollection decorations)
            return false;

        foreach (var decoration in decorations)
        {
            foreach (var targetDecoration in target)
            {
                if (decoration.Location == targetDecoration.Location)
                    return true;
            }
        }

        return false;
    }
}
