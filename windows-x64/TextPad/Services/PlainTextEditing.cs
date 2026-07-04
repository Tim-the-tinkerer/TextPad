using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using TextPad.Models;

namespace TextPad.Services;

public static class PlainTextEditing
{
    private static readonly Dictionary<char, char> Pairs = new()
    {
        ['('] = ')',
        ['['] = ']',
        ['{'] = '}',
        ['"'] = '"',
        ['\''] = '\''
    };

    public static bool HandlePreviewKeyDown(TextEditor editor, KeyEventArgs e, LineEndingKind lineEnding = LineEndingKind.Lf)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            InsertBacktab(editor);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        {
            InsertNewlineWithAutoIndent(editor, lineEnding);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public static bool HandleTextInput(TextEditor editor, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || e.Text.Length != 1)
            return false;

        var ch = e.Text[0];
        if (!Pairs.ContainsKey(ch) && !Pairs.ContainsValue(ch))
            return false;

        return HandlePairing(editor, ch);
    }

    public static void InsertTab(TextEditor editor)
    {
        var prefs = EditorPreferences.Instance;
        var area = editor.TextArea;
        var selection = area.Selection;

        if (!selection.IsEmpty)
        {
            var spaces = new string(' ', prefs.TabWidth);
            area.Document.Replace(selection.SurroundingSegment, spaces);
            return;
        }

        var offset = area.Caret.Offset;
        var line = area.Document.GetLineByOffset(offset);
        var column = offset - line.Offset;
        var remainder = column % prefs.TabWidth;
        var spacesToNextStop = remainder == 0 ? prefs.TabWidth : prefs.TabWidth - remainder;
        area.Document.Insert(offset, new string(' ', spacesToNextStop));
    }

    private static void InsertBacktab(TextEditor editor)
    {
        var prefs = EditorPreferences.Instance;
        var area = editor.TextArea;
        var doc = area.Document;
        var selection = area.Selection;

        if (!selection.IsEmpty)
        {
            DedentSelection(editor, prefs.TabWidth);
            return;
        }

        var offset = area.Caret.Offset;
        if (offset <= 0)
            return;

        var line = doc.GetLineByOffset(offset);
        var column = offset - line.Offset;
        if (column <= 0)
            return;

        var removeCount = column % prefs.TabWidth;
        if (removeCount == 0)
            removeCount = prefs.TabWidth;
        removeCount = Math.Min(removeCount, column);

        var slice = doc.GetText(offset - removeCount, removeCount);
        if (slice.All(c => c == ' '))
            doc.Remove(offset - removeCount, removeCount);
    }

    private static void DedentSelection(TextEditor editor, int tabWidth)
    {
        var area = editor.TextArea;
        var doc = area.Document;
        var start = area.Selection.SurroundingSegment.Offset;
        var end = start + area.Selection.SurroundingSegment.Length;
        var startLine = doc.GetLineByOffset(start);
        var endLine = doc.GetLineByOffset(end);

        doc.BeginUpdate();
        try
        {
            for (var lineNumber = endLine.LineNumber; lineNumber >= startLine.LineNumber; lineNumber--)
            {
                var line = doc.GetLineByNumber(lineNumber);
                var prefixLength = Math.Min(tabWidth, line.Length);
                if (prefixLength == 0)
                    continue;

                var prefix = doc.GetText(line.Offset, prefixLength);
                var removable = prefix.TakeWhile(c => c == ' ').Count();
                if (removable > 0)
                    doc.Remove(line.Offset, removable);
            }
        }
        finally
        {
            doc.EndUpdate();
        }
    }

    private static void InsertNewlineWithAutoIndent(TextEditor editor, LineEndingKind lineEnding)
    {
        var prefs = EditorPreferences.Instance;
        var area = editor.TextArea;
        var doc = area.Document;
        var offset = area.Caret.Offset;
        var line = doc.GetLineByOffset(offset);
        var lineText = doc.GetText(line);
        var indent = new string(lineText.TakeWhile(c => c is ' ' or '\t').ToArray());

        var trimmed = lineText.Trim();
        var extraIndent = trimmed.EndsWith('{') || trimmed.EndsWith('(') || trimmed.EndsWith('[') || trimmed.EndsWith(':')
            ? new string(' ', prefs.TabWidth)
            : string.Empty;

        var newline = NewlineFor(lineEnding);
        doc.Insert(offset, newline + indent + extraIndent);
        area.Caret.Offset = offset + newline.Length + indent.Length + extraIndent.Length;
    }

    private static string NewlineFor(LineEndingKind lineEnding) => lineEnding switch
    {
        LineEndingKind.CrLf => "\r\n",
        LineEndingKind.Cr => "\r",
        _ => "\n"
    };

    private static bool HandlePairing(TextEditor editor, char ch)
    {
        var area = editor.TextArea;
        var doc = area.Document;
        var offset = area.Caret.Offset;

        if (Pairs.TryGetValue(ch, out var close))
        {
            if (ch == close)
            {
                if (offset < doc.TextLength && doc.GetCharAt(offset) == close)
                {
                    area.Caret.Offset = offset + 1;
                    return true;
                }

                doc.Insert(offset, $"{ch}{close}");
                area.Caret.Offset = offset + 1;
                return true;
            }

            if (area.Selection.IsEmpty)
            {
                doc.Insert(offset, $"{ch}{close}");
                area.Caret.Offset = offset + 1;
                return true;
            }
        }

        if (Pairs.ContainsValue(ch) && area.Selection.IsEmpty && offset < doc.TextLength && doc.GetCharAt(offset) == ch)
        {
            area.Caret.Offset = offset + 1;
            return true;
        }

        return false;
    }
}