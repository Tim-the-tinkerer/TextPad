using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using TextPad.Controls;
using TextPad.Dialogs;
using TextPad.Models;
using TextPad.Services;

namespace TextPad;

public partial class MainWindow : Window
{
    private readonly ClosedTabManager _closedTabs = new();
    private readonly AutoSaveManager _autoSave = new();
    private FindReplaceDialog? _findReplaceDialog;
    private int _findMatchIndex;
    private IReadOnlyList<TextSearchMatch> _findMatches = Array.Empty<TextSearchMatch>();
    private string[]? _launchFiles;
    private readonly List<string> _pendingExternalFiles = new();
    private bool _isShuttingDown;
    private bool _documentsInitialized;
    private int _fileOpenOperations;
    private readonly HashSet<Guid> _externalChangePrompts = new();
    private readonly Dictionary<EditorTab, TabEventHandlers> _tabEventHandlers = new();
    private DispatcherTimer? _findDebounceTimer;
    private DispatcherTimer? _editFindDebounceTimer;
    private string _pendingFindQuery = string.Empty;
    private readonly HashSet<string> _openingPaths = new(StringComparer.OrdinalIgnoreCase);

    private sealed class TabEventHandlers(
        EventHandler closeRequested,
        EventHandler contentChanged,
        EventHandler caretMoved,
        EventHandler externalFileChanged)
    {
        public EventHandler CloseRequested { get; } = closeRequested;
        public EventHandler ContentChanged { get; } = contentChanged;
        public EventHandler CaretMoved { get; } = caretMoved;
        public EventHandler ExternalFileChanged { get; } = externalFileChanged;
    }

    public MainWindow(string[]? launchFiles = null)
    {
        InitializeComponent();
        _launchFiles = launchFiles;
        ApplyViewMenuChecks();
        RefreshRecentMenu();
        Loaded += OnWindowLoaded;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SetupKeyboardShortcuts();
    }

    private void SetupKeyboardShortcuts()
    {
        BindShortcut(Key.N, ModifierKeys.Control, () => NewUntitledTab());
        BindShortcut(Key.T, ModifierKeys.Control, () => NewUntitledTab());
        BindShortcut(Key.O, ModifierKeys.Control, () => OpenFile_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.S, ModifierKeys.Control, () => SaveFile_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.S, ModifierKeys.Control | ModifierKeys.Shift, () => SaveFileAs_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.W, ModifierKeys.Control, () => { if (ActiveTab is not null) CloseTab(ActiveTab); });
        BindShortcut(Key.W, ModifierKeys.Control | ModifierKeys.Shift, () => CloseWindow_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.T, ModifierKeys.Control | ModifierKeys.Shift, () => ReopenTab_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.Tab, ModifierKeys.Control, () => SelectRelativeTab(1));
        BindShortcut(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, () => SelectRelativeTab(-1));
        BindShortcut(Key.Z, ModifierKeys.Control, () => Undo_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.Y, ModifierKeys.Control, () => Redo_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.X, ModifierKeys.Control, () => Cut_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.C, ModifierKeys.Control, () => Copy_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.V, ModifierKeys.Control, () => Paste_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.A, ModifierKeys.Control, () => SelectAll_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.V, ModifierKeys.Control | ModifierKeys.Shift, () => PasteMatchStyle_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.F, ModifierKeys.Control, () => Find_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.H, ModifierKeys.Control, () => FindReplace_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.G, ModifierKeys.Control, () => FindNext_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.G, ModifierKeys.Control | ModifierKeys.Shift, () => FindPrevious_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.L, ModifierKeys.Control, () => GoToLine_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.P, ModifierKeys.Control, () => Print_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.B, ModifierKeys.Control, () => Bold_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.I, ModifierKeys.Control, () => Italic_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.U, ModifierKeys.Control, () => Underline_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.OemPlus, ModifierKeys.Control, () => ZoomIn_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.Add, ModifierKeys.Control, () => ZoomIn_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.OemMinus, ModifierKeys.Control, () => ZoomOut_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.Subtract, ModifierKeys.Control, () => ZoomOut_Click(this, new RoutedEventArgs()));
        BindShortcut(Key.F1, ModifierKeys.None, OpenHelp);

        for (var i = 0; i < 9; i++)
        {
            var index = i;
            BindShortcut(Key.D1 + i, ModifierKeys.Control, () => SelectTabIndex(index));
        }
    }

    private void BindShortcut(Key key, ModifierKeys modifiers, Action action)
    {
        var command = new RoutedUICommand();
        CommandBindings.Add(new CommandBinding(command, (_, _) => action()));
        InputBindings.Add(new KeyBinding(command, key, modifiers));
    }

    private void SelectRelativeTab(int delta)
    {
        if (DocumentTabs.Items.Count == 0)
            return;

        var current = DocumentTabs.SelectedIndex;
        if (current < 0)
            current = 0;

        DocumentTabs.SelectedIndex = (current + delta + DocumentTabs.Items.Count) % DocumentTabs.Items.Count;
        ActiveTab?.Focus();
        RefreshTabBar();
    }

    private void SelectTabIndex(int index)
    {
        if (index < 0 || index >= DocumentTabs.Items.Count)
            return;

        DocumentTabs.SelectedIndex = index;
        ActiveTab?.Focus();
        RefreshTabBar();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_documentsInitialized)
            return;

        _documentsInitialized = true;
        InitializeDocuments(_launchFiles);
        _launchFiles = null;
        ApplyThemeToAllTabs();

