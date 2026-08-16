using System.IO;
using ICSharpCode.AvalonEdit.Document;
using TextPad.Models;

namespace TextPad.Services;

public sealed class PlainTextOpenPayload
{
    public const int SimpleEditorThreshold = 500_000;
    public const int SimpleEditorMaxCharacters = 5_000_000;

    public required EditorDocument Document { get; init; }
    public required bool WordWrap { get; init; }
    public required bool ForceWordWrap { get; init; }
    public required bool UseSimpleEditor { get; init; }
    public int CharacterCount { get; init; }
    public int LogicalLineCount { get; init; }
    public TextDocument? TextDocument { get; init; }
    public string? SimpleEditorText { get; init; }

    public static PlainTextOpenPayload FromDocument(EditorDocument document)
    {
        var text = document.PlainContent ?? string.Empty;
        document.PlainContent = null;
        var forceWordWrap = LargeFileSupport.HasExtremelyLongLines(text);
        document.ForceWordWrap = forceWordWrap;
        var logicalLineCount = LargeFileSupport.CountLogicalLines(text);
        // AvalonEdit virtualizes line rendering and remains the canonical editor.
        // Switching large one-line documents to WPF TextBox duplicated the editor
        // stack and made behavior and performance less predictable.
        var useSimpleEditor = false;

        if (useSimpleEditor)
        {
            if (text.Length > SimpleEditorMaxCharacters)
            {
                throw new InvalidDataException(
                    $"File has an extremely long line ({text.Length:N0} characters) that exceeds the editor limit of {SimpleEditorMaxCharacters:N0} characters.");
            }

            return new PlainTextOpenPayload
            {
                Document = document,
                SimpleEditorText = text,
                UseSimpleEditor = true,
                WordWrap = true,
                ForceWordWrap = true,
                CharacterCount = text.Length,
                LogicalLineCount = logicalLineCount
            };
        }

        var textDocument = new TextDocument(text);
        textDocument.SetOwnerThread(null!);

        return new PlainTextOpenPayload
        {
            Document = document,
            TextDocument = textDocument,
            UseSimpleEditor = false,
            WordWrap = LargeFileSupport.ComputeWordWrap(text),
            ForceWordWrap = forceWordWrap,
            CharacterCount = text.Length,
            LogicalLineCount = logicalLineCount
        };
    }
}
