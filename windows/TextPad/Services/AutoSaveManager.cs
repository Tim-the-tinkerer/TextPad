using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using TextPad.Controls;
using TextPad.Models;

namespace TextPad.Services;

public sealed class AutoSaveEntry
{
    public Guid DocumentId { get; init; }
    public string? FilePath { get; init; }
    public string DisplayName { get; init; } = "Untitled";
    public string Content { get; init; } = string.Empty;
    public string EncodingName { get; init; } = "UTF-8";
    public string LineEnding { get; init; } = "LF";
    public string Format { get; init; } = "PlainText";
    public string? RtfDataBase64 { get; init; }
    public DateTime SavedAt { get; init; }
}

public sealed class AutoSaveManager
{
    public const int MaxAutoSaveCharacters = 500_000;

    private static readonly TimeSpan DismissedArchiveLifetime = TimeSpan.FromDays(30);

    private static string AutoSaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "com.textpad.editor",
        "Autosave");

    private static string DismissedDirectory => Path.Combine(AutoSaveDirectory, "dismissed");

    private readonly Dictionary<Guid, DispatcherTimer> _timers = new();
    private readonly Dictionary<Guid, EventHandler> _timerHandlers = new();

    public static void PerformMaintenance()
    {
        CleanupTempFiles();
        PruneDismissedArchive();
    }

    public void Attach(EditorTab tab, Dispatcher dispatcher)
    {
        var id = tab.Document.DocumentId;
        Detach(id);

        if (!EditorPreferences.Instance.AutoSaveEnabled)
            return;

        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(EditorPreferences.Instance.AutoSaveIntervalSeconds)
        };
        EventHandler handler = (_, _) => WriteSnapshot(tab);
        timer.Tick += handler;
        timer.Start();
        _timers[id] = timer;
        _timerHandlers[id] = handler;
    }

    public void Detach(Guid documentId)
    {
        if (_timers.Remove(documentId, out var timer))
        {
            if (_timerHandlers.Remove(documentId, out var handler))
                timer.Tick -= handler;
            timer.Stop();
        }
    }

    public void StopAll()
    {
        foreach (var (id, timer) in _timers)
        {
            if (_timerHandlers.Remove(id, out var handler))
                timer.Tick -= handler;
            timer.Stop();
        }
        _timers.Clear();
        _timerHandlers.Clear();
    }

    public void WriteSnapshot(EditorTab tab)
    {
        if (tab.IsDisposed)
            return;

        var document = tab.Document;
        if (tab.TextLength > MaxAutoSaveCharacters)
            return;

        if (!document.IsDirty && !string.IsNullOrEmpty(document.FilePath))
        {
            RemoveSnapshot(document.DocumentId);
            return;
        }

        if (MatchesDiskFile(tab))
        {
            RemoveSnapshot(document.DocumentId);
            return;
        }

        var entry = new AutoSaveEntry
        {
            DocumentId = document.DocumentId,
            FilePath = document.FilePath,
            DisplayName = document.DisplayName,
            Content = tab.Text,
            EncodingName = document.IsRichText ? "RTF" : DocumentEncoding.NameFor(document.Encoding),
            LineEnding = document.LineEnding.ToString(),
            Format = document.Format.ToString(),
            RtfDataBase64 = document.IsRichText ? Convert.ToBase64String(tab.GetRtfBytes()) : null,
            SavedAt = DateTime.UtcNow
        };

        try
        {
            Directory.CreateDirectory(AutoSaveDirectory);
            var path = Path.Combine(AutoSaveDirectory, $"{document.DocumentId}.json");
            var tempPath = Path.Combine(AutoSaveDirectory, $"{document.DocumentId}.{Guid.NewGuid():N}.tmp");
            var json = JsonSerializer.Serialize(entry);
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            // Best-effort.
        }
    }

    public void RemoveSnapshot(Guid documentId)
    {
        try
        {
            var path = Path.Combine(AutoSaveDirectory, $"{documentId}.json");
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort.
        }
    }

    public static void ArchiveDismissedSnapshot(Guid documentId)
    {
        try
        {
            var path = Path.Combine(AutoSaveDirectory, $"{documentId}.json");
            if (!File.Exists(path))
                return;

            Directory.CreateDirectory(DismissedDirectory);
            var dest = Path.Combine(DismissedDirectory, $"{documentId}.json");
            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(path, dest);
        }
        catch
        {
            // Best-effort.
        }
    }

    public static IReadOnlyList<AutoSaveEntry> LoadRecoveryEntries()
    {
        var dir = AutoSaveDirectory;
        if (!Directory.Exists(dir))
            return Array.Empty<AutoSaveEntry>();

        var entries = new List<AutoSaveEntry>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<AutoSaveEntry>(File.ReadAllText(file));
                if (entry is not null)
                    entries.Add(entry);
            }
            catch
            {
                // Skip corrupt snapshots.
            }
        }

        return entries.OrderByDescending(e => e.SavedAt).ToList();
    }

    private static void CleanupTempFiles()
    {
        if (!Directory.Exists(AutoSaveDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(AutoSaveDirectory, "*.tmp"))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Best-effort.
            }
        }
    }

    private static void PruneDismissedArchive()
    {
        if (!Directory.Exists(DismissedDirectory))
            return;

        var cutoff = DateTime.UtcNow - DismissedArchiveLifetime;
        foreach (var file in Directory.EnumerateFiles(DismissedDirectory, "*.json"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // Best-effort.
            }
        }
    }

    private static bool MatchesDiskFile(EditorTab tab)
    {
        var path = tab.Document.FilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        try
        {
            if (tab.Document.IsRichText)
            {
                var disk = SafeFileReader.ReadAllBytes(path);
                var current = tab.GetRtfBytes();
                return disk.AsSpan().SequenceEqual(current);
            }

            var diskBytes = SafeFileReader.ReadAllBytes(path);
            var saveBytes = tab.Document.BuildBytesForSave(tab.Text);
            return diskBytes.AsSpan().SequenceEqual(saveBytes);
        }
        catch
        {
            return false;
        }
    }

}