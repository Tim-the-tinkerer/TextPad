using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace TextPad.Services;

public static class BundledFonts
{
    public static readonly string[] FamilyNames = ["Interlac", "Interlac Unicode"];

    private const uint FrPrivate = 0x10;
    private static readonly Dictionary<string, FontFamily> Families = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> FilesByFamily = new(StringComparer.OrdinalIgnoreCase);

    public static string? DirectoryPath { get; private set; }

    public static IReadOnlyList<string> PreferenceFontNames { get; } =
    [
        "Consolas",
        "Cascadia Mono",
        "Courier New",
        "Lucida Console",
        ..FamilyNames
    ];

    public static void Register()
    {
        DirectoryPath = ResolveFontDirectory();
        if (DirectoryPath is null)
            return;

        var baseUri = new Uri(DirectoryPath + Path.DirectorySeparatorChar);
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.ttf")
                     .Concat(Directory.EnumerateFiles(DirectoryPath, "*.otf")))
        {
            AddFontResourceEx(file, FrPrivate, IntPtr.Zero);
        }

        foreach (var name in FamilyNames)
        {
            var family = new FontFamily(baseUri, $"./#{name}");
            Families[name] = family;
            var match = Directory.EnumerateFiles(DirectoryPath)
                .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path)
                    .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
                    .Equals(name.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                FilesByFamily[name] = match;
        }
    }

    public static FontFamily Resolve(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && Families.TryGetValue(name.Trim(), out var family))
            return family;
        return new FontFamily(string.IsNullOrWhiteSpace(name) ? "Segoe UI" : name);
    }

    public static string? FileForFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return FilesByFamily.TryGetValue(name.Trim(), out var path) ? path : null;
    }

    private static string? ResolveFontDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Fonts"))
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);
}
