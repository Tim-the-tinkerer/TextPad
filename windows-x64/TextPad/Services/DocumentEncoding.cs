using System.Text;

namespace TextPad.Services;

public static class DocumentEncoding
{
    private static (Encoding encoding, string name)[]? _supported;

    public static (Encoding encoding, string name)[] Supported => _supported ??= CreateSupported();

    private static (Encoding encoding, string name)[] CreateSupported()
    {
        return
        [
            (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), "UTF-8"),
            (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), "UTF-8 with BOM"),
            (Encoding.Unicode, "UTF-16 LE"),
            (Encoding.BigEndianUnicode, "UTF-16 BE"),
            (Encoding.ASCII, "ASCII"),
            (Encoding.Latin1, "ISO Latin-1"),
            (Encoding.GetEncoding(1252), "Windows Latin-1")
        ];
    }

    public static Encoding EncodingFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        foreach (var item in Supported)
        {
            if (item.name == name)
                return item.encoding;
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    public static string NameFor(Encoding encoding)
    {
        foreach (var item in Supported)
        {
            if (EncodingEquals(item.encoding, encoding))
                return item.name;
        }
        return encoding.WebName;
    }

    public static Encoding Detect(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;
        }

        if (IsValidUtf8(bytes))
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        return Encoding.GetEncoding(1252);
    }

    private const int Utf8ValidationSampleSize = 64 * 1024;

    private static bool IsValidUtf8(byte[] bytes)
    {
        if (bytes.Length == 0)
            return true;

        var sampleLength = Math.Min(bytes.Length, Utf8ValidationSampleSize);
        try
        {
            var decoder = Encoding.UTF8.GetDecoder();
            decoder.Fallback = DecoderFallback.ExceptionFallback;
            var chars = new char[Encoding.UTF8.GetMaxCharCount(sampleLength)];
            decoder.GetChars(bytes, 0, sampleLength, chars, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EncodingEquals(Encoding a, Encoding b)
    {
        if (a.CodePage != b.CodePage)
            return false;
        if (a is UTF8Encoding ua && b is UTF8Encoding ub)
            return ua.GetPreamble().Length == ub.GetPreamble().Length;
        return true;
    }
}