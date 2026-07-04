using System.Windows.Media;

namespace TextPad.Models;

public enum EditorThemeKind
{
    System,
    Light,
    Dark,
    Solarized,
    Sepia
}

public sealed class EditorTheme
{
    public EditorThemeKind Kind { get; init; }
    public Color Background { get; init; }
    public Color Text { get; init; }
    public Color LineNumberText { get; init; }
    public Color CurrentLineHighlight { get; init; }
    public Color Selection { get; init; }
    public Color UiAccent { get; init; }
    public Color TabBarBackground { get; init; }
    public Color TabSelectedBackground { get; init; }
    public Color TabText { get; init; }
    public Color TabTextSelected { get; init; }
    public bool IsDark { get; init; }

    public static EditorTheme For(EditorThemeKind kind)
    {
        var resolved = kind;
        if (kind == EditorThemeKind.System)
            resolved = IsSystemDark() ? EditorThemeKind.Dark : EditorThemeKind.Light;

        return resolved switch
        {
            EditorThemeKind.Dark => new EditorTheme
            {
                Kind = EditorThemeKind.Dark,
                Background = ColorFromRgb(38, 38, 43),
                Text = ColorFromRgb(255, 255, 255),
                LineNumberText = ColorFromRgb(209, 209, 209),
                CurrentLineHighlight = ColorFromRgb(56, 61, 71),
                Selection = ColorFromArgb(153, 64, 89, 140),
                UiAccent = ColorFromRgb(89, 158, 255),
                TabBarBackground = ColorFromRgb(26, 26, 31),
                TabSelectedBackground = ColorFromRgb(38, 38, 43),
                TabText = ColorFromRgb(219, 219, 219),
                TabTextSelected = ColorFromRgb(255, 255, 255),
                IsDark = true
            },
            EditorThemeKind.Solarized => new EditorTheme
            {
                Kind = EditorThemeKind.Solarized,
                Background = ColorFromRgb(5, 48, 61),
                Text = ColorFromRgb(240, 242, 235),
                LineNumberText = ColorFromRgb(199, 207, 196),
                CurrentLineHighlight = ColorFromRgb(10, 66, 79),
                Selection = ColorFromArgb(204, 0, 71, 84),
                UiAccent = ColorFromRgb(38, 140, 209),
                TabBarBackground = ColorFromRgb(0, 33, 41),
                TabSelectedBackground = ColorFromRgb(5, 48, 61),
                TabText = ColorFromRgb(204, 212, 201),
                TabTextSelected = ColorFromRgb(247, 250, 245),
                IsDark = true
            },
            EditorThemeKind.Sepia => new EditorTheme
            {
                Kind = EditorThemeKind.Sepia,
                Background = ColorFromRgb(245, 237, 219),
                Text = ColorFromRgb(31, 20, 13),
                LineNumberText = ColorFromRgb(97, 77, 56),
                CurrentLineHighlight = ColorFromRgb(230, 219, 199),
                Selection = ColorFromArgb(140, 217, 191, 140),
                UiAccent = ColorFromRgb(133, 82, 31),
                TabBarBackground = ColorFromRgb(230, 219, 199),
                TabSelectedBackground = ColorFromRgb(245, 237, 219),
                TabText = ColorFromRgb(107, 87, 66),
                TabTextSelected = ColorFromRgb(31, 20, 13),
                IsDark = false
            },
            _ => new EditorTheme
            {
                Kind = resolved,
                Background = ColorFromRgb(255, 255, 255),
                Text = ColorFromRgb(13, 13, 18),
                LineNumberText = ColorFromRgb(92, 92, 102),
                CurrentLineHighlight = ColorFromRgb(230, 237, 250),
                Selection = ColorFromRgb(191, 217, 255),
                UiAccent = ColorFromRgb(0, 115, 242),
                TabBarBackground = ColorFromRgb(240, 240, 245),
                TabSelectedBackground = ColorFromRgb(255, 255, 255),
                TabText = ColorFromRgb(77, 77, 87),
                TabTextSelected = ColorFromRgb(13, 13, 18),
                IsDark = false
            }
        };
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Color ColorFromRgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static Color ColorFromArgb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}