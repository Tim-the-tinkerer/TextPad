using System.IO;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using TextPad.Models;

namespace TextPad.Services;

public sealed class CurrentLineHighlighter : IBackgroundRenderer
{
    private readonly TextDocument _document;
    private int _line = -1;
    private Brush _brush;

    public CurrentLineHighlighter(TextDocument document, Brush brush)
    {
        _document = document;
        _brush = brush;
        Layer = KnownLayer.Background;
    }

    public KnownLayer Layer { get; }

    public void SetLine(int line) => _line = line;

    public void SetBrush(Brush brush) => _brush = brush;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_line < 1 || _line > _document.LineCount)
            return;

        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, _document.GetLineByNumber(_line)))
            drawingContext.DrawRectangle(_brush, null, rect);
    }
}

public static class SyntaxHighlighterSetup
{
    private static readonly Dictionary<string, IHighlightingDefinition> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HighlightingBrush?> OriginalForegrounds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HighlightingBrush?> OriginalBackgrounds = new(StringComparer.OrdinalIgnoreCase);

    public static IHighlightingDefinition? ForDocument(EditorDocument document) =>
        ForLanguage(document.SyntaxLanguage == SyntaxLanguage.Auto
            ? DetectFromPath(document.FilePath)
            : document.SyntaxLanguage);

    public static IHighlightingDefinition? ForFile(string? path) =>
        ForLanguage(DetectFromPath(path));

