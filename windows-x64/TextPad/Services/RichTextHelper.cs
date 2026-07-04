using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TextPad.Models;

namespace TextPad.Services;

public static class RichTextHelper
{
    public static void LoadRtf(RichTextBox editor, byte[] data, bool applyTheme = true)
    {
        using var stream = new MemoryStream(data);
        var range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        range.Load(stream, DataFormats.Rtf);
        if (applyTheme)
            ApplyTheme(editor, EditorPreferences.Instance.EffectiveTheme);
    }

    public static void ApplyTheme(RichTextBox editor, EditorTheme theme)
    {
        editor.Background = new SolidColorBrush(theme.Background);
        editor.Foreground = new SolidColorBrush(theme.Text);
        editor.Document.Foreground = new SolidColorBrush(theme.Text);
        editor.Document.Background = new SolidColorBrush(theme.Background);

        var selectionBrush = new SolidColorBrush(theme.Selection);
        selectionBrush.Freeze();
        var selectionForeground = new SolidColorBrush(theme.Text);
        selectionForeground.Freeze();
        editor.SelectionBrush = selectionBrush;
        editor.SelectionTextBrush = selectionForeground;
        editor.SelectionOpacity = 1.0;

        RemapDocumentColors(editor.Document, theme);
    }

    public static void ApplyExportTheme(RichTextBox editor)
    {
        var exportTheme = EditorTheme.For(EditorThemeKind.Light);
        ApplyTheme(editor, exportTheme);
        NormalizeExportColors(editor, exportTheme);
    }

    public static byte[] BuildRtfFromPlainText(string text)
    {
        var editor = new RichTextBox();
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text = text;
        return SaveRtf(editor);
    }

    public static byte[] SaveRtf(RichTextBox editor)
    {
        using var stream = new MemoryStream();
        var range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        range.Save(stream, DataFormats.Rtf);
        return stream.ToArray();
    }

    public static string GetPlainText(RichTextBox editor) =>
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;

    public static string GetSelectedText(RichTextBox editor) =>
        editor.Selection.IsEmpty
            ? string.Empty
            : new TextRange(editor.Selection.Start, editor.Selection.End).Text;

    public static int GetSelectionStart(RichTextBox editor) =>
        GetOffset(editor.Document.ContentStart, editor.Selection.Start);

    public static int GetCharacterCount(RichTextBox editor)
    {
        var position = editor.Document.ContentStart;
        var end = editor.Document.ContentEnd;
        var count = 0;
        while (position is not null && position.CompareTo(end) < 0)
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                count += position.GetTextInRun(LogicalDirection.Forward).Length;
            position = position.GetNextInsertionPosition(LogicalDirection.Forward);
        }

