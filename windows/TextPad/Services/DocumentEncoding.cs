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
            (new UTF8Encoding(false, true), "UTF-8"),
            (new UTF8Encoding(true, true), "UTF-8 with BOM"),
            (new UnicodeEncoding(false, true, true), "UTF-16 LE"),
            (new UnicodeEncoding(true, true, true), "UTF-16 BE"),
            (Encoding.GetEncoding(20127, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), "ASCII"),
            (Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), "ISO Latin-1"),
            (Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), "Windows Latin-1"),
            (Encoding.GetEncoding(10000, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), "Mac Roman")
        ];
    }

    public static Encoding EncodingFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new UTF8Encoding(false, true);

        foreach (var item in Supported)
        {
            if (item.name == name)
                return item.encoding;
        }

        return new UTF8Encoding(false, true);
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
            return new UTF8Encoding(true, true);

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                return new UnicodeEncoding(false, true, true);
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return new UnicodeEncoding(true, true, true);
        }

        if (TryDetectBomlessUtf16(bytes, out var utf16))
            return utf16;

        if (IsValidUtf8(bytes))
            return new UTF8Encoding(false, true);

        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    private static bool IsValidUtf8(byte[] bytes)
    {
        if (bytes.Length == 0)
            return true;

        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDetectBomlessUtf16(byte[] bytes, out Encoding encoding)
    {
        encoding = null!;
        if (bytes.Length < 4 || bytes.Length % 2 != 0)
            return false;

        var pairs = Math.Min(bytes.Length / 2, 32 * 1024);
        var zeroEven = 0;
        var zeroOdd = 0;
        for (var i = 0; i < pairs; i++)
        {
            var even = bytes[i * 2];
            var odd = bytes[i * 2 + 1];
            if (even == 0 && odd != 0) zeroEven++;
            if (odd == 0 && even != 0) zeroOdd++;
        }

        // Require a strong NUL-position signal. Arbitrary binary data should
        // continue to the legacy-encoding fallback rather than becoming UTF-16.
        if (zeroOdd > Math.Max(2, zeroEven * 3) && zeroOdd > pairs / 8)
        {
            encoding = new UnicodeEncoding(false, false, true);
            return CanDecode(bytes, encoding);
        }
        if (zeroEven > Math.Max(2, zeroOdd * 3) && zeroEven > pairs / 8)
        {
            encoding = new UnicodeEncoding(true, false, true);
            return CanDecode(bytes, encoding);
        }
        return false;
    }

    private static bool CanDecode(byte[] bytes, Encoding encoding)
    {
        try
        {
            _ = encoding.GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
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
