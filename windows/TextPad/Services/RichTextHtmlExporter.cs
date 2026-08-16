using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TextPad.Services;

public static class RichTextHtmlExporter
{
    public static string Export(FlowDocument document)
    {
        var body = new StringBuilder();
        foreach (var block in document.Blocks)
            ExportBlock(block, body);

        var html = body.ToString().Trim();
        return string.IsNullOrEmpty(html) ? "<p></p>" : html;
    }

    private static void ExportBlock(Block block, StringBuilder output)
    {
        switch (block)
        {
            case Paragraph paragraph:
                ExportParagraph(paragraph, output);
                break;
            case Section section:
                output.Append("<div>");
                foreach (var child in section.Blocks)
                    ExportBlock(child, output);
                output.Append("</div>");
                break;
            case List list:
                ExportList(list, output);
                break;
            case Table table:
                ExportTable(table, output);
                break;
            case BlockUIContainer ui:
                if (ui.Child is not null)
                    output.Append($"<!-- embedded UI: {WebUtility.HtmlEncode(ui.Child.GetType().Name)} -->");
                break;
        }
    }

    private static void ExportParagraph(Paragraph paragraph, StringBuilder output)
    {
        var style = ParagraphStyle(paragraph);
        output.Append(style.Length > 0 ? $"<p style=\"{style}\">" : "<p>");
        ExportInlines(paragraph.Inlines, output);
        output.Append("</p>\n");
    }

    private static void ExportList(List list, StringBuilder output)
    {
        var tag = list.MarkerStyle == TextMarkerStyle.Decimal ? "ol" : "ul";
        output.Append('<').Append(tag).Append('>');
        foreach (ListItem item in list.ListItems)
        {
            output.Append("<li>");
            foreach (var child in item.Blocks)
            {
                if (child is Paragraph paragraph)
                    ExportInlines(paragraph.Inlines, output);
                else
                    ExportBlock(child, output);
            }
            output.Append("</li>");
        }
        output.Append("</").Append(tag).Append('>');
    }

    private static void ExportTable(Table table, StringBuilder output)
    {
        output.Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">");
        foreach (var rowGroup in table.RowGroups)
        {
            foreach (var row in rowGroup.Rows)
            {
                output.Append("<tr>");
                foreach (var cell in row.Cells)
                {
                    output.Append("<td>");
                    foreach (var child in cell.Blocks)
                        ExportBlock(child, output);
                    output.Append("</td>");
                }
                output.Append("</tr>");
            }
        }
        output.Append("</table>");
    }

    private static void ExportInlines(InlineCollection inlines, StringBuilder output)
    {
        foreach (var inline in inlines)
            ExportInline(inline, output);
    }

    private static void ExportInline(Inline inline, StringBuilder output)
    {
        switch (inline)
        {
            case Run run:
                ExportStyledText(run.Text, run, output);
                break;
            case Hyperlink link:
            {
                var href = WebUtility.HtmlEncode(link.NavigateUri?.ToString() ?? string.Empty);
                output.Append($"<a href=\"{href}\">");
                ExportInlines(link.Inlines, output);
                output.Append("</a>");
                break;
            }
            case Span span:
                ExportStyledOpen(span, output);
                ExportInlines(span.Inlines, output);
                ExportStyledClose(span, output);
                break;
            case LineBreak:
                output.Append("<br/>");
                break;
            case InlineUIContainer ui:
                if (ui.Child is not null)
                    output.Append($"<!-- inline UI: {WebUtility.HtmlEncode(ui.Child.GetType().Name)} -->");
                break;
        }
    }

    private static void ExportStyledText(string? text, TextElement element, StringBuilder output)
    {
        if (string.IsNullOrEmpty(text))
            return;

        ExportStyledOpen(element, output);
        output.Append(WebUtility.HtmlEncode(text));
        ExportStyledClose(element, output);
    }

    private static void ExportStyledOpen(TextElement element, StringBuilder output)
    {
        if (IsBold(element))
            output.Append("<strong>");
        if (IsItalic(element))
            output.Append("<em>");
        if (IsUnderline(element))
            output.Append("<u>");
        if (IsStrikethrough(element))
            output.Append("<s>");

        var style = InlineStyle(element);
        if (style.Length > 0)
            output.Append($"<span style=\"{style}\">");
    }

    private static void ExportStyledClose(TextElement element, StringBuilder output)
    {
        var style = InlineStyle(element);
        if (style.Length > 0)
            output.Append("</span>");
        if (IsStrikethrough(element))
            output.Append("</s>");
        if (IsUnderline(element))
            output.Append("</u>");
        if (IsItalic(element))
            output.Append("</em>");
        if (IsBold(element))
            output.Append("</strong>");
    }

    private static string ParagraphStyle(Paragraph paragraph)
    {
        var styles = new List<string>();
        var align = paragraph.TextAlignment switch
        {
            TextAlignment.Center => "center",
            TextAlignment.Right => "right",
            TextAlignment.Justify => "justify",
            _ => string.Empty
        };
        if (align.Length > 0)
            styles.Add($"text-align:{align}");

        AppendColorStyles(paragraph, styles);
        return string.Join(';', styles);
    }

    private static string InlineStyle(TextElement element)
    {
        var styles = new List<string>();
        AppendColorStyles(element, styles);

        if (element is Inline inline && inline.FontSize > 0 && Math.Abs(inline.FontSize - 12) > 0.1)
            styles.Add($"font-size:{inline.FontSize:0.##}pt");

        if (element is Inline inline2 && inline2.FontFamily is not null)
            styles.Add($"font-family:{inline2.FontFamily.Source}");

        return string.Join(';', styles);
    }

    private static void AppendColorStyles(TextElement element, ICollection<string> styles)
    {
        if (element.GetValue(TextElement.ForegroundProperty) is SolidColorBrush foreground
            && foreground.Color.A > 0
            && IsExportSafeForeground(foreground.Color))
            styles.Add($"color:{ToHex(foreground.Color)}");

        if (element.GetValue(TextElement.BackgroundProperty) is SolidColorBrush background
            && background.Color.A > 0
            && IsExportSafeBackground(background.Color))
            styles.Add($"background-color:{ToHex(background.Color)}");
    }

    private static bool IsExportSafeForeground(Color color)
    {
        if (IsNearColor(color, Colors.Black))
            return false;

        var luminance = GetLuminance(color);
        var saturation = GetSaturation(color);
        return saturation >= 0.2 || luminance < 0.55;
    }

    private static bool IsExportSafeBackground(Color color)
    {
        if (IsNearColor(color, Colors.White))
            return false;

        return GetLuminance(color) > 0.7;
    }

    private static double GetLuminance(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double GetSaturation(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var maxChannel = Math.Max(r, Math.Max(g, b));
        var minChannel = Math.Min(r, Math.Min(g, b));
        return maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel;
    }

    private static bool IsBold(TextElement element) =>
        element.GetValue(TextElement.FontWeightProperty) is FontWeight weight && weight >= FontWeights.Bold;

    private static bool IsItalic(TextElement element) =>
        element.GetValue(TextElement.FontStyleProperty) is FontStyle style && style == FontStyles.Italic;

    private static bool IsUnderline(TextElement element) =>
        HasDecoration(element, TextDecorations.Underline);

    private static bool IsStrikethrough(TextElement element) =>
        HasDecoration(element, TextDecorations.Strikethrough);

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

    private static string ToHex(Color color) =>
        color.A < 255
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool IsNearColor(Color a, Color b) =>
        Math.Abs(a.R - b.R) < 8 && Math.Abs(a.G - b.G) < 8 && Math.Abs(a.B - b.B) < 8;
}