        if (_pendingExternalFiles.Count > 0)
        {
            var pending = _pendingExternalFiles.ToArray();
            _pendingExternalFiles.Clear();
            OpenExternalFiles(pending);
        }
    }

    public void OpenExternalFiles(IReadOnlyList<string> paths)
    {
        if (!_documentsInitialized)
        {
            _pendingExternalFiles.AddRange(paths);
            return;
        }

        _ = OpenFilesAsync(paths);

        ActivateWindow();
    }

    public void ActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void InitializeDocuments(string[]? launchFiles)
    {
        OfferAutoSaveRecovery();

        if (launchFiles is { Length: > 0 })
        {
            _ = OpenFilesAsync(launchFiles.Where(File.Exists));
        }

        if (DocumentTabs.Items.Count == 0)
            NewUntitledTab();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General &&
            EditorPreferences.Instance.Theme == EditorThemeKind.System)
            ApplyThemeToAllTabs();
    }

    private void OfferAutoSaveRecovery()
    {
        AutoSaveManager.PerformMaintenance();

        while (true)
        {
            var entries = AutoSaveManager.LoadRecoveryEntries();
            if (entries.Count == 0)
                return;

            var entry = entries[0];
            var formatter = new System.Globalization.CultureInfo("en-US").DateTimeFormat;
            var when = entry.SavedAt.ToLocalTime().ToString("g", formatter);
            var result = System.Windows.MessageBox.Show(
                this,
                $"Found an auto-saved copy of \"{entry.DisplayName}\" from {when}.\n\nRecover this document?",
                "TextPad",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (TryOpenRecoveredEntry(entry))
                    _autoSave.RemoveSnapshot(entry.DocumentId);
                else
                    AutoSaveManager.ArchiveDismissedSnapshot(entry.DocumentId);
            }
            else
            {
                AutoSaveManager.ArchiveDismissedSnapshot(entry.DocumentId);
            }
        }
    }

    private EditorTab? ActiveTab =>
        DocumentTabs.SelectedItem is TabItem item ? item.Tag as EditorTab : null;

    private void NewUntitledTab(string? filePath = null, string? content = null, Encoding? encoding = null)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            OpenFile(filePath, encoding);
            return;
        }

        var doc = new EditorDocument { FilePath = filePath };
        if (encoding is not null)
            doc.Encoding = encoding;
        AddTab(EditorTab.Create(doc, content ?? string.Empty));
    }

    private async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            await OpenFileAsync(path);
    }

    private async void OpenFile(string path, Encoding? encoding = null) =>
        await OpenFileAsync(path, encoding);

    private async Task OpenFileAsync(string path, Encoding? encoding = null)
    {
        var normalized = Path.GetFullPath(path);
        if (!File.Exists(normalized))
        {
            System.Windows.MessageBox.Show(this, $"The file could not be found:\n{normalized}", "Open Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_openingPaths.Add(normalized))
            return;

        EditorTab? tab = null;
        var replaceEmptyTab = HasSingleEmptyUntitledTab();
        var emptyTab = replaceEmptyTab ? ActiveTab : null;
        BeginFileOpenOperation();

        try
        {
            var existing = FindTabByPath(normalized);
            if (existing is not null)
            {
                DocumentTabs.SelectedItem = existing.TabItem;
                existing.Focus();
                RefreshTabBar();
                UpdateStatusBar();
                return;
            }

            if (replaceEmptyTab && emptyTab is not null && IsTabOpen(emptyTab))
                DisposeTab(emptyTab);

            var (doc, payload) = await Task.Run(() =>
            {
                var loaded = EditorDocument.LoadFromFile(normalized, encoding);
                if (loaded.IsRichText && loaded.RtfData is not null)
                    return (loaded, (PlainTextOpenPayload?)null);
                return (loaded, PlainTextOpenPayload.FromDocument(loaded));
            });

            existing = FindTabByPath(normalized);
            if (existing is not null)
            {
                DocumentTabs.SelectedItem = existing.TabItem;
                existing.Focus();
                RefreshTabBar();
                UpdateStatusBar();
                return;
            }

            if (doc.IsRichText && doc.RtfData is not null)
            {
                tab = EditorTab.Create(doc);
                AddTab(tab);
            }
            else
            {
                tab = EditorTab.CreateShell(payload!.Document);
                tab.TabHeader.Title = payload.Document.DisplayName + " …";
                AddTab(tab);
                await tab.PopulatePlainContentAsync(payload);
                tab.RefreshTabTitle();
            }

            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            if (tab is not null && IsTabOpen(tab))
                DisposeTab(tab);

            if (DocumentTabs.Items.Count == 0)
                NewUntitledTab();

            System.Windows.MessageBox.Show(this, ex.Message, "Open Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _openingPaths.Remove(normalized);
            EndFileOpenOperation();
        }
    }

    private void BeginFileOpenOperation()
    {
        if (Interlocked.Increment(ref _fileOpenOperations) == 1)
            Mouse.OverrideCursor = Cursors.Wait;
    }

    private void EndFileOpenOperation()
    {
        if (Interlocked.Decrement(ref _fileOpenOperations) == 0)
            Mouse.OverrideCursor = null;
    }

    private EditorTab? FindTabByPath(string path)
    {
        foreach (TabItem item in DocumentTabs.Items)
        {
            if (item.Tag is not EditorTab tab || string.IsNullOrEmpty(tab.Document.FilePath))
                continue;

            if (string.Equals(Path.GetFullPath(tab.Document.FilePath), path, StringComparison.OrdinalIgnoreCase))
                return tab;
        }

        return null;
    }

    private bool HasSingleEmptyUntitledTab()
    {
        if (DocumentTabs.Items.Count != 1 || ActiveTab is not { } tab)
            return false;

        return string.IsNullOrEmpty(tab.Document.FilePath) &&
               !tab.Document.IsDirty &&
               tab.TextLength == 0;
    }

    private bool TryOpenRecoveredEntry(AutoSaveEntry entry)
    {
        if (!ConfirmRecoveryAgainstDisk(entry))
            return false;

        if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
        {
            var normalized = Path.GetFullPath(entry.FilePath);
            if (FindTabByPath(normalized) is { } existing)
            {
                DocumentTabs.SelectedItem = existing.TabItem;
                existing.Focus();
                RefreshTabBar();

                var choice = System.Windows.MessageBox.Show(
                    this,
                    $"\"{entry.DisplayName}\" is already open.\n\nYes = Replace the open tab with the recovered version\nNo = Keep the current tab\nCancel = Don't recover",
                    "TextPad",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel)
                    return false;
                if (choice == MessageBoxResult.No)
                {
                    AutoSaveManager.ArchiveDismissedSnapshot(entry.DocumentId);
                    return false;
                }

                if (!CloseTab(existing))
                    return false;
            }
        }

        try
        {
            OpenRecoveredEntry(entry.FilePath, entry.Content, entry.Format, entry.RtfDataBase64, entry.EncodingName);
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this,
                $"Could not recover \"{entry.DisplayName}\":\n{ex.Message}",
                "Recovery Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private bool ConfirmRecoveryAgainstDisk(AutoSaveEntry entry)
    {
        if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
            return true;

        var diskTime = File.GetLastWriteTimeUtc(entry.FilePath);
        if (diskTime <= entry.SavedAt.AddSeconds(1))
            return true;

        var result = System.Windows.MessageBox.Show(
            this,
            $"\"{entry.DisplayName}\" was modified on disk after the auto-saved copy.\n\nRecover the auto-saved version anyway?",
            "TextPad",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void OpenRecoveredEntry(string? filePath, string content, string? format, string? rtfBase64, string? encodingName)
    {
        var doc = new EditorDocument { FilePath = filePath, IsDirty = true };
        if (Enum.TryParse<DocumentFormat>(format, out var parsed) && parsed == DocumentFormat.RichText && !string.IsNullOrEmpty(rtfBase64))
        {
            try
            {
                doc.Format = DocumentFormat.RichText;
                doc.RtfData = Convert.FromBase64String(rtfBase64);
            }
            catch (FormatException ex)
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"The recovered RTF data is corrupt and could not be restored.\n\n{ex.Message}",
                    "Recovery Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            AddTab(EditorTab.Create(doc));
            return;
        }

        Encoding? encoding = null;
        if (!string.IsNullOrEmpty(encodingName))
        {
            foreach (var item in DocumentEncoding.Supported)
            {
                if (item.name == encodingName)
                {
                    encoding = item.encoding;
                    break;
                }
            }
        }

        if (encoding is not null)
            doc.Encoding = encoding;
        AddTab(EditorTab.Create(doc, content));
    }

    private void AddTab(EditorTab tab)
    {
        tab.TabItem.Tag = tab;
        WireTabEvents(tab);
        tab.SetContextMenu(CreateEditorContextMenu());

        DocumentTabs.Items.Add(tab.TabItem);
        DocumentTabs.SelectedItem = tab.TabItem;
        _autoSave.Attach(tab, Dispatcher);
        tab.Focus();
        RefreshTabBar();
        UpdateStatusBar();
    }

    private void ApplyChromeTheme()
    {
        var theme = EditorPreferences.Instance.EffectiveTheme;
        var chromeBackground = new SolidColorBrush(theme.TabBarBackground);
        var chromeForeground = new SolidColorBrush(theme.TabText);

        MainStatusBar.Background = chromeBackground;
        MainStatusBar.Foreground = chromeForeground;
        FindBar.ApplyTheme(theme);
    }

    private void RefreshTabBar()
    {
        var theme = EditorPreferences.Instance.EffectiveTheme;
        Resources["TabBarBackgroundBrush"] = new SolidColorBrush(theme.TabBarBackground);
        Resources["TabSelectedBackgroundBrush"] = new SolidColorBrush(theme.TabSelectedBackground);
        Resources["TabTextBrush"] = new SolidColorBrush(theme.TabText);
        Resources["TabTextSelectedBrush"] = new SolidColorBrush(theme.TabTextSelected);
        Resources["TabAccentBrush"] = new SolidColorBrush(theme.UiAccent);
        Resources["TabHoverBrush"] = new SolidColorBrush(
            theme.IsDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(32, 0, 0, 0));
        Resources["MenuBarBackgroundBrush"] = new SolidColorBrush(theme.TabBarBackground);
        Resources["MenuBarTextBrush"] = new SolidColorBrush(theme.TabText);
        Resources["MenuPopupBackgroundBrush"] = new SolidColorBrush(theme.TabSelectedBackground);
        Resources["MenuPopupTextBrush"] = new SolidColorBrush(theme.TabTextSelected);
        Resources["MenuShortcutTextBrush"] = new SolidColorBrush(theme.LineNumberText);
        Resources["MenuHighlightBrush"] = new SolidColorBrush(theme.CurrentLineHighlight);
        Resources["MenuDisabledTextBrush"] = new SolidColorBrush(
            theme.IsDark ? Color.FromRgb(128, 128, 128) : Color.FromRgb(160, 160, 168));
        Resources["MenuSeparatorBrush"] = new SolidColorBrush(
            theme.IsDark ? Color.FromArgb(64, 255, 255, 255) : Color.FromArgb(64, 0, 0, 0));
        Resources["MenuPopupBorderBrush"] = new SolidColorBrush(
            theme.IsDark ? Color.FromRgb(64, 70, 80) : Color.FromRgb(208, 208, 216));

        foreach (TabItem item in DocumentTabs.Items)
        {
            if (item.Tag is not EditorTab tab)
                continue;

            var selected = DocumentTabs.SelectedItem == item;
            tab.TabHeader.ApplyAppearance(
                selected,
                theme.TabText,
                theme.TabTextSelected,
                theme.UiAccent,
                theme.IsDark);
        }
    }

    private bool CloseTab(EditorTab tab, bool prompt = true)
    {
        var discardChanges = false;
        if (tab.Document.IsDirty && prompt)
        {
            var result = System.Windows.MessageBox.Show(
                $"Save changes to \"{tab.Document.DisplayName}\"?",
                "TextPad",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;
            if (result == MessageBoxResult.No)
                discardChanges = true;
            if (result == MessageBoxResult.Yes && !SaveTab(tab))
                return false;
        }

        if (!_isShuttingDown)
            RememberClosedTab(tab);

        if (discardChanges)
            _autoSave.RemoveSnapshot(tab.Document.DocumentId);

        DisposeTab(tab);

        if (!_isShuttingDown && DocumentTabs.Items.Count == 0)
            NewUntitledTab();

        return true;
    }

    private void RememberClosedTab(EditorTab tab)
    {
        var dirty = tab.Document.IsDirty;
        _closedTabs.Push(new ClosedTabSnapshot
        {
            Content = dirty ? tab.Text : string.Empty,
            FilePath = tab.Document.FilePath,
            EncodingName = tab.Document.IsRichText ? "RTF" : DocumentEncoding.NameFor(tab.Document.Encoding),
            Format = tab.Document.Format,
            RtfDataBase64 = tab.IsRichText && dirty ? Convert.ToBase64String(tab.GetRtfBytes()) : null
        });
    }

    private void DisposeTab(EditorTab tab)
    {
        UnwireTabEvents(tab);
        _autoSave.Detach(tab.Document.DocumentId);
        if (!tab.Document.IsDirty)
            _autoSave.RemoveSnapshot(tab.Document.DocumentId);
        DocumentTabs.Items.Remove(tab.TabItem);
        tab.Dispose();
    }

    private void WireTabEvents(EditorTab tab)
    {
        UnwireTabEvents(tab);

        EventHandler closeRequested = (_, _) => CloseTab(tab);
        EventHandler contentChanged = (_, _) =>
        {
            UpdateStatusBar();
            DebouncedSyncFindMatchesAfterEdit();
        };
        EventHandler caretMoved = (_, _) =>
        {
            tab.UpdateCurrentLineHighlight();
            UpdateStatusBar();
        };
        EventHandler externalFileChanged = (_, _) => OnExternalFileChanged(tab);

        tab.TabHeader.CloseRequested += closeRequested;
        tab.ContentChanged += contentChanged;
        tab.CaretMoved += caretMoved;
        tab.ExternalFileChanged += externalFileChanged;
        _tabEventHandlers[tab] = new TabEventHandlers(
            closeRequested, contentChanged, caretMoved, externalFileChanged);
    }

    private void UnwireTabEvents(EditorTab tab)
    {
        if (!_tabEventHandlers.Remove(tab, out var handlers))
            return;

        tab.TabHeader.CloseRequested -= handlers.CloseRequested;
        tab.ContentChanged -= handlers.ContentChanged;
        tab.CaretMoved -= handlers.CaretMoved;
        tab.ExternalFileChanged -= handlers.ExternalFileChanged;
    }

    private bool IsTabOpen(EditorTab tab) => DocumentTabs.Items.Contains(tab.TabItem);

    private bool SaveTab(EditorTab tab, string? path = null)
    {
        var savePath = path ?? tab.Document.FilePath;
        if (string.IsNullOrEmpty(savePath))
            return SaveTabAs(tab);

        var isRichText = tab.Document.IsRichText || tab.IsRichText;
        if (!DocumentFormatSupport.ValidateSavePath(savePath, isRichText, out var validationError))
        {
            System.Windows.MessageBox.Show(this, validationError, "Save Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var previousPath = tab.Document.FilePath;
        var previousFormat = tab.Document.Format;

        try
        {
            if (isRichText)
            {
                var rtfBytes = tab.GetRtfBytes();
                tab.Document.SaveRtf(rtfBytes, savePath);
                tab.Document.FilePath = savePath;
                tab.Document.Format = DocumentFormat.RichText;
                tab.Document.RtfData = rtfBytes;
                tab.Document.IsDirty = false;
            }
            else
            {
                tab.Document.SavePlainText(tab.Text, savePath);
                tab.Document.FilePath = savePath;
                tab.Document.Format = DocumentFormatSupport.Detect(savePath);
                tab.Document.PlainContent = tab.Text;
                tab.Document.IsDirty = false;
            }

            tab.RefreshTabTitle();
            RefreshTabBar();
            tab.ApplySyntaxHighlighting();
            tab.RefreshFileMonitoring();
            tab.Document.NoteSavedToDisk();
            tab.NotifySavedToDisk();
            _autoSave.RemoveSnapshot(tab.Document.DocumentId);
            EditorPreferences.Instance.AddRecent(savePath);
            RefreshRecentMenu();
            UpdateStatusBar();
            return true;
        }
        catch (Exception ex)
        {
            tab.Document.FilePath = previousPath;
            tab.Document.Format = previousFormat;
            System.Windows.MessageBox.Show(this, ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveTabAs(EditorTab tab)
    {
        var dialog = new SaveFileDialog
        {
            Filter = tab.IsRichText
                ? "Rich Text (*.rtf)|*.rtf|All files (*.*)|*.*"
                : "Plain Text (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = tab.Document.SuggestedSaveFileName
        };
        if (dialog.ShowDialog(this) != true)
            return false;
        return SaveTab(tab, dialog.FileName);
    }

    private void ApplyViewMenuChecks()
    {
        var prefs = EditorPreferences.Instance;
        WordWrapItem.IsChecked = prefs.WordWrap;
        LineNumbersItem.IsChecked = prefs.ShowLineNumbers;
        InvisiblesItem.IsChecked = prefs.ShowInvisibles;
        CurrentLineItem.IsChecked = prefs.HighlightCurrentLine;
    }

    private TextSearchOptions GetActiveSearchOptions()
    {
        if (FindBar.Visibility == Visibility.Visible)
            return new TextSearchOptions(FindBar.MatchCase);

        return _findReplaceDialog?.SearchOptions ?? new TextSearchOptions();
    }

    private void ApplyThemeToAllTabs()
    {
        SyntaxHighlighterSetup.RefreshAllThemeColors(EditorPreferences.Instance.EffectiveTheme);
        ApplyChromeTheme();
        foreach (TabItem item in DocumentTabs.Items)
        {
            if (item.Tag is EditorTab tab)
            {
                tab.ApplyPreferences();
                tab.UpdateCurrentLineHighlight();
            }
        }
        RefreshTabBar();
        UpdateStatusBar();
    }

    private void RefreshRecentMenu()
    {
        RecentMenu.Items.Clear();
        var files = EditorPreferences.Instance.RecentFiles.Where(File.Exists).ToList();
        if (files.Count == 0)
        {
            var empty = new MenuItem { Header = "(empty)", IsEnabled = false };
            RecentMenu.Items.Add(empty);
            return;
        }

        foreach (var file in files)
        {
            var item = new MenuItem { Header = file, Tag = file };
            item.Click += (_, _) => OpenFile(file);
            RecentMenu.Items.Add(item);
        }
        RecentMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Clear Menu" };
        clear.Click += (_, _) =>
        {
            EditorPreferences.Instance.RecentFiles.Clear();
            EditorPreferences.Instance.Save();
            RefreshRecentMenu();
        };
        RecentMenu.Items.Add(clear);
    }

    private void UpdateStatusBar()
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        var (line, col) = tab.GetCaretPosition();
        LineColStatus.Text = $"Ln {line}, Col {col}";
        CharCountStatus.Text = $"{tab.TextLength:N0} characters";
        EncodingStatus.Text = tab.IsRichText ? "RTF" : DocumentEncoding.NameFor(tab.Document.Encoding);
        LineEndingStatus.Text = tab.IsRichText ? string.Empty : tab.Document.LineEnding switch
        {
            LineEndingKind.CrLf => "CRLF",
            LineEndingKind.Cr => "CR",
            LineEndingKind.Mixed => "Mixed",
            LineEndingKind.Lf => "LF",
            _ => "—"
        };
    }

    private void SelectMatch(EditorTab tab, TextSearchMatch match) =>
        tab.Select(match.Start, match.Length);

    private void DebouncedSyncFindMatchesAfterEdit()
    {
        if (FindBar.Visibility != Visibility.Visible &&
            _findReplaceDialog is not { IsVisible: true } &&
            string.IsNullOrEmpty(_lastFindQuery))
            return;

        _editFindDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _editFindDebounceTimer.Stop();
        _editFindDebounceTimer.Tick -= OnEditFindDebounceTick;
        _editFindDebounceTimer.Tick += OnEditFindDebounceTick;
        _editFindDebounceTimer.Start();
    }

    private void OnEditFindDebounceTick(object? sender, EventArgs e)
    {
        _editFindDebounceTimer?.Stop();
        _editFindDebounceTimer!.Tick -= OnEditFindDebounceTick;
        SyncFindMatchesAfterEdit();
    }

    private void SyncFindMatchesAfterEdit()
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        string query;
        TextSearchOptions options;
        if (FindBar.Visibility == Visibility.Visible)
        {
            query = FindBar.Query;
            options = new TextSearchOptions(FindBar.MatchCase);
        }
        else if (_findReplaceDialog is { IsVisible: true })
        {
            query = _findReplaceDialog.FindText;
            options = _findReplaceDialog.SearchOptions;
        }
        else if (!string.IsNullOrEmpty(_lastFindQuery))
        {
            query = _lastFindQuery;
            options = _lastFindOptions;
        }
        else
        {
            return;
        }

        if (string.IsNullOrEmpty(query))
            return;

        RefreshFindMatches(tab, query, options, preserveSelection: true);
    }

    private void RefreshFindMatches(EditorTab tab, string query, TextSearchOptions options, bool preserveSelection = false)
    {
        var caretPosition = preserveSelection ? tab.SelectionStart : 0;
        _findMatches = EditorTextSearch.FindAll(tab.Text, query, options);
        _lastFindQuery = query;
        _lastFindOptions = options;
        if (_findMatches.Count > 0)
        {
            if (preserveSelection)
            {
                _findMatchIndex = ResolveMatchIndexAfterEdit(_findMatches, caretPosition);
                SelectMatch(tab, _findMatches[_findMatchIndex]);
                FindBar.SetMatchCount(_findMatchIndex + 1, _findMatches.Count);
            }
            else
            {
                _findMatchIndex = 0;
                SelectMatch(tab, _findMatches[0]);
                FindBar.SetMatchCount(1, _findMatches.Count);
            }
        }
        else
        {
            _findMatchIndex = 0;
            FindBar.SetMatchCount(0, 0);
        }
    }

    private static int ResolveMatchIndexAfterEdit(IReadOnlyList<TextSearchMatch> matches, int caretPosition)
    {
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (caretPosition >= match.Start && caretPosition < match.Start + match.Length)
                return i;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Start >= caretPosition)
                return i;
        }

        return matches.Count - 1;
    }

    private void FindNext(bool backward = false)
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        var query = FindBar.Visibility == Visibility.Visible
            ? FindBar.Query
            : _findReplaceDialog?.FindText ?? string.Empty;
        if (string.IsNullOrEmpty(query))
            return;

        var options = GetActiveSearchOptions();

        if (_findMatches.Count == 0 || !string.Equals(query, _lastFindQuery, StringComparison.Ordinal) || options != _lastFindOptions)
            RefreshFindMatches(tab, query, options);

        if (_findMatches.Count == 0)
            return;

        if (backward)
        {
            _findMatchIndex = _findMatchIndex < 0
                ? _findMatches.Count - 1
                : (_findMatchIndex - 1 + _findMatches.Count) % _findMatches.Count;
        }
        else
        {
            _findMatchIndex = (_findMatchIndex + 1) % _findMatches.Count;
        }

        SelectMatch(tab, _findMatches[_findMatchIndex]);
        FindBar.SetMatchCount(_findMatchIndex + 1, _findMatches.Count);
    }

    private string _lastFindQuery = string.Empty;
    private TextSearchOptions _lastFindOptions;

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isShuttingDown = true;
        _autoSave.StopAll();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Loaded -= OnWindowLoaded;
        _findDebounceTimer?.Stop();
        _editFindDebounceTimer?.Stop();

        var tabs = DocumentTabs.Items.Cast<TabItem>()
            .Select(item => item.Tag as EditorTab)
            .Where(tab => tab is not null)
            .Cast<EditorTab>()
            .ToList();

        foreach (var tab in tabs)
        {
            if (!tab.Document.IsDirty)
                continue;

            var result = System.Windows.MessageBox.Show(
                $"Save changes to \"{tab.Document.DisplayName}\"?",
                "TextPad",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                _isShuttingDown = false;
                e.Cancel = true;
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                ReapplyAutoSaveToAllTabs();
                return;
            }

            if (result == MessageBoxResult.Yes && !SaveTab(tab))
            {
                _isShuttingDown = false;
                e.Cancel = true;
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                ReapplyAutoSaveToAllTabs();
                return;
            }

            if (result == MessageBoxResult.No)
                _autoSave.RemoveSnapshot(tab.Document.DocumentId);
        }

        foreach (var tab in tabs)
        {
            if (IsTabOpen(tab))
                DisposeTab(tab);
        }

        EditorPreferences.Instance.Save();
        base.OnClosing(e);
    }

    private void NewFile_Click(object sender, RoutedEventArgs e) => NewUntitledTab();

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All files (*.*)|*.*|Rich Text (*.rtf)|*.rtf|Plain Text (*.txt)|*.txt",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        await OpenFilesAsync(dialog.FileNames);
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is not null)
            SaveTab(ActiveTab);
    }

    private void SaveFileAs_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is not null)
            SaveTabAs(ActiveTab);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Undo_Click(object sender, RoutedEventArgs e) => ActiveTab?.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is { UsesSimpleEditor: true })
            return;
        ActiveTab?.Redo();
    }
    private void Cut_Click(object sender, RoutedEventArgs e)
    {
        if (TryEditFocusedTextInput(static editor => editor.Cut()))
            return;
        ActiveTab?.Cut();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (TryEditFocusedTextInput(static editor => editor.Copy()))
            return;
        ActiveTab?.Copy();
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (TryEditFocusedTextInput(static editor => editor.Paste()))
            return;
        ActiveTab?.Paste();
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        var selected = ActiveTab?.SelectedText;
        FindBar.Show(string.IsNullOrWhiteSpace(selected) ? null : selected);
    }

    private void FindReplace_Click(object sender, RoutedEventArgs e)
    {
        if (_findReplaceDialog is null)
        {
            _findReplaceDialog = new FindReplaceDialog { Owner = this };
            _findReplaceDialog.FindNextRequested += (_, _) => FindNext();
            _findReplaceDialog.ReplaceRequested += (_, _) => ReplaceCurrent();
            _findReplaceDialog.ReplaceAllRequested += (_, _) => ReplaceAll();
        }

        var selected = ActiveTab?.SelectedText;
        if (!string.IsNullOrWhiteSpace(selected))
            _findReplaceDialog.SetFindText(selected);
        _findReplaceDialog.Show();
        _findReplaceDialog.Activate();
    }

    private void ReplaceCurrent()
    {
        var tab = ActiveTab;
        if (tab is null || _findReplaceDialog is null)
            return;

        var query = _findReplaceDialog.FindText;
        if (string.IsNullOrEmpty(query))
            return;

        var options = _findReplaceDialog.SearchOptions;
        var start = tab.SelectionStart;
        var selectedLength = tab.SelectedText.Length;
        TextSearchMatch match;

        if (selectedLength > 0 &&
            EditorTextSearch.MatchesAt(tab.Text, query, start, selectedLength, options))
        {
            match = new TextSearchMatch(start, selectedLength);
        }
        else if (EditorTextSearch.FindNext(tab.Text, query, start, options, false, out match) < 0)
        {
            return;
        }

        tab.ReplaceText(match.Start, match.Length, _findReplaceDialog.ReplaceText);
        SelectMatch(tab, new TextSearchMatch(match.Start, _findReplaceDialog.ReplaceText.Length));
        SyncFindMatchesAfterEdit();
    }

    private void ReplaceAll()
    {
        var tab = ActiveTab;
        if (tab is null || _findReplaceDialog is null)
            return;

        var query = _findReplaceDialog.FindText;
        if (string.IsNullOrEmpty(query))
            return;

        var matches = EditorTextSearch.FindAll(tab.Text, query, _findReplaceDialog.SearchOptions);
        if (matches.Count == 0)
            return;

        for (var i = matches.Count - 1; i >= 0; i--)
            tab.ReplaceText(matches[i].Start, matches[i].Length, _findReplaceDialog.ReplaceText);

        SyncFindMatchesAfterEdit();
    }

    private void GoToLine_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        var dialog = new GoToLineDialog(tab.LineCount) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedLine is int line)
        {
            tab.GoToLine(line);
            tab.UpdateCurrentLineHighlight();
            UpdateStatusBar();
        }
    }

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.WordWrap = WordWrapItem.IsChecked;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void LineNumbers_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.ShowLineNumbers = LineNumbersItem.IsChecked;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void CurrentLine_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.HighlightCurrentLine = CurrentLineItem.IsChecked;
        EditorPreferences.Instance.Save();
        foreach (TabItem item in DocumentTabs.Items)
            if (item.Tag is EditorTab tab)
                tab.UpdateCurrentLineHighlight();
    }

    private void LightTheme_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.Theme = EditorThemeKind.Light;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.Theme = EditorThemeKind.Dark;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void SystemTheme_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.Theme = EditorThemeKind.System;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void SolarizedTheme_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.Theme = EditorThemeKind.Solarized;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void SepiaTheme_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.Theme = EditorThemeKind.Sepia;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PreferencesDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ApplyThemeToAllTabs();
            ReapplyAutoSaveToAllTabs();
        }
    }

    private void ReapplyAutoSaveToAllTabs()
    {
        _autoSave.StopAll();
        if (!EditorPreferences.Instance.AutoSaveEnabled)
            return;

        foreach (TabItem item in DocumentTabs.Items)
        {
            if (item.Tag is EditorTab tab)
                _autoSave.Attach(tab, Dispatcher);
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => NewUntitledTab();

    private void NextTab_Click(object sender, RoutedEventArgs e) => SelectRelativeTab(1);

    private void PreviousTab_Click(object sender, RoutedEventArgs e) => SelectRelativeTab(-1);

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is not null)
            CloseTab(ActiveTab);
    }

    private void ReopenTab_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _closedTabs.Pop();
        if (snapshot is null)
            return;

        if (snapshot.RtfDataBase64 is null && string.IsNullOrEmpty(snapshot.Content) &&
            !string.IsNullOrEmpty(snapshot.FilePath))
        {
            if (File.Exists(snapshot.FilePath))
            {
                OpenFile(snapshot.FilePath);
                return;
            }

            System.Windows.MessageBox.Show(this,
                $"The file no longer exists:\n{snapshot.FilePath}",
                "Reopen Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            OpenRecoveredEntry(
                snapshot.FilePath,
                snapshot.Content,
                snapshot.Format.ToString(),
                snapshot.RtfDataBase64,
                snapshot.EncodingName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this,
                $"Could not reopen the closed tab:\n{ex.Message}",
                "Reopen Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenWithEncoding_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All files (*.*)|*.*|Rich Text (*.rtf)|*.rtf|Plain Text (*.txt)|*.txt",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var encodingDialog = new EncodingDialog(forOpen: true) { Owner = this };
        if (encodingDialog.ShowDialog() != true || encodingDialog.SelectedEncoding is null)
            return;

        foreach (var file in dialog.FileNames)
            OpenFile(file, encodingDialog.SelectedEncoding);
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null || string.IsNullOrEmpty(tab.Document.FilePath) || !File.Exists(tab.Document.FilePath))
        {
            System.Windows.MessageBox.Show(this, "This document has no saved version on disk.", "TextPad",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(this,
            $"Revert \"{tab.Document.DisplayName}\" to the last saved version?",
            "TextPad", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            tab.ReloadFromDisk();
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Revert Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DocumentEncoding_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null || tab.IsRichText)
            return;

        var status = $"Detected line endings: {tab.Document.LineEnding}";
        var dialog = new EncodingDialog(forOpen: false, tab.Document.Encoding, tab.Document.LineEndingPolicy, status)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;

        var encodingChanged = dialog.SelectedEncoding is not null &&
                              dialog.SelectedEncoding != tab.Document.Encoding;
        var lineEndingChanged = dialog.SelectedLineEndingPolicy != tab.Document.LineEndingPolicy;

        if (dialog.SelectedEncoding is not null)
            tab.Document.Encoding = dialog.SelectedEncoding;
        tab.Document.LineEndingPolicy = dialog.SelectedLineEndingPolicy;

        if (encodingChanged || lineEndingChanged)
        {
            tab.Document.IsDirty = true;
            tab.RefreshTabTitle();
            RefreshTabBar();
        }

        UpdateStatusBar();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        DocumentExport.Print(this, tab.Document.DisplayName, tab.Text, tab.RichEditor);
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        var exportName = string.IsNullOrEmpty(tab.Document.FilePath)
            ? "Untitled"
            : Path.GetFileNameWithoutExtension(tab.Document.FilePath);
        DocumentExport.ExportPdf(this, exportName, tab.Text, tab.RichEditor);
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null)
            return;

        var exportName = string.IsNullOrEmpty(tab.Document.FilePath)
            ? "Untitled"
            : Path.GetFileNameWithoutExtension(tab.Document.FilePath);
        DocumentExport.ExportHtml(this, tab.RichEditor, exportName, tab.Text);
    }

    private void PasteMatchStyle_Click(object sender, RoutedEventArgs e) => ActiveTab?.PasteMatchStyle();
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (TryEditFocusedTextInput(static editor => editor.SelectAll()))
            return;
        ActiveTab?.SelectAll();
    }
    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => FindNext(backward: true);

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ActiveTab?.Zoom(1);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ActiveTab?.Zoom(-1);

    private void Invisibles_Click(object sender, RoutedEventArgs e)
    {
        EditorPreferences.Instance.ShowInvisibles = InvisiblesItem.IsChecked;
        EditorPreferences.Instance.Save();
        ApplyThemeToAllTabs();
    }

    private void SetSyntaxLanguage(SyntaxLanguage language)
    {
        ActiveTab?.SetSyntaxLanguage(language);
        UpdateStatusBar();
    }

    private void SyntaxAuto_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Auto);
    private void SyntaxPlain_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.PlainText);
    private void SyntaxCSharp_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.CSharp);
    private void SyntaxJavaScript_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.JavaScript);
    private void SyntaxPython_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Python);
    private void SyntaxHtml_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Html);
    private void SyntaxCss_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Css);
    private void SyntaxJson_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Json);
    private void SyntaxMarkdown_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Markdown);
    private void SyntaxShell_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Shell);
    private void SyntaxCpp_Click(object sender, RoutedEventArgs e) => SetSyntaxLanguage(SyntaxLanguage.Cpp);

    private void EnsureRichTextEditor()
    {
        var tab = ActiveTab;
        if (tab is null || tab.IsRichText || tab.RichEditor is not null)
            return;

        ConvertActiveTabToRichText(tab);
    }

    private void Bold_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.ToggleBold(editor);
    }

    private void Italic_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.ToggleItalic(editor);
    }

    private void Underline_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.ToggleUnderline(editor);
    }

    private void Strikethrough_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.ToggleStrikethrough(editor);
    }

    private void TextColor_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is not { } editor)
            return;

        var color = PickColor();
        if (color is not null)
            RichTextCommands.ApplyForeground(editor, color.Value);
    }

    private void HighlightColor_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is not { } editor)
            return;

        var color = PickColor();
        if (color is not null)
            RichTextCommands.ApplyBackground(editor, color.Value);
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.SetAlignment(editor, TextAlignment.Left);
    }

    private void AlignCenter_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.SetAlignment(editor, TextAlignment.Center);
    }

    private void AlignRight_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.SetAlignment(editor, TextAlignment.Right);
    }

    private void AlignJustify_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.SetAlignment(editor, TextAlignment.Justify);
    }

    private void IncreaseIndent_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.IncreaseIndent(editor);
    }

    private void DecreaseIndent_Click(object sender, RoutedEventArgs e)
    {
        EnsureRichTextEditor();
        if (ActiveTab?.RichEditor is { } editor)
            RichTextCommands.DecreaseIndent(editor);
    }

    private void MakeRichText_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null || tab.IsRichText)
            return;
        ConvertActiveTabToRichText(tab);
    }

    private void MakePlainText_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null || !tab.IsRichText)
            return;

        var result = System.Windows.MessageBox.Show(this,
            "Convert this document to plain text? Formatting will be removed.",
            "TextPad", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        ConvertActiveTabToPlainText(tab);
    }

    private void ConvertActiveTabToRichText(EditorTab tab)
    {
        var text = tab.Text;
        var doc = tab.Document;
        doc.Format = DocumentFormat.RichText;
        doc.IsDirty = true;
        doc.RtfData = RichTextHelper.BuildRtfFromPlainText(text);
        ReplaceTabEditor(tab, EditorTab.Create(doc));
    }

    private void ConvertActiveTabToPlainText(EditorTab tab)
    {
        var doc = tab.Document;
        doc.Format = DocumentFormat.PlainText;
        doc.PlainContent = tab.Text;
        doc.RtfData = null;
        doc.IsDirty = true;
        ReplaceTabEditor(tab, EditorTab.Create(doc, doc.PlainContent));
    }

    private void ReplaceTabEditor(EditorTab oldTab, EditorTab newTab)
    {
        var index = DocumentTabs.Items.IndexOf(oldTab.TabItem);
        DisposeTab(oldTab);
        newTab.TabItem.Tag = newTab;
        WireTabEvents(newTab);
        newTab.SetContextMenu(CreateEditorContextMenu());

        if (index >= 0)
            DocumentTabs.Items.Insert(index, newTab.TabItem);
        else
            DocumentTabs.Items.Add(newTab.TabItem);

        DocumentTabs.SelectedItem = newTab.TabItem;
        _autoSave.Attach(newTab, Dispatcher);
        if (newTab.Document.IsDirty)
            _autoSave.WriteSnapshot(newTab);
        newTab.Focus();
        RefreshTabBar();
        UpdateStatusBar();
    }

    private Color? PickColor()
    {
        var dialog = new ColorPickerDialog { Owner = this };
        return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
    }

    private void OnExternalFileChanged(EditorTab tab)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsTabOpen(tab))
                return;

            if (!_externalChangePrompts.Add(tab.Document.DocumentId))
                return;

            try
            {
                if (tab.Document.WasDeletedFromDisk())
                {
                    tab.Document.MarkFileMissingFromDisk();
                    tab.RefreshTabTitle();
                    tab.RefreshFileMonitoring();
                    System.Windows.MessageBox.Show(
                        this,
                        $"\"{tab.Document.DisplayName}\" was deleted or moved on disk.\n\nUse Save As to keep your changes.",
                        "TextPad",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!tab.Document.HasChangedOnDisk())
                    return;

                if (tab.Document.IsDirty)
                {
                    var conflict = System.Windows.MessageBox.Show(
                        this,
                        $"\"{tab.Document.DisplayName}\" was modified on disk while you have unsaved changes.\n\nReload from disk and discard your unsaved edits?",
                        "TextPad",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (conflict == MessageBoxResult.Yes)
                        ReloadTabFromDisk(tab);

                    return;
                }

                var result = System.Windows.MessageBox.Show(
                    this,
                    $"\"{tab.Document.DisplayName}\" was modified by another program. Reload?",
                    "TextPad",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    ReloadTabFromDisk(tab);
            }
            finally
            {
                _externalChangePrompts.Remove(tab.Document.DocumentId);
            }
        });
    }

    private void ReloadTabFromDisk(EditorTab tab)
    {
        try
        {
            tab.ReloadFromDisk();
            tab.NotifySavedToDisk();
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Reload Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e) => OpenHelp();

    private void OpenHelp()
    {
        if (!AppHelp.IsAvailable)
        {
            System.Windows.MessageBox.Show(
                this,
                $"The help file could not be found:\n{AppHelp.HelpFilePath}",
                "TextPad Help",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        OpenFile(AppHelp.HelpFilePath);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            this,
            $"TextPad {GetAppVersionString()}\nA lightweight 64-bit text editor for Windows.\nInspired by BBEdit and CotEditor.",
            "About TextPad",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string GetAppVersionString()
    {
        var asm = Assembly.GetExecutingAssembly();
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+')[0];

        var version = asm.GetName().Version;
        return version is null ? "1.5.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private ContextMenu CreateEditorContextMenu()
    {
        var menu = new ContextMenu
        {
            Style = (Style)Resources["EditorContextMenuStyle"]
        };
        menu.Opened += EditorContextMenu_Opened;

        menu.Items.Add(CreateContextMenuItem("Undo", "Ctrl+Z", "undo", Undo_Click));
        menu.Items.Add(CreateContextMenuItem("Redo", "Ctrl+Y", "redo", Redo_Click));
        menu.Items.Add(CreateContextSeparator());
        menu.Items.Add(CreateContextMenuItem("Cut", "Ctrl+X", "cut", Cut_Click));
        menu.Items.Add(CreateContextMenuItem("Copy", "Ctrl+C", "copy", Copy_Click));
        menu.Items.Add(CreateContextMenuItem("Paste", "Ctrl+V", "paste", Paste_Click));
        menu.Items.Add(CreateContextMenuItem("Paste and Match Style", "Ctrl+Shift+V", "pasteMatchStyle", PasteMatchStyle_Click));
        menu.Items.Add(CreateContextMenuItem("Select All", "Ctrl+A", "selectAll", SelectAll_Click));
        menu.Items.Add(CreateContextSeparator());
        menu.Items.Add(CreateContextMenuItem("Find…", "Ctrl+F", "find", Find_Click));
        menu.Items.Add(CreateContextMenuItem("Find and Replace…", "Ctrl+H", "findReplace", FindReplace_Click));
        menu.Items.Add(CreateContextMenuItem("Go to Line…", "Ctrl+L", "goToLine", GoToLine_Click));

        return menu;
    }

    private MenuItem CreateContextMenuItem(string header, string gesture, string tag, RoutedEventHandler click)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGestureText = gesture,
            Tag = tag,
            Style = (Style)Resources["EditorContextMenuItemStyle"]
        };
        item.Click += click;
        return item;
    }

    private Separator CreateContextSeparator() =>
        new() { Style = (Style)Resources["EditorContextMenuSeparatorStyle"] };

    private void EditorContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        var tab = GetTabFromContextMenu(menu);
        if (tab is not null && DocumentTabs.SelectedItem != tab.TabItem)
        {
            DocumentTabs.SelectedItem = tab.TabItem;
            tab.Focus();
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            var enabled = item.Tag switch
            {
                "redo" => tab is not null && !tab.UsesSimpleEditor,
                "cut" or "copy" => tab is not null && tab.SelectedText.Length > 0,
                "paste" or "pasteMatchStyle" => Clipboard.ContainsText(),
                "selectAll" => tab is not null && tab.TextLength > 0,
                "find" or "findReplace" or "goToLine" => tab is not null,
                _ => true
            };
            item.IsEnabled = enabled;
        }
    }

    private EditorTab? GetTabFromContextMenu(ContextMenu? menu)
    {
        if (menu?.PlacementTarget is not DependencyObject target)
            return ActiveTab;

        foreach (TabItem item in DocumentTabs.Items)
        {
            if (item.Tag is not EditorTab tab)
                continue;

            var current = target;
            while (current is not null)
            {
                if (ReferenceEquals(current, tab.View) ||
                    ReferenceEquals(current, tab.PlainEditor) ||
                    ReferenceEquals(current, tab.RichEditor))
                    return tab;

                current = VisualTreeHelper.GetParent(current);
            }
        }

        return ActiveTab;
    }

    private bool TryEditFocusedTextInput(Action<TextBoxBase> action)
    {
        if (Keyboard.FocusedElement is not TextBoxBase focused)
            return false;

        if (IsFocusInActiveEditor(focused))
            return false;

        action(focused);
        return true;
    }

    private bool IsFocusInActiveEditor(DependencyObject? focused = null)
    {
        focused ??= Keyboard.FocusedElement as DependencyObject;
        var tab = ActiveTab;
        if (focused is null || tab is null)
            return false;

        var view = tab.View;
        var current = focused;
        while (current is not null)
        {
            if (ReferenceEquals(current, view))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveTab is not null)
            ActiveTab.UpdateCurrentLineHighlight();
        RefreshTabBar();
        UpdateStatusBar();
    }

    private void FindBar_SearchChanged(object? sender, string query)
    {
        _pendingFindQuery = query;
        _findDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _findDebounceTimer.Stop();
        _findDebounceTimer.Tick -= OnFindDebounceTick;
        _findDebounceTimer.Tick += OnFindDebounceTick;
        _findDebounceTimer.Start();
    }

    private void OnFindDebounceTick(object? sender, EventArgs e)
    {
        _findDebounceTimer?.Stop();
        _findDebounceTimer!.Tick -= OnFindDebounceTick;

        var tab = ActiveTab;
        if (tab is null)
            return;

        _lastFindQuery = _pendingFindQuery;
        _lastFindOptions = new TextSearchOptions(FindBar.MatchCase);
        RefreshFindMatches(tab, _pendingFindQuery, _lastFindOptions);
    }

    private void FindBar_FindNextRequested(object? sender, EventArgs e) => FindNext();
    private void FindBar_FindPreviousRequested(object? sender, EventArgs e) => FindNext(backward: true);

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            await OpenFilesAsync(files.Where(File.Exists));
    }
}