        return count;
    }

    public static int GetTextLength(RichTextBox editor) => GetCharacterCount(editor);

    public static void SelectRange(RichTextBox editor, int start, int length)
    {
        var startPtr = GetPointerAtOffset(editor.Document.ContentStart, start);
        var endPtr = GetPointerAtOffset(editor.Document.ContentStart, start + length);
        if (startPtr is null || endPtr is null)
            return;

        editor.Selection.Select(startPtr, endPtr);
        editor.CaretPosition = endPtr;
        editor.Focus();
    }

    public static void ReplaceRange(RichTextBox editor, int start, int length, string replacement)
    {
        var startPtr = GetPointerAtOffset(editor.Document.ContentStart, start);
        var endPtr = GetPointerAtOffset(editor.Document.ContentStart, start + length);
        if (startPtr is null || endPtr is null)
            return;

        editor.Selection.Select(startPtr, endPtr);
        editor.Selection.Text = replacement;
    }

    public static (int Line, int Column) GetCaretPosition(RichTextBox editor)
    {
        var offset = GetOffset(editor.Document.ContentStart, editor.CaretPosition);
        var text = GetPlainText(editor);
        if (offset <= 0 || text.Length == 0)
            return (1, 1);

        var line = 1;
        var col = 1;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }

        return (line, col);
    }

    public static int GetLineCount(RichTextBox editor)
    {
        var text = GetPlainText(editor);
        if (string.IsNullOrEmpty(text))
            return 1;
        return text.Split('\n').Length;
    }

    public static void GoToLine(RichTextBox editor, int line)
    {
        var text = GetPlainText(editor);
        var target = Math.Max(1, line);
        var currentLine = 1;
        var offset = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (currentLine == target)
                break;
            if (text[i] == '\n')
            {
                currentLine++;
                offset = i + 1;
            }
        }

        var pointer = GetPointerAtOffset(editor.Document.ContentStart, offset);
        if (pointer is not null)
        {
            editor.CaretPosition = pointer;
            editor.Selection.Select(pointer, pointer);
        }
    }

    private static int GetOffset(TextPointer start, TextPointer position) =>
        new TextRange(start, position).Text.Length;

    private static TextPointer? GetPointerAtOffset(TextPointer start, int offset)
    {
        var current = start;
        var remaining = offset;

        while (current != null && remaining > 0)
        {
            if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var run = current.GetTextInRun(LogicalDirection.Forward);
                if (run.Length <= remaining)
                {
                    remaining -= run.Length;
                    current = current.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
                }
                else
                {
                    return current.GetPositionAtOffset(remaining, LogicalDirection.Forward);
                }
            }
            else
            {
                current = current.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        return current ?? start;
    }

    private static void NormalizeExportColors(RichTextBox editor, EditorTheme exportTheme)
    {
        editor.Background = Brushes.White;
        editor.Foreground = new SolidColorBrush(exportTheme.Text);
        editor.Document.Background = Brushes.White;
        editor.Document.Foreground = new SolidColorBrush(exportTheme.Text);
        NormalizeExportBlocks(editor.Document.Blocks, exportTheme);
    }

    private static void NormalizeExportBlocks(BlockCollection blocks, EditorTheme exportTheme)
    {
        foreach (var block in blocks)
            NormalizeExportBlock(block, exportTheme);
    }

    private static void NormalizeExportBlock(Block block, EditorTheme exportTheme)
    {
        switch (block)
        {
            case Paragraph paragraph:
                NormalizeExportTextElement(paragraph, exportTheme);
                NormalizeExportInlines(paragraph.Inlines, exportTheme);
                break;
            case Section section:
                NormalizeExportBlocks(section.Blocks, exportTheme);
                break;
            case List list:
                foreach (ListItem item in list.ListItems)
                    NormalizeExportBlocks(item.Blocks, exportTheme);
                break;
            case Table table:
                foreach (var rowGroup in table.RowGroups)
                {
                    foreach (var row in rowGroup.Rows)
                    {
                        foreach (var cell in row.Cells)
                            NormalizeExportBlocks(cell.Blocks, exportTheme);
                    }
                }
                break;
        }
    }

    private static void NormalizeExportInlines(InlineCollection inlines, EditorTheme exportTheme, bool insideLink = false)
    {
        foreach (var inline in inlines)
            NormalizeExportInline(inline, exportTheme, insideLink);
    }

    private static void NormalizeExportInline(Inline inline, EditorTheme exportTheme, bool insideLink)
    {
        var inLink = insideLink || inline is Hyperlink;
        if (!inLink)
            NormalizeExportTextElement(inline, exportTheme);

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
                NormalizeExportInline(child, exportTheme, inLink);
        }
    }

    private static void NormalizeExportTextElement(TextElement element, EditorTheme exportTheme)
    {
        var foreground = GetBrushColor(element.GetValue(TextElement.ForegroundProperty), Colors.Black);
        if (ShouldNormalizeForegroundForExport(foreground))
            element.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(exportTheme.Text));

        var background = GetBrushColor(element.GetValue(TextElement.BackgroundProperty), Colors.Transparent);
        if (ShouldNormalizeBackgroundForExport(background))
            element.SetValue(TextElement.BackgroundProperty, new SolidColorBrush(exportTheme.Selection));
    }

    private static bool ShouldNormalizeForegroundForExport(Color color)
    {
        if (color.A < 16)
            return false;

        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var maxChannel = Math.Max(r, Math.Max(g, b));
        var minChannel = Math.Min(r, Math.Min(g, b));
        var saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel;
        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

        return saturation < 0.2 && luminance > 0.55;
    }

    private static bool ShouldNormalizeBackgroundForExport(Color color)
    {
        if (color.A < 32)
            return false;

        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 0.45;
    }

    private static void RemapDocumentColors(FlowDocument document, EditorTheme theme)
    {
        RemapBlocks(document.Blocks, theme);
    }

    private static void RemapBlocks(BlockCollection blocks, EditorTheme theme)
    {
        foreach (var block in blocks)
            RemapBlock(block, theme);
    }

    private static void RemapBlock(Block block, EditorTheme theme)
    {
        switch (block)
        {
            case Paragraph paragraph:
                RemapInlines(paragraph.Inlines, theme);
                break;
            case Section section:
                RemapBlocks(section.Blocks, theme);
                break;
            case List list:
                foreach (ListItem item in list.ListItems)
                    RemapBlocks(item.Blocks, theme);
                break;
            case Table table:
                foreach (var rowGroup in table.RowGroups)
                {
                    foreach (var row in rowGroup.Rows)
                    {
                        foreach (var cell in row.Cells)
                            RemapBlocks(cell.Blocks, theme);
                    }
                }
                break;
        }
    }

    private static void RemapInlines(InlineCollection inlines, EditorTheme theme, bool insideLink = false)
    {
        foreach (var inline in inlines)
            RemapInline(inline, theme, insideLink);
    }

    private static void RemapInline(Inline inline, EditorTheme theme, bool insideLink)
    {
        var inLink = insideLink || inline is Hyperlink;

        if (inline is Hyperlink)
            inline.Foreground = new SolidColorBrush(theme.UiAccent);
        else
        {
            var foreground = GetBrushColor(inline.GetValue(TextElement.ForegroundProperty), Colors.Black);
            if (ShouldRemapForeground(foreground, theme))
                inline.Foreground = new SolidColorBrush(inLink ? theme.UiAccent : theme.Text);
        }

        var background = GetBrushColor(inline.GetValue(TextElement.BackgroundProperty), Colors.Transparent);
        if (background.A > 0 && ShouldRemapHighlight(background, theme))
            inline.Background = new SolidColorBrush(theme.Selection);

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
                RemapInline(child, theme, inLink);
        }
    }

    private static Color GetBrushColor(object? value, Color fallback)
    {
        if (value is SolidColorBrush brush)
            return brush.Color;
        return fallback;
    }

    private static bool ShouldRemapForeground(Color color, EditorTheme theme)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var maxChannel = Math.Max(r, Math.Max(g, b));
        var minChannel = Math.Min(r, Math.Min(g, b));
        var saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel;
        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

        if (saturation > 0.18)
            return false;

        return theme.IsDark ? luminance < 0.55 : luminance > 0.65;
    }

    private static bool ShouldRemapHighlight(Color color, EditorTheme theme)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        var alpha = color.A / 255.0;

        if (theme.IsDark)
            return luminance < 0.22 && alpha > 0.2;
        return luminance > 0.92 && alpha > 0.2;
    }
}