    public static SyntaxLanguage DetectFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return SyntaxLanguage.PlainText;

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => SyntaxLanguage.CSharp,
            ".js" or ".jsx" or ".ts" or ".tsx" or ".mjs" => SyntaxLanguage.JavaScript,
            ".json" => SyntaxLanguage.Json,
            ".xml" or ".xaml" or ".csproj" or ".config" => SyntaxLanguage.Xml,
            ".html" or ".htm" => SyntaxLanguage.Html,
            ".css" or ".scss" => SyntaxLanguage.Css,
            ".py" or ".pyw" => SyntaxLanguage.Python,
            ".java" => SyntaxLanguage.Java,
            ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp" or ".c" or ".m" or ".mm" or ".swift" => SyntaxLanguage.Cpp,
            ".sql" => SyntaxLanguage.Sql,
            ".ps1" => SyntaxLanguage.PowerShell,
            ".md" or ".markdown" => SyntaxLanguage.Markdown,
            ".sh" or ".bash" or ".zsh" => SyntaxLanguage.Shell,
            _ => SyntaxLanguage.PlainText
        };
    }

    public static string DisplayName(SyntaxLanguage language) => language switch
    {
        SyntaxLanguage.CSharp => "C#",
        SyntaxLanguage.JavaScript => "JavaScript",
        SyntaxLanguage.Python => "Python",
        SyntaxLanguage.Html => "HTML",
        SyntaxLanguage.Css => "CSS",
        SyntaxLanguage.Json => "JSON",
        SyntaxLanguage.Markdown => "Markdown",
        SyntaxLanguage.Shell => "Shell",
        SyntaxLanguage.Cpp => "C/C++",
        SyntaxLanguage.Xml => "XML",
        SyntaxLanguage.Java => "Java",
        SyntaxLanguage.Sql => "SQL",
        SyntaxLanguage.PowerShell => "PowerShell",
        _ => "Plain Text"
    };

    private static IHighlightingDefinition? ForLanguage(SyntaxLanguage language) => language switch
    {
        SyntaxLanguage.CSharp => Get("C#"),
        SyntaxLanguage.JavaScript or SyntaxLanguage.Json => Get("JavaScript"),
        SyntaxLanguage.Xml => Get("XML"),
        SyntaxLanguage.Html => Get("HTML"),
        SyntaxLanguage.Css => Get("CSS"),
        SyntaxLanguage.Python => Get("Python"),
        SyntaxLanguage.Java => Get("Java"),
        SyntaxLanguage.Cpp => Get("C++"),
        SyntaxLanguage.Sql => Get("SQL"),
        SyntaxLanguage.PowerShell => Get("PowerShell"),
        SyntaxLanguage.Markdown => Get("MarkDown"),
        SyntaxLanguage.Shell => Get("PowerShell"),
        _ => null
    };

    private static IHighlightingDefinition? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
            return cached;

        try
        {
            var def = HighlightingManager.Instance.GetDefinition(name);
            if (def is not null)
            {
                Cache[name] = def;
                ApplyThemeColors(def, EditorPreferences.Instance.EffectiveTheme);
            }
            return def;
        }
        catch
        {
            return null;
        }
    }

    public static void RefreshAllThemeColors(EditorTheme theme)
    {
        foreach (var definition in Cache.Values.Distinct())
            ApplyThemeColors(definition, theme);
    }

    public static void ApplyThemeColors(IHighlightingDefinition? definition, EditorTheme theme)
    {
        if (definition is null)
            return;

        foreach (var color in definition.NamedHighlightingColors)
        {
            var key = $"{definition.Name}:{color.Name}";
            var bgKey = $"{definition.Name}:{color.Name}:bg";
            OriginalForegrounds.TryAdd(key, color.Foreground);
            OriginalBackgrounds.TryAdd(bgKey, color.Background);

            if (OriginalForegrounds.TryGetValue(key, out var originalForeground))
                color.Foreground = originalForeground;
            if (OriginalBackgrounds.TryGetValue(bgKey, out var originalBackground))
                color.Background = originalBackground;

            if (!theme.IsDark)
                continue;

            if (TryGetExplicitColor(color.Name, theme.Kind, out var explicitColor))
            {
                color.Foreground = new SimpleHighlightingBrush(explicitColor);
                continue;
            }

            var foreground = GetBrushColor(color.Foreground);
            if (foreground is not null && NeedsForegroundRemap(foreground.Value, theme))
                color.Foreground = new SimpleHighlightingBrush(AdjustForegroundForDarkTheme(foreground.Value, theme));

            var background = GetBrushColor(color.Background);
            if (background is not null && NeedsBackgroundRemap(background.Value, theme))
                color.Background = new SimpleHighlightingBrush(AdjustBackgroundForDarkTheme(background.Value, theme));
        }
    }

    private static bool TryGetExplicitColor(string name, EditorThemeKind kind, out Color color)
    {
        var palette = kind == EditorThemeKind.Solarized ? SolarizedSyntaxColors : DarkSyntaxColors;
        return palette.TryGetValue(name, out color);
    }

    private static readonly Dictionary<string, Color> DarkSyntaxColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Heading"] = Rgb(220, 220, 170),
        ["BlockQuote"] = Rgb(156, 220, 254),
        ["Link"] = Rgb(79, 193, 255),
        ["Image"] = Rgb(78, 201, 176),
        ["Keyword"] = Rgb(86, 156, 214),
        ["Keywords"] = Rgb(86, 156, 214),
        ["ValueTypeKeywords"] = Rgb(86, 156, 214),
        ["ReferenceTypeKeywords"] = Rgb(86, 156, 214),
        ["Visibility"] = Rgb(86, 156, 214),
        ["OperatorKeywords"] = Rgb(86, 156, 214),
        ["GotoKeywords"] = Rgb(86, 156, 214),
        ["ContextKeywords"] = Rgb(86, 156, 214),
        ["String"] = Rgb(206, 145, 120),
        ["Char"] = Rgb(206, 145, 120),
        ["StringInterpolation"] = Rgb(206, 145, 120),
        ["Comment"] = Rgb(106, 153, 85),
        ["Preprocessor"] = Rgb(106, 153, 85),
        ["NamespaceKeywords"] = Rgb(106, 153, 85),
        ["NumberLiteral"] = Rgb(181, 206, 168),
        ["MethodCall"] = Rgb(220, 220, 170),
        ["ExceptionKeywords"] = Rgb(78, 201, 176),
        ["TrueFalse"] = Rgb(78, 201, 176),
        ["TypeKeywords"] = Rgb(78, 201, 176),
        ["SemanticKeywords"] = Rgb(78, 201, 176),
        ["Modifiers"] = Rgb(206, 145, 120),
        ["GetSetAddRemove"] = Rgb(206, 145, 120),
        ["ParameterModifiers"] = Rgb(206, 145, 120),
        ["UnsafeKeywords"] = Rgb(220, 220, 170),
        ["CheckedKeyword"] = Rgb(180, 180, 180)
    };

    private static readonly Dictionary<string, Color> SolarizedSyntaxColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Heading"] = Rgb(181, 137, 0),
        ["BlockQuote"] = Rgb(147, 161, 161),
        ["Link"] = Rgb(38, 139, 209),
        ["Image"] = Rgb(42, 161, 152),
        ["Keyword"] = Rgb(38, 139, 209),
        ["Keywords"] = Rgb(38, 139, 209),
        ["ValueTypeKeywords"] = Rgb(38, 139, 209),
        ["ReferenceTypeKeywords"] = Rgb(38, 139, 209),
        ["Visibility"] = Rgb(38, 139, 209),
        ["OperatorKeywords"] = Rgb(38, 139, 209),
        ["GotoKeywords"] = Rgb(38, 139, 209),
        ["ContextKeywords"] = Rgb(38, 139, 209),
        ["String"] = Rgb(42, 161, 152),
        ["Char"] = Rgb(42, 161, 152),
        ["StringInterpolation"] = Rgb(42, 161, 152),
        ["Comment"] = Rgb(88, 110, 117),
        ["Preprocessor"] = Rgb(88, 110, 117),
        ["NamespaceKeywords"] = Rgb(88, 110, 117),
        ["NumberLiteral"] = Rgb(211, 54, 47),
        ["MethodCall"] = Rgb(211, 54, 47),
        ["ExceptionKeywords"] = Rgb(42, 161, 152),
        ["TrueFalse"] = Rgb(42, 161, 152),
        ["TypeKeywords"] = Rgb(42, 161, 152),
        ["SemanticKeywords"] = Rgb(42, 161, 152),
        ["Modifiers"] = Rgb(203, 75, 22),
        ["GetSetAddRemove"] = Rgb(203, 75, 22),
        ["ParameterModifiers"] = Rgb(203, 75, 22),
        ["UnsafeKeywords"] = Rgb(181, 137, 0),
        ["CheckedKeyword"] = Rgb(147, 161, 161)
    };

    private static Color? GetBrushColor(HighlightingBrush? brush) =>
        brush is null ? null : brush.GetColor(null);

    private static bool NeedsForegroundRemap(Color color, EditorTheme theme)
    {
        var contrast = ContrastRatio(color, theme.Background);
        if (contrast >= 4.0)
            return false;

        return RelativeLuminance(color) <= RelativeLuminance(theme.Background) + 0.12;
    }

    private static bool NeedsBackgroundRemap(Color color, EditorTheme theme)
    {
        var contrast = ContrastRatio(color, theme.Background);
        return contrast < 1.8 || RelativeLuminance(color) > RelativeLuminance(theme.Text) - 0.05;
    }

    private static Color AdjustForegroundForDarkTheme(Color color, EditorTheme theme)
    {
        var (h, s, l) = RgbToHsl(color);
        var bgLuminance = RelativeLuminance(theme.Background);
        var target = Math.Clamp(bgLuminance + 0.42, 0.58, 0.9);
        if (l < target)
            l = target;
        if (s < 0.12 && l < 0.72)
            l = 0.72;
        return HslToRgb(h, Math.Max(s, 0.2), l);
    }

    private static Color AdjustBackgroundForDarkTheme(Color color, EditorTheme theme)
    {
        var bgLuminance = RelativeLuminance(theme.Background);
        var target = Math.Clamp(bgLuminance + 0.08, 0.12, 0.28);
        var current = RelativeLuminance(color);
        if (current > target + 0.2)
            return Color.FromRgb(
                ScaleChannel(color.R, target, current),
                ScaleChannel(color.G, target, current),
                ScaleChannel(color.B, target, current));
        return color;
    }

    private static byte ScaleChannel(byte channel, double targetLuminance, double currentLuminance)
    {
        if (currentLuminance <= 0)
            return (byte)(targetLuminance * 255);
        var scaled = channel * (targetLuminance / currentLuminance);
        return (byte)Math.Clamp(scaled, 0, 255);
    }

    private static double ContrastRatio(Color a, Color b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte c)
        {
            var v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static (double H, double S, double L) RgbToHsl(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;
        if (Math.Abs(max - min) < 0.00001)
            return (0, 0, l);

        var d = max - min;
        var s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        double h;
        if (max == r)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;
        h /= 6.0;
        return (h, s, l);
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        if (s <= 0.00001)
        {
            var gray = (byte)Math.Round(l * 255);
            return Color.FromRgb(gray, gray, gray);
        }

        static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        var r = HueToRgb(p, q, h + 1.0 / 3.0);
        var g = HueToRgb(p, q, h);
        var b = HueToRgb(p, q, h - 1.0 / 3.0);
        return Color.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255));
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}