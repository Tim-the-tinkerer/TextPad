using System.IO;
using System.Text;
using System.Text.Json;
using TextPad.Services;

namespace TextPad.Models;

public enum LineEndingPolicy
{
    Preserve,
    Lf,
    CrLf
}

public sealed class EditorPreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "com.textpad.editor",
        "settings.json");

    public static EditorPreferences Instance { get; } = Load();

    public EditorThemeKind Theme { get; set; } = EditorThemeKind.System;
    public bool WordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public bool HighlightCurrentLine { get; set; } = true;
    public bool ShowInvisibles { get; set; }
    public int TabWidth { get; set; } = 4;
    public int FontSize { get; set; } = 13;
    public string FontFamily { get; set; } = "Consolas";
    public LineEndingPolicy LineEndingPolicy { get; set; } = LineEndingPolicy.Preserve;
    public Encoding DefaultEncoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 60;
    public List<string> RecentFiles { get; set; } = new();

    public EditorTheme EffectiveTheme => EditorTheme.For(Theme);

    public bool IsDarkTheme => EffectiveTheme.IsDark;

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var dto = new SettingsDto
        {
            Theme = Theme.ToString(),
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            HighlightCurrentLine = HighlightCurrentLine,
            ShowInvisibles = ShowInvisibles,
            TabWidth = TabWidth,
            FontSize = FontSize,
            FontFamily = FontFamily,
            LineEndingPolicy = LineEndingPolicy.ToString(),
            DefaultEncodingName = DocumentEncoding.NameFor(DefaultEncoding),
            AutoSaveEnabled = AutoSaveEnabled,
            AutoSaveIntervalSeconds = AutoSaveIntervalSeconds,
            RecentFiles = RecentFiles
        };
        AtomicFileWriter.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 20)
            RecentFiles.RemoveRange(20, RecentFiles.Count - 20);
        Save();
    }

    private static EditorPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new EditorPreferences();

            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(SettingsPath));
            if (dto is null)
                return new EditorPreferences();

            return new EditorPreferences
            {
                Theme = Enum.TryParse<EditorThemeKind>(dto.Theme, out var theme) ? theme : EditorThemeKind.System,
                WordWrap = dto.WordWrap,
                ShowLineNumbers = dto.ShowLineNumbers,
                HighlightCurrentLine = dto.HighlightCurrentLine,
                ShowInvisibles = dto.ShowInvisibles,
                TabWidth = dto.TabWidth is > 0 and < 16 ? dto.TabWidth : 4,
                FontSize = dto.FontSize is >= 8 and <= 72 ? dto.FontSize : 13,
                FontFamily = string.IsNullOrWhiteSpace(dto.FontFamily) ? "Consolas" : dto.FontFamily,
                LineEndingPolicy = Enum.TryParse<LineEndingPolicy>(dto.LineEndingPolicy, out var le) ? le : LineEndingPolicy.Preserve,
                DefaultEncoding = DocumentEncoding.EncodingFromName(dto.DefaultEncodingName),
                AutoSaveEnabled = dto.AutoSaveEnabled,
                AutoSaveIntervalSeconds = dto.AutoSaveIntervalSeconds is >= 15 and <= 600 ? dto.AutoSaveIntervalSeconds : 60,
                RecentFiles = dto.RecentFiles ?? new List<string>()
            };
        }
        catch
        {
            return new EditorPreferences();
        }
    }

    private sealed class SettingsDto
    {
        public string Theme { get; set; } = "System";
        public bool WordWrap { get; set; } = true;
        public bool ShowLineNumbers { get; set; } = true;
        public bool HighlightCurrentLine { get; set; } = true;
        public bool ShowInvisibles { get; set; }
        public int TabWidth { get; set; } = 4;
        public int FontSize { get; set; } = 13;
        public string FontFamily { get; set; } = "Consolas";
        public string LineEndingPolicy { get; set; } = "Preserve";
        public string? DefaultEncodingName { get; set; }
        public bool AutoSaveEnabled { get; set; } = true;
        public int AutoSaveIntervalSeconds { get; set; } = 60;
        public List<string>? RecentFiles { get; set; }
    }
}