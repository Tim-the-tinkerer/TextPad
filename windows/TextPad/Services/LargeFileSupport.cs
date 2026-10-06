using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using System.Windows.Threading;
using TextPad.Models;

namespace TextPad.Services;

public static class LargeFileSupport
{
    public const int LongLineThreshold = 8000;
    public const int LargeDocumentCharacterThreshold = 500_000;

    private const int MaxLineScanCharacters = 250_000;

    public static bool HasExtremelyLongLines(string text)
    {
        var current = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (IsLineBreak(text, i, out var width))
            {
                if (current > LongLineThreshold)
                    return true;
                current = 0;
                i += width - 1;
                continue;
            }

            current++;
            if (current > LongLineThreshold)
                return true;
        }

        return current > LongLineThreshold;
    }

    public static int MaxLineLength(string text)
    {
        if (text.Length > MaxLineScanCharacters)
            return LongLineThreshold + 1;

        var maxLength = 0;
        var current = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (IsLineBreak(text, i, out var width))
            {
                if (current > maxLength)
                    maxLength = current;
                current = 0;
                i += width - 1;
                continue;
            }

            current++;
        }

        return Math.Max(maxLength, current);
    }

    private static bool IsLineBreak(string text, int index, out int width)
    {
        if (text[index] == '\r')
        {
            width = index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
            return true;
        }

        if (text[index] == '\n')
        {
            width = 1;
            return true;
        }

        width = 1;
        return false;
    }

    public static bool EffectiveWordWrap(bool preferred, string text)
    {
        _ = text;
        return preferred;
    }

    public static bool ComputeWordWrap(string text)
    {
        _ = text;
        return EditorPreferences.Instance.WordWrap;
    }

    public static void ConfigureEditorForContent(TextEditor editor, string text)
    {
        var forceWordWrap = false;
        ApplyEditorContentSettings(
            editor,
            ComputeWordWrap(text),
            text.Length,
            forceWordWrap,
            CountLogicalLines(text));
    }

    public static void LoadPlainText(TextEditor editor, string text)
    {
        ConfigureEditorForContent(editor, text);
        editor.Text = text;
    }

    public static void AttachPlainTextPayload(TextEditor editor, PlainTextOpenPayload payload)
    {
        if (payload.TextDocument is null)
            return;

        ApplyEditorContentSettings(
            editor, payload.WordWrap, payload.CharacterCount, payload.ForceWordWrap, payload.LogicalLineCount);
        payload.TextDocument.SetOwnerThread(Thread.CurrentThread);
        editor.Document = payload.TextDocument;
        editor.CaretOffset = 0;
        payload.TextDocument.UndoStack.ClearAll();
        payload.TextDocument.UndoStack.MarkAsOriginalFile();
    }

    public static async Task AttachPlainTextPayloadAsync(TextEditor editor, PlainTextOpenPayload payload)
    {
        if (payload.TextDocument is null)
            return;

        ApplyEditorContentSettings(
            editor, payload.WordWrap, payload.CharacterCount, payload.ForceWordWrap, payload.LogicalLineCount);

        editor.Visibility = System.Windows.Visibility.Collapsed;
        try
        {
            payload.TextDocument.SetOwnerThread(Thread.CurrentThread);
            editor.Document = payload.TextDocument;
            editor.CaretOffset = 0;
            payload.TextDocument.UndoStack.ClearAll();
            payload.TextDocument.UndoStack.MarkAsOriginalFile();
        }
        finally
        {
            editor.Visibility = System.Windows.Visibility.Visible;
        }

        await editor.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
    }

    public static int CountLogicalLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (!IsLineBreak(text, i, out var width))
                continue;
            count++;
            i += width - 1;
        }

        return count;
    }

    public static bool ShouldShowLineNumbers(int characterCount, int logicalLineCount) =>
        EditorPreferences.Instance.ShowLineNumbers &&
        (characterCount <= LargeDocumentCharacterThreshold || logicalLineCount <= 50_000);

    public static void ApplyEditorContentSettings(
        TextEditor editor,
        bool wordWrap,
        int characterCount,
        bool forceWordWrap,
        int logicalLineCount = 1)
    {
        editor.SyntaxHighlighting = null;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.ShowLineNumbers = ShouldShowLineNumbers(characterCount, logicalLineCount);
        _ = forceWordWrap;

        editor.WordWrap = wordWrap;
    }
}
