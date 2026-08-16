using System.IO;
using System.Windows.Threading;

namespace TextPad.Services;

public sealed class FileChangeMonitor : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan SuppressAfterSave = TimeSpan.FromSeconds(2);

    private FileSystemWatcher? _watcher;
    private string? _path;
    private Action? _onChanged;
    private Dispatcher? _dispatcher;
    private DispatcherTimer? _debounceTimer;
    private EventHandler? _debounceTickHandler;
    private DateTime _suppressUntilUtc;

    public void Watch(string path, Action onChanged, Dispatcher? dispatcher = null)
    {
        Stop();
        _path = path;
        _onChanged = onChanged;
        _dispatcher = dispatcher;

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        if (dispatcher is not null)
        {
            _debounceTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = DebounceInterval
            };
            _debounceTickHandler = (_, _) =>
            {
                _debounceTimer!.Stop();
                NotifyIfNotSuppressed();
            };
            _debounceTimer.Tick += _debounceTickHandler;
        }

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    public void SuppressBriefly() => _suppressUntilUtc = DateTime.UtcNow.Add(SuppressAfterSave);

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(e.FullPath, _path, StringComparison.OrdinalIgnoreCase))
            return;

        if (_debounceTimer is not null && _dispatcher is not null)
        {
            _dispatcher.BeginInvoke(() =>
            {
                _debounceTimer!.Stop();
                _debounceTimer.Start();
            });
            return;
        }

        NotifyIfNotSuppressed();
    }

    private void NotifyIfNotSuppressed()
    {
        if (DateTime.UtcNow < _suppressUntilUtc)
            return;

        _onChanged?.Invoke();
    }

    public void Stop()
    {
        if (_debounceTimer is not null)
        {
            if (_debounceTickHandler is not null)
                _debounceTimer.Tick -= _debounceTickHandler;
            _debounceTimer.Stop();
            _debounceTimer = null;
            _debounceTickHandler = null;
        }

        _dispatcher = null;

        if (_watcher is null)
            return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileEvent;
        _watcher.Created -= OnFileEvent;
        _watcher.Renamed -= OnFileEvent;
        _watcher.Deleted -= OnFileEvent;
        _watcher.Dispose();
        _watcher = null;
        _path = null;
        _onChanged = null;
    }

    public void Dispose() => Stop();
}