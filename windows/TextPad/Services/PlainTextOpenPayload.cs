using ICSharpCode.AvalonEdit.Document;
using TextPad.Models;

namespace TextPad.Services;

public sealed class PlainTextOpenPayload
{
    public required EditorDocument Document { get; init; }
    public required bool WordWrap { get; init; }
    public required bool ForceWordWrap { get; init; }
    public int CharacterCount { get; init; }
    public int LogicalLineCount { get; init; }
    public TextDocument? TextDocument { get; init; }

    public static PlainTextOpenPayload FromDocument(EditorDocument document)
    {
        var text = document.PlainContent ?? string.Empty;
        document.PlainContent = null;
        var forceWordWrap = LargeFileSupport.HasExtremelyLongLines(text);
        document.ForceWordWrap = forceWordWrap;
        var textDocument = new TextDocument(text);
        textDocument.SetOwnerThread(null!);

        return new PlainTextOpenPayload
        {
            Document = document,
            TextDocument = textDocument,
            WordWrap = LargeFileSupport.ComputeWordWrap(text),
            ForceWordWrap = forceWordWrap,
            CharacterCount = text.Length,
            LogicalLineCount = LargeFileSupport.CountLogicalLines(text)
        };
    }
}
