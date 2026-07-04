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
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                current = 0;
                continue;
            }

            if (ch != '\r')
            {
                current++;
                if (current > LongLineThreshold)
                    return true;
            }
        }

        return current > LongLineThreshold;
    }

    public static int MaxLineLength(string text)
    {
        if (text.Length > MaxLineScanCharacters)
            return LongLineThreshold + 1;

        var maxLength = 0;
        var current = 0;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                if (current > maxLength)
                    maxLength = current;
                current = 0;
            }
            else if (ch != '\r')
            {
                current++;
            }
        }

        return Math.Max(maxLength, current);
    }

    public static bool EffectiveWordWrap(bool preferred, string text) =>
        preferred && MaxLineLength(text) <= LongLineThreshold;

    public static bool ComputeWordWrap(string text)
    {
        if (!EditorPreferences.Instance.WordWrap)
            return false;

        if (HasExtremelyLongLines(text))
            return true;

        if (text.Length > LargeDocumentCharacterThreshold)
            return false;

        return MaxLineLength(text) <= LongLineThreshold;
    }

    public static void ConfigureEditorForContent(TextEditor editor, string text)
    {
        var forceWordWrap = HasExtremelyLongLines(text);
        ApplyEditorContentSettings(
            editor,
            forceWordWrap || ComputeWordWrap(text),
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
        foreach (var ch in text)
        {
            if (ch == '\n')
                count++;
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

        editor.WordWrap = forceWordWrap || wordWrap;
    }
}