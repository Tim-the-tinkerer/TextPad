using System.IO;
using System.Text;
using TextPad.Services;

namespace TextPad.Models;

public enum LineEndingKind
{
    Lf,
    CrLf,
    Cr,
    Mixed,
    Unknown
}

public sealed class EditorDocument
{
    public Guid DocumentId { get; } = Guid.NewGuid();
    public string? FilePath { get; set; }
    public DocumentFormat Format { get; set; } = DocumentFormat.PlainText;
    public Encoding Encoding { get; set; } = new UTF8Encoding(false, true);
    public LineEndingKind LineEnding { get; set; } = LineEndingKind.Lf;
    public bool IsDirty { get; set; }
    public byte[]? RtfData { get; set; }
    public LineEndingPolicy LineEndingPolicy { get; set; } = LineEndingPolicy.Preserve;
    public SyntaxLanguage SyntaxLanguage { get; set; } = SyntaxLanguage.Auto;
    public bool ForceWordWrap { get; set; }
    public bool IsFileMissingFromDisk { get; private set; }
    public DateTime? LastKnownDiskWriteTimeUtc { get; private set; }

    public void MarkFileMissingFromDisk() => IsFileMissingFromDisk = true;

    public bool IsRichText => Format == DocumentFormat.RichText;

    public bool HasChangedOnDisk()
    {
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath) || LastKnownDiskWriteTimeUtc is null)
            return false;

        return File.GetLastWriteTimeUtc(FilePath) > LastKnownDiskWriteTimeUtc;
    }

    public bool WasDeletedFromDisk() =>
        !string.IsNullOrEmpty(FilePath) &&
        LastKnownDiskWriteTimeUtc is not null &&
        !File.Exists(FilePath);

    public void NoteSavedToDisk()
    {
        if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
        {
            LastKnownDiskWriteTimeUtc = File.GetLastWriteTimeUtc(FilePath);
            IsFileMissingFromDisk = false;
        }
    }

    public string DisplayName =>
        string.IsNullOrEmpty(FilePath) ? "Untitled" : Path.GetFileName(FilePath);

    public string TabTitle
    {
        get
        {
            var title = DisplayName;
            if (IsFileMissingFromDisk)
                title += " (missing)";
            if (IsDirty)
                title += " •";
            return title;
        }
    }

    public string SuggestedSaveFileName
    {
        get
        {
            var ext = DocumentFormatSupport.DefaultExtension(Format);
            if (!string.IsNullOrEmpty(FilePath))
                return Path.GetFileNameWithoutExtension(FilePath) + "." + ext;
            return "Untitled." + ext;
        }
    }

    public const int MaxLoadBytes = 256 * 1024 * 1024;

    public static EditorDocument LoadFromFile(string path, Encoding? explicitEncoding = null)
    {
        var bytes = SafeFileReader.ReadAllBytes(path);
        if (bytes.Length > MaxLoadBytes)
            throw new InvalidDataException($"File is too large to open ({bytes.Length / (1024 * 1024)} MB). Maximum is {MaxLoadBytes / (1024 * 1024)} MB.");
        var doc = new EditorDocument { FilePath = path };
        doc.Format = DocumentFormatSupport.Detect(path);

        if (doc.IsRichText)
        {
            if (!LooksLikeRtf(bytes))
                throw new InvalidDataException("Unable to read RTF file.");

            doc.RtfData = bytes;
            doc.Encoding = Encoding.UTF8;
            doc.LineEnding = LineEndingKind.Lf;
            doc.IsDirty = false;
            doc.NoteSavedToDisk();
            return doc;
        }

        var encoding = explicitEncoding ?? DocumentEncoding.Detect(bytes);
        var preamble = encoding.GetPreamble();
        var offset = StartsWith(bytes, preamble) ? preamble.Length : 0;
        string text;
        try
        {
            text = encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException($"The file contains bytes that are invalid for {DocumentEncoding.NameFor(encoding)}.", ex);
        }

        doc.Encoding = encoding;
        doc.LineEnding = DetectLineEndings(text);
        doc.IsDirty = false;
        doc.PlainContent = text;
        doc.LineEndingPolicy = EditorPreferences.Instance.LineEndingPolicy;
        doc.SyntaxLanguage = SyntaxLanguage.Auto;
        doc.NoteSavedToDisk();
        return doc;
    }

    public void ReloadFromDisk()
    {
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
            throw new FileNotFoundException("The document has no saved file on disk.");

        var reloaded = LoadFromFile(FilePath, Encoding);
        Format = reloaded.Format;
        Encoding = reloaded.Encoding;
        LineEnding = reloaded.LineEnding;
        LineEndingPolicy = reloaded.LineEndingPolicy;
        SyntaxLanguage = reloaded.SyntaxLanguage;
        PlainContent = reloaded.PlainContent;
        RtfData = reloaded.RtfData;
        IsDirty = false;
    }

    public string? PlainContent { get; set; }

    private const int LineEndingSampleSize = 256 * 1024;

    public static LineEndingKind DetectLineEndings(string text)
    {
        if (string.IsNullOrEmpty(text))
            return LineEndingKind.Unknown;

        var sample = text.Length <= LineEndingSampleSize ? text : text[..LineEndingSampleSize];
        var hasLf = sample.Contains('\n');
        var hasCr = sample.Contains('\r');
        if (!hasLf && !hasCr) return LineEndingKind.Unknown;
        if (hasLf && !hasCr) return LineEndingKind.Lf;
        if (hasCr && !hasLf) return LineEndingKind.Cr;
        if (sample.Contains("\r\n", StringComparison.Ordinal))
        {
            var normalized = sample.Replace("\r\n", "");
            return normalized.Contains('\r') || normalized.Contains('\n')
                ? LineEndingKind.Mixed
                : LineEndingKind.CrLf;
        }
        return LineEndingKind.Mixed;
    }

    public byte[] BuildBytesForSave(string text)
    {
        var output = ApplyLineEndingPolicy(text);
        byte[] bytes;
        try
        {
            bytes = Encoding.GetBytes(output);
        }
        catch (EncoderFallbackException ex)
        {
            throw new InvalidDataException($"The document contains characters that cannot be saved as {DocumentEncoding.NameFor(Encoding)}. Choose a Unicode encoding and try again.", ex);
        }
        var preamble = Encoding.GetPreamble();
        return preamble.Length > 0 ? preamble.Concat(bytes).ToArray() : bytes;
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        if (prefix.Length == 0 || bytes.Length < prefix.Length)
            return false;
        for (var i = 0; i < prefix.Length; i++)
            if (bytes[i] != prefix[i]) return false;
        return true;
    }

    public void SavePlainText(string text, string? targetPath = null)
    {
        var path = targetPath ?? FilePath;
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("No file path set.");

        AtomicFileWriter.WriteAllBytes(path, BuildBytesForSave(text));

        if (targetPath is null || string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            PlainContent = text;
            IsDirty = false;
        }
    }

    public void SaveRtf(byte[] rtfData, string? targetPath = null)
    {
        var path = targetPath ?? FilePath;
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("No file path set.");

        AtomicFileWriter.WriteAllBytes(path, rtfData);

        if (targetPath is null || string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            RtfData = rtfData;
            IsDirty = false;
        }
    }

    private string ApplyLineEndingPolicy(string text) =>
        LineEndingPolicy switch
        {
            LineEndingPolicy.Lf => text.Replace("\r\n", "\n").Replace("\r", "\n"),
            LineEndingPolicy.CrLf => text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n"),
            _ => text
        };

    private static bool LooksLikeRtf(byte[] data)
    {
        if (data.Length < 5)
            return false;

        var header = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 10));
        return header.Contains("{\\rtf", StringComparison.OrdinalIgnoreCase);
    }
}
