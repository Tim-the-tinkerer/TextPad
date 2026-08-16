using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        // Cocoa/email RTF often uses nested tables that WPF lays out as broken
        // side-by-side columns (headers left, body right). Flatten to vertical flow.
        FlattenComplexTables(editor.Document);
        RestoreNamedFontsFromRtf(editor.Document, data);
        if (applyTheme)
            ApplyTheme(editor, EditorPreferences.Instance.EffectiveTheme);
    }

    private static readonly HashSet<string> GenericFontFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Times New Roman",
        "Segoe UI",
        "Microsoft Sans Serif",
        "serif",
        "sans-serif",
        "Global User Interface",
        "Global Monospace",
        "Global Sans Serif",
        "Global Serif"
    };

    private static readonly Regex RtfDefaultFontRegex = new(
        @"\\deff(\d+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex RtfFontEntryRegex = new(
        @"\\f(\d+)(?:\\[a-zA-Z]+[0-9]*)*\s+([^;{}]+);",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// WPF's RTF reader often falls back to Times New Roman for named faces
    /// in the font table (especially <c>\fnil</c> entries). Re-apply those
    /// names when the document asked for a real family such as Interlac Unicode.
    /// </summary>
    private static void RestoreNamedFontsFromRtf(FlowDocument document, byte[] rtf)
    {
        if (!TryReadRtfFontTable(rtf, out var fonts, out var defaultId))
            return;

        if (!fonts.TryGetValue(defaultId, out var defaultName) || IsGenericFont(defaultName))
            defaultName = fonts.Values.FirstOrDefault(name => !IsGenericFont(name)) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(defaultName))
            return;

        var tableNames = new HashSet<string>(fonts.Values, StringComparer.OrdinalIgnoreCase);
        if (IsGenericFont(document.FontFamily?.Source) && !TableRequestsFont(tableNames, document.FontFamily?.Source))
            document.FontFamily = BundledFonts.Resolve(defaultName);

        RestoreNamedFonts(document.Blocks, tableNames, defaultName);
    }

    private static bool TryReadRtfFontTable(
        byte[] rtf,
        out Dictionary<int, string> fonts,
        out int defaultId)
    {
        fonts = new Dictionary<int, string>();
        defaultId = 0;
        var text = Encoding.Latin1.GetString(rtf);
        var tableStart = text.IndexOf("{\\fonttbl", StringComparison.Ordinal);
        if (tableStart < 0)
            return false;

        var tableEnd = text.IndexOf("}}", tableStart, StringComparison.Ordinal);
        var table = tableEnd >= 0 ? text[tableStart..(tableEnd + 2)] : text[tableStart..];

        var defaultMatch = RtfDefaultFontRegex.Match(text[..Math.Min(text.Length, tableStart + 32)]);
        if (defaultMatch.Success)
            defaultId = int.Parse(defaultMatch.Groups[1].Value);

        foreach (Match match in RtfFontEntryRegex.Matches(table))
        {
            var name = match.Groups[2].Value.Trim();
            if (name.StartsWith("\\", StringComparison.Ordinal))
                continue;
            fonts[int.Parse(match.Groups[1].Value)] = name;
        }

        return fonts.Count > 0;
    }

    private static void RestoreNamedFonts(
        IEnumerable<Block> blocks,
        HashSet<string> tableNames,
        string fallback)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    RestoreElementFont(paragraph, tableNames, fallback);
                    RestoreNamedFonts(paragraph.Inlines, tableNames, fallback);
                    break;
                case Section section:
                    RestoreNamedFonts(section.Blocks, tableNames, fallback);
                    break;
                case List list:
                    foreach (ListItem item in list.ListItems)
                        RestoreNamedFonts(item.Blocks, tableNames, fallback);
                    break;
                case Table table:
                    foreach (var rowGroup in table.RowGroups)
                    foreach (var row in rowGroup.Rows)
                    foreach (var cell in row.Cells)
                        RestoreNamedFonts(cell.Blocks, tableNames, fallback);
                    break;
            }
        }
    }

    private static void RestoreNamedFonts(
        InlineCollection inlines,
        HashSet<string> tableNames,
        string fallback)
    {
        foreach (var inline in inlines)
        {
            RestoreElementFont(inline, tableNames, fallback);
            if (inline is Span span)
                RestoreNamedFonts(span.Inlines, tableNames, fallback);
        }
    }

    private static void RestoreElementFont(
        DependencyObject element,
        HashSet<string> tableNames,
        string fallback)
    {
        var current = element switch
        {
            TextElement textElement => textElement.FontFamily?.Source,
            FlowDocument document => document.FontFamily?.Source,
            _ => null
        };
        if (!IsGenericFont(current) || TableRequestsFont(tableNames, current))
            return;

        var family = BundledFonts.Resolve(fallback);
        switch (element)
        {
            case TextElement textElement:
                textElement.FontFamily = family;
                break;
            case FlowDocument document:
                document.FontFamily = family;
                break;
        }
    }

    private static bool IsGenericFont(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;
        return GenericFontFamilies.Contains(PrimaryFontName(name));
    }

    private static bool TableRequestsFont(HashSet<string> tableNames, string? name) =>
        !string.IsNullOrWhiteSpace(name) && tableNames.Contains(PrimaryFontName(name));

    private static string PrimaryFontName(string name)
    {
        var comma = name.IndexOf(',');
        return (comma < 0 ? name : name[..comma]).Trim();
    }

    /// <summary>
    /// Flatten nested / Cocoa-style tables into a readable top-to-bottom flow.
    /// Simple 2-column leaf rows (e.g. product | price) are preserved as tables.
    /// </summary>
    private static void FlattenComplexTables(FlowDocument document)
    {
        if (!ContainsNestedTable(document.Blocks))
            return;

        var flattened = new List<Block>();
        FlattenBlocks(document.Blocks.ToList(), flattened);

        document.Blocks.Clear();
        foreach (var block in flattened)
            document.Blocks.Add(block);
    }

    private static bool ContainsNestedTable(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Table table:
                    foreach (var rowGroup in table.RowGroups)
                    foreach (var row in rowGroup.Rows)
                    foreach (var cell in row.Cells)
                    {
                        if (cell.Blocks.OfType<Table>().Any())
                            return true;
                        if (ContainsNestedTable(cell.Blocks))
                            return true;
                    }
                    break;
                case Section section:
                    if (ContainsNestedTable(section.Blocks))
                        return true;
                    break;
                case List list:
                    foreach (ListItem item in list.ListItems)
                    {
                        if (ContainsNestedTable(item.Blocks))
                            return true;
                    }
                    break;
            }
        }

        return false;
    }

    private static void FlattenBlocks(IReadOnlyList<Block> blocks, List<Block> output)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Table table:
                    FlattenTable(table, output);
                    break;
                case Section section:
                    FlattenBlocks(section.Blocks.ToList(), output);
                    break;
                case List list:
                    foreach (ListItem item in list.ListItems.ToList())
                        FlattenBlocks(item.Blocks.ToList(), output);
                    break;
                default:
                    DetachAndAdd(block, output);
                    break;
            }
        }
    }

    private static void FlattenTable(Table table, List<Block> output)
    {
        foreach (var rowGroup in table.RowGroups.ToList())
        {
            foreach (var row in rowGroup.Rows.ToList())
            {
                var cells = row.Cells.ToList();
                if (cells.Count == 0)
                    continue;

                var anyNested = cells.Any(cell =>
                    cell.Blocks.OfType<Table>().Any() || ContainsNestedTable(cell.Blocks));

                if (!anyNested && cells.Count == 2 && cells.All(IsSimpleContentCell))
                {
                    // Keep product|price style rows as a simple two-column table.
                    var simple = BuildSimpleTwoColumnRow(cells);
                    if (simple is not null)
                        output.Add(simple);
                    else
                        FlattenCellsVertically(cells, output);
                }
                else
                {
                    FlattenCellsVertically(cells, output);
                }
            }
        }
    }

    private static void FlattenCellsVertically(IReadOnlyList<TableCell> cells, List<Block> output)
    {
        foreach (var cell in cells)
        {
            var cellBlocks = cell.Blocks.ToList();
            // Cocoa RTF inserts tiny spacer cells that become empty paragraphs.
            if (cellBlocks.Count == 0 || cellBlocks.All(IsEffectivelyEmptyBlock))
                continue;

            FlattenBlocks(cellBlocks, output);
        }
    }

    private static bool IsSimpleContentCell(TableCell cell)
    {
        if (cell.Blocks.Count == 0)
            return false;

        foreach (var block in cell.Blocks)
        {
            if (block is Table or Section or List)
                return false;
            if (block is not Paragraph)
                return false;
        }

        return true;
    }

    private static bool IsEffectivelyEmptyBlock(Block block)
    {
        if (block is not Paragraph paragraph)
            return false;

        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        return string.IsNullOrWhiteSpace(text);
    }

    private static Table? BuildSimpleTwoColumnRow(IReadOnlyList<TableCell> cells)
    {
        if (cells.Count != 2)
            return null;

        var leftText = GetBlockCollectionPlainText(cells[0].Blocks).Trim();
        var rightText = GetBlockCollectionPlainText(cells[1].Blocks).Trim();
        // Skip empty spacer columns produced by Cocoa RTF.
        if (string.IsNullOrWhiteSpace(leftText) || string.IsNullOrWhiteSpace(rightText))
            return null;

        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(MoveCellContent(cells[0]));
        row.Cells.Add(MoveCellContent(cells[1]));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell MoveCellContent(TableCell source)
    {
        var cell = new TableCell
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2, 8, 2)
        };
        foreach (var block in source.Blocks.ToList())
        {
            source.Blocks.Remove(block);
            cell.Blocks.Add(block);
        }

        return cell;
    }

    private static void DetachAndAdd(Block block, List<Block> output)
    {
        DetachBlock(block);
        output.Add(block);
    }

    private static void DetachBlock(Block block)
    {
        switch (block.Parent)
        {
            case FlowDocument document:
                document.Blocks.Remove(block);
                break;
            case Section section:
                section.Blocks.Remove(block);
                break;
            case TableCell cell:
                cell.Blocks.Remove(block);
                break;
            case ListItem listItem:
                listItem.Blocks.Remove(block);
                break;
        }
    }

    private static string GetBlockCollectionPlainText(IEnumerable<Block> blocks)
    {
        var list = blocks as IList<Block> ?? blocks.ToList();
        if (list.Count == 0)
            return string.Empty;

        // Prefer BlockCollection ContentStart/End when available.
        if (blocks is BlockCollection collection && collection.Count > 0)
        {
            var start = collection.FirstBlock?.ContentStart;
            var end = collection.LastBlock?.ContentEnd;
            if (start is not null && end is not null)
                return new TextRange(start, end).Text;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var block in list)
        {
            if (block is Paragraph paragraph)
                sb.Append(new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
        }

        return sb.ToString();
    }

    public static void ApplyTheme(RichTextBox editor, EditorTheme theme)
    {
        // Present RTF on a conventional paper surface. Do not restyle the
        // document for the application theme; only lift ink that cannot be
        // read on that surface (e.g. Cocoa #F0F2EB on white).
        editor.Background = Brushes.White;
        editor.Foreground = Brushes.Black;

        var selectionBrush = new SolidColorBrush(theme.Selection);
        selectionBrush.Freeze();
        var selectionForeground = new SolidColorBrush(theme.Text);
        selectionForeground.Freeze();
        editor.SelectionBrush = selectionBrush;
        editor.SelectionTextBrush = selectionForeground;
        editor.SelectionOpacity = 1.0;

        EnsureReadableForegrounds(editor.Document);
    }

    private static void EnsureReadableForegrounds(FlowDocument document) =>
        EnsureReadableForegrounds(document.Blocks, Luminance(Colors.White));

    private static void EnsureReadableForegrounds(IEnumerable<Block> blocks, double surfaceLuminance)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    LiftIfUnreadable(paragraph, surfaceLuminance);
                    EnsureReadableInlines(paragraph.Inlines, surfaceLuminance);
                    break;
                case Section section:
                    EnsureReadableForegrounds(section.Blocks, surfaceLuminance);
                    break;
                case List list:
                    foreach (ListItem item in list.ListItems)
                        EnsureReadableForegrounds(item.Blocks, surfaceLuminance);
                    break;
                case Table table:
                    foreach (var rowGroup in table.RowGroups)
                    foreach (var row in rowGroup.Rows)
                    foreach (var cell in row.Cells)
                    {
                        var cellColor = GetBrushColor(cell.Background, Colors.Transparent);
                        var luminance = cellColor.A > 51 ? Luminance(cellColor) : surfaceLuminance;
                        EnsureReadableForegrounds(cell.Blocks, luminance);
                    }
                    break;
            }
        }
    }

    private static void EnsureReadableInlines(InlineCollection inlines, double surfaceLuminance)
    {
        foreach (var inline in inlines)
        {
            LiftIfUnreadable(inline, surfaceLuminance);
            if (inline is Span span)
                EnsureReadableInlines(span.Inlines, surfaceLuminance);
        }
    }

    private static void LiftIfUnreadable(TextElement element, double surfaceLuminance)
    {
        if (element.ReadLocalValue(TextElement.ForegroundProperty) == DependencyProperty.UnsetValue)
            return;

        var foreground = GetBrushColor(element.Foreground, Colors.Transparent);
        if (foreground.A <= 16)
            return;
        if (Saturation(foreground) > 0.18)
            return;
        if (ContrastRatio(Luminance(foreground), surfaceLuminance) >= 3.0)
            return;

        element.Foreground = Brushes.Black;
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
        if (ShouldNormalizeForeground(foreground, 1.0, exportTheme))
            element.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(exportTheme.Text));

        var background = GetBrushColor(element.GetValue(TextElement.BackgroundProperty), Colors.Transparent);
        if (ShouldNormalizeBackgroundForExport(background))
            element.SetValue(TextElement.BackgroundProperty, new SolidColorBrush(exportTheme.Selection));
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

    private readonly struct RemapContext
    {
        public double BackgroundLuminance { get; init; }
        public bool InsideTable { get; init; }

        public static RemapContext ForTheme(EditorTheme theme) => new()
        {
            BackgroundLuminance = Luminance(theme.Background),
            InsideTable = false
        };
    }

    private static void RemapDocumentColors(FlowDocument document, EditorTheme theme)
    {
        RemapBlocks(document.Blocks, theme, RemapContext.ForTheme(theme));
    }

    private static void RemapBlocks(BlockCollection blocks, EditorTheme theme, RemapContext context)
    {
        foreach (var block in blocks)
            RemapBlock(block, theme, context);
    }

    private static void RemapBlock(Block block, EditorTheme theme, RemapContext context)
    {
        switch (block)
        {
            case Paragraph paragraph:
                RemapTextElement(paragraph, theme, context);
                RemapInlines(paragraph.Inlines, theme, context);
                break;
            case Section section:
                RemapBlocks(section.Blocks, theme, context);
                break;
            case List list:
                foreach (ListItem item in list.ListItems)
                    RemapBlocks(item.Blocks, theme, context);
                break;
            case Table table:
                foreach (var rowGroup in table.RowGroups)
                {
                    foreach (var row in rowGroup.Rows)
                    {
                        foreach (var cell in row.Cells)
                        {
                            RemapTableCellBackground(cell, theme);
                            var cellBackground = GetBrushColor(cell.Background, Colors.Transparent);
                            // Transparent cells sit on the document/theme background — do not
                            // assume a white page (that left near-black Cocoa text unreadable).
                            var cellContext = new RemapContext
                            {
                                InsideTable = true,
                                BackgroundLuminance = cellBackground.A > 51
                                    ? Luminance(cellBackground)
                                    : context.BackgroundLuminance
                            };
                            RemapBlocks(cell.Blocks, theme, cellContext);
                        }
                    }
                }
                break;
        }
    }

    private static void RemapTableCellBackground(TableCell cell, EditorTheme theme)
    {
        var background = GetBrushColor(cell.Background, Colors.Transparent);
        if (background.A <= 0)
            return;

        if (ShouldRemapHighlight(background, theme))
            cell.Background = new SolidColorBrush(theme.Selection);
        else if (ShouldRemapDocumentBackground(background, theme))
            cell.Background = new SolidColorBrush(theme.TabSelectedBackground);
    }

    private static void RemapInlines(InlineCollection inlines, EditorTheme theme, RemapContext context, bool insideLink = false)
    {
        foreach (var inline in inlines)
            RemapInline(inline, theme, context, insideLink);
    }

    private static void RemapTextElement(TextElement element, EditorTheme theme, RemapContext context)
    {
        var background = GetBrushColor(element.GetValue(TextElement.BackgroundProperty), Colors.Transparent);
        var backgroundLuminance = EffectiveBackgroundLuminance(background, context);
        var foreground = GetBrushColor(element.GetValue(TextElement.ForegroundProperty), Colors.Black);

        if (ShouldNormalizeForeground(foreground, backgroundLuminance, theme))
            element.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(PreferredTextColor(backgroundLuminance, theme)));

        if (background.A > 0 && ShouldRemapHighlight(background, theme))
            element.SetValue(TextElement.BackgroundProperty, new SolidColorBrush(theme.Selection));
        else if (background.A > 0 && ShouldRemapDocumentBackground(background, theme))
            element.SetValue(TextElement.BackgroundProperty, new SolidColorBrush(theme.TabSelectedBackground));
    }

    private static void RemapInline(Inline inline, EditorTheme theme, RemapContext context, bool insideLink)
    {
        var inLink = insideLink || inline is Hyperlink;
        var background = GetBrushColor(inline.GetValue(TextElement.BackgroundProperty), Colors.Transparent);
        var backgroundLuminance = EffectiveBackgroundLuminance(background, context);

        if (inline is Hyperlink)
        {
            inline.Foreground = new SolidColorBrush(theme.UiAccent);
        }
        else if (inLink)
        {
            // Nested RTF spans inside hyperlinks keep original link blues that override
            // the hyperlink brush — clear them so the theme accent shows through.
            inline.ClearValue(TextElement.ForegroundProperty);
        }
        else
        {
            var foreground = GetBrushColor(inline.GetValue(TextElement.ForegroundProperty), Colors.Black);
            if (ShouldNormalizeForeground(foreground, backgroundLuminance, theme))
                inline.Foreground = new SolidColorBrush(PreferredTextColor(backgroundLuminance, theme));
            else if (theme.IsDark && ShouldBrightenAccent(foreground, backgroundLuminance))
                inline.Foreground = new SolidColorBrush(BrightenForDarkTheme(foreground));
        }

        if (background.A > 0 && ShouldRemapHighlight(background, theme))
            inline.Background = new SolidColorBrush(theme.Selection);
        else if (background.A > 0 && ShouldRemapDocumentBackground(background, theme))
            inline.Background = new SolidColorBrush(theme.TabSelectedBackground);

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
                RemapInline(child, theme, context, inLink);
        }
    }

    /// <summary>
    /// Saturated accents (price reds, etc.) that are fine on white paper become muddy
    /// on dark themes — brighten them while keeping hue.
    /// </summary>
    private static bool ShouldBrightenAccent(Color color, double backgroundLuminance)
    {
        if (color.A <= 16 || backgroundLuminance >= 0.5)
            return false;
        if (Saturation(color) <= 0.18)
            return false;
        return ContrastRatio(Luminance(color), backgroundLuminance) < 3.0;
    }

    private static Color BrightenForDarkTheme(Color color)
    {
        // Mix toward white until contrast against a typical dark surface is comfortable.
        const double targetBg = 0.15;
        var result = color;
        for (var i = 0; i < 8; i++)
        {
            if (ContrastRatio(Luminance(result), targetBg) >= 3.5)
                break;
            result = Color.FromArgb(
                result.A,
                (byte)Math.Min(255, result.R + (255 - result.R) * 0.28),
                (byte)Math.Min(255, result.G + (255 - result.G) * 0.28),
                (byte)Math.Min(255, result.B + (255 - result.B) * 0.28));
        }

        return result;
    }

    private static double EffectiveBackgroundLuminance(Color inlineBackground, RemapContext context)
    {
        if (inlineBackground.A > 51)
            return Luminance(inlineBackground);

        // Inherit the surrounding surface (theme page or parent cell). Nested Cocoa
        // tables often leave cells transparent; assuming white hid dark body text.
        return context.BackgroundLuminance;
    }

    private static Color PreferredTextColor(double backgroundLuminance, EditorTheme theme) =>
        backgroundLuminance > 0.6
            ? EditorTheme.For(EditorThemeKind.Light).Text
            : theme.Text;

    private static Color GetBrushColor(object? value, Color fallback)
    {
        if (value is SolidColorBrush brush)
            return brush.Color;
        return fallback;
    }

    private static double Luminance(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double Saturation(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var maxChannel = Math.Max(r, Math.Max(g, b));
        var minChannel = Math.Min(r, Math.Min(g, b));
        return maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel;
    }

    private static double ContrastRatio(double foreground, double background)
    {
        var lighter = Math.Max(foreground, background) + 0.05;
        var darker = Math.Min(foreground, background) + 0.05;
        return lighter / darker;
    }

    private static bool ShouldNormalizeForeground(Color color, double backgroundLuminance, EditorTheme theme)
    {
        if (color.A <= 16)
            return true;

        var luminance = Luminance(color);
        var saturation = Saturation(color);

        // Near-black / near-white neutrals from Cocoa RTF often have a slight channel
        // bias (e.g. #0D0D12). Treat those as text colors, not intentional accents.
        var isNearBlack = luminance < 0.18;
        var isNearWhite = luminance > 0.85;
        var isNeutral = saturation <= 0.18
                        || (isNearBlack && saturation < 0.5)
                        || (isNearWhite && saturation < 0.5);

        if (!isNeutral)
            return false;

        return ContrastRatio(luminance, backgroundLuminance) < 3.0;
    }

    private static bool ShouldRemapHighlight(Color color, EditorTheme theme)
    {
        var alpha = color.A / 255.0;
        if (alpha <= 0.2)
            return false;

        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var maxChannel = Math.Max(r, Math.Max(g, b));
        var minChannel = Math.Min(r, Math.Min(g, b));
        var saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel;
        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

        if (saturation < 0.12)
            return false;

        if (theme.IsDark)
            return luminance < 0.22;
        return luminance > 0.92;
    }

    private static bool ShouldRemapDocumentBackground(Color color, EditorTheme theme)
    {
        if (!theme.IsDark || color.A <= 51)
            return false;

        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var maxChannel = Math.Max(r, Math.Max(g, b));
        var minChannel = Math.Min(r, Math.Min(g, b));
        var saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel;
        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return saturation < 0.12 && luminance > 0.55;
    }
}
