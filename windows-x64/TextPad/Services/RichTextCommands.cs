using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TextPad.Services;

public static class RichTextCommands
{
    public static void ToggleBold(RichTextBox editor) => ToggleWeight(editor, FontWeights.Bold);
    public static void ToggleItalic(RichTextBox editor) => ToggleStyle(editor, FontStyles.Italic);
    public static void ToggleUnderline(RichTextBox editor) => ToggleDecoration(editor, TextDecorations.Underline);
    public static void ToggleStrikethrough(RichTextBox editor) => ToggleDecoration(editor, TextDecorations.Strikethrough);

    public static void SetAlignment(RichTextBox editor, TextAlignment alignment)
    {
        if (editor.Selection.IsEmpty)
            return;

        var start = editor.Selection.Start;
        var end = editor.Selection.End;
        var pointer = start;
        while (pointer != null && pointer.CompareTo(end) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.ElementStart &&
                pointer.Parent is Paragraph paragraph)
                paragraph.TextAlignment = alignment;

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
    }

    public static void ApplyForeground(RichTextBox editor, Color color)
    {
        if (!editor.Selection.IsEmpty)
            editor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
    }

    public static void ApplyBackground(RichTextBox editor, Color color)
    {
        if (!editor.Selection.IsEmpty)
            editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(color));
    }

    public static void IncreaseIndent(RichTextBox editor) =>
        editor.Selection.Text = "    " + editor.Selection.Text;

    public static void DecreaseIndent(RichTextBox editor)
    {
        if (editor.Selection.IsEmpty)
            return;

        var text = editor.Selection.Text;
        if (text.StartsWith("    "))
            editor.Selection.Text = text[4..];
        else if (text.StartsWith('\t'))
            editor.Selection.Text = text[1..];
    }

    public static void PasteAndMatchStyle(RichTextBox editor)
    {
        if (!Clipboard.ContainsText())
            return;

        var text = Clipboard.GetText();
        editor.Selection.Text = text;
    }

    private static void ToggleWeight(RichTextBox editor, FontWeight target)
    {
        if (editor.Selection.IsEmpty)
            return;

        var current = editor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
        var next = current is FontWeight weight && weight == target ? FontWeights.Normal : target;
        editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, next);
    }

    private static void ToggleStyle(RichTextBox editor, FontStyle target)
    {
        if (editor.Selection.IsEmpty)
            return;

        var current = editor.Selection.GetPropertyValue(TextElement.FontStyleProperty);
        var next = current is FontStyle style && style == target ? FontStyles.Normal : target;
        editor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, next);
    }

    private static void ToggleDecoration(RichTextBox editor, TextDecorationCollection target)
    {
        if (editor.Selection.IsEmpty)
            return;

        var current = editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        var next = current == target ? null : target;
        editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, next);
    }
}