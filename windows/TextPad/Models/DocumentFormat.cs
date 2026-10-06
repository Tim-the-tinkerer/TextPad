using System.IO;

namespace TextPad.Models;

public enum DocumentFormat
{
    PlainText,
    RichText
}

public static class DocumentFormatSupport
{
    public static DocumentFormat Detect(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return DocumentFormat.PlainText;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".rtf")
            return DocumentFormat.RichText;
        return DocumentFormat.PlainText;
    }

    public static string DefaultExtension(DocumentFormat format) =>
        format == DocumentFormat.RichText ? "rtf" : "txt";

    public static bool ValidateSavePath(string path, bool isRichText, out string errorMessage)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".rtfd")
        {
            errorMessage = "RTFD packages are not supported. Open or export the document as a standard .rtf file.";
            return false;
        }
        if (isRichText)
        {
            if (ext != ".rtf")
            {
                errorMessage = "Rich text documents must be saved with a .rtf extension.";
                return false;
            }
        }
        else if (ext == ".rtf")
        {
            errorMessage = "Plain text cannot be saved with a rich text (.rtf) extension.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}