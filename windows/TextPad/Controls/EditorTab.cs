using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using TextPad.Models;
using TextPad.Services;

namespace TextPad.Controls;

public sealed class EditorTab : IDisposable
{
    public EditorDocument Document { get; }
    public bool IsRichText => Document.IsRichText;
    public FrameworkElement View { get; private set; }
    public TextEditor? PlainEditor { get; }
    public RichTextBox? RichEditor { get; }
    public TabItem TabItem { get; private set; } = null!;
    public DocumentTabHeader TabHeader { get; }
    public CurrentLineHighlighter? LineHighlighter { get; private set; }
    public InvisibleCharacterRenderer? InvisibleRenderer { get; private set; }
    private ContextMenu? _contextMenu;
    private readonly FileChangeMonitor _fileMonitor = new();
    private bool _suppressDirty;
    private Grid? _previewGrid;
    private WebBrowser? _previewBrowser;
    private GridSplitter? _previewSplitter;
    private ColumnDefinition? _editorColumn;
    private ColumnDefinition? _splitterColumn;
    private ColumnDefinition? _previewColumn;
    private DispatcherTimer? _previewTimer;
    private string? _pendingPreviewHtml;

    public bool IsDisposed { get; private set; }
    public bool IsMarkdownPreviewVisible { get; private set; }

    public bool IsMarkdownLanguage
    {
        get
        {
            var language = Document.SyntaxLanguage;
            if (language == SyntaxLanguage.Auto)
                language = SyntaxHighlighterSetup.DetectFromPath(Document.FilePath);
            return language == SyntaxLanguage.Markdown;
        }
    }

    public event EventHandler? ContentChanged;
    public event EventHandler? CaretMoved;
    public event EventHandler? ExternalFileChanged;

    public static EditorTab Create(EditorDocument document, string? plainTextOverride = null)
    {
        if (document.IsRichText && document.RtfData is not null)
            return new EditorTab(document, document.RtfData);

        if (plainTextOverride is not null)
            document.PlainContent = plainTextOverride;

        var payload = PlainTextOpenPayload.FromDocument(document);
        var tab = new EditorTab(payload.Document);
        tab.PopulatePlainContent(payload, async: false).GetAwaiter().GetResult();
        return tab;
    }

    public static async Task<EditorTab> CreateAsync(EditorDocument document, string? plainTextOverride = null)
    {
        if (document.IsRichText && document.RtfData is not null)
            return new EditorTab(document, document.RtfData);

        if (plainTextOverride is not null)
            document.PlainContent = plainTextOverride;

        var payload = await Task.Run(() => PlainTextOpenPayload.FromDocument(document));
        return await CreateFromPayloadAsync(payload);
    }

    public static async Task<EditorTab> CreateFromPayloadAsync(PlainTextOpenPayload payload)
    {
        var tab = CreateShell(payload.Document);
        await tab.PopulatePlainContentAsync(payload);
        return tab;
    }

    public static EditorTab CreateShell(EditorDocument document) => new(document);

    private EditorTab(EditorDocument document)
    {
        Document = document;
        PlainEditor = CreatePlainEditor();
        PlainEditor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument();
        PlainEditor.TextChanged += OnPlainTextChanged;
        PlainEditor.TextArea.Caret.PositionChanged += OnCaretMoved;
        PlainEditor.TextArea.PreviewKeyDown += OnPlainPreviewKeyDown;
        PlainEditor.TextArea.TextEntered += OnPlainTextEntered;

        View = PlainEditor;
        RichEditor = null;
        ApplyTheme();
        ApplyContextMenu();
        TabHeader = CreateTabHeader();
    }

    public async Task PopulatePlainContentAsync(PlainTextOpenPayload payload) =>
        await PopulatePlainContent(payload, async: true);

    private async Task PopulatePlainContent(PlainTextOpenPayload payload, bool async)
    {
        _suppressDirty = true;
        try
        {
            if (async)
                await LargeFileSupport.AttachPlainTextPayloadAsync(PlainEditor!, payload);
            else
                AttachPlainTextPayload(PlainEditor!, payload);
        }
        finally
        {
            _suppressDirty = false;
        }

        FinishPlainContentSetup(payload);
    }

    private static void AttachPlainTextPayload(TextEditor editor, PlainTextOpenPayload payload)
    {
        if (payload.TextDocument is null)
            return;

        LargeFileSupport.ApplyEditorContentSettings(
            editor, payload.WordWrap, payload.CharacterCount, payload.ForceWordWrap, payload.LogicalLineCount);
        payload.TextDocument.SetOwnerThread(Thread.CurrentThread);
        editor.Document = payload.TextDocument;
        editor.CaretOffset = 0;
        payload.TextDocument.UndoStack.ClearAll();
        payload.TextDocument.UndoStack.MarkAsOriginalFile();
    }

    private void FinishPlainContentSetup(PlainTextOpenPayload payload)
    {
        if (payload.CharacterCount <= LargeFileSupport.LargeDocumentCharacterThreshold)
        {
            LineHighlighter = new CurrentLineHighlighter(PlainEditor!.Document, CreateHighlightBrush());
            PlainEditor.TextArea.TextView.BackgroundRenderers.Add(LineHighlighter);
            InvisibleRenderer = new InvisibleCharacterRenderer();
            PlainEditor.TextArea.TextView.BackgroundRenderers.Add(InvisibleRenderer);

            PlainEditor.Dispatcher.BeginInvoke(
                ApplySyntaxHighlighting,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        StartFileMonitoring();
    }

    private EditorTab(EditorDocument document, byte[] rtfData)
    {
        Document = document;
        RichEditor = CreateRichEditor();
        _suppressDirty = true;
        try
        {
            try
            {
                RichTextHelper.LoadRtf(RichEditor, rtfData, applyTheme: false);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Unable to read RTF content: {ex.Message}", ex);
            }
        }
        finally
        {
            _suppressDirty = false;
        }

        RichEditor.TextChanged += OnRichTextChanged;
        RichEditor.SelectionChanged += OnRichSelectionChanged;

        View = RichEditor;
        PlainEditor = null;
        LineHighlighter = null;
        ApplyContextMenu();

        var theme = EditorPreferences.Instance.EffectiveTheme;
        RichEditor.Background = new SolidColorBrush(theme.Background);
        RichEditor.Foreground = new SolidColorBrush(theme.Text);
        RichEditor.Dispatcher.BeginInvoke(
            () =>
            {
                _suppressDirty = true;
                try
                {
                    RichTextHelper.ApplyTheme(RichEditor, EditorPreferences.Instance.EffectiveTheme);
                }
                finally
                {
                    _suppressDirty = false;
                    if (!Document.IsDirty && HasSavedFileOnDisk())
                        Document.IsDirty = false;
                }
            },
            System.Windows.Threading.DispatcherPriority.Background);

        if (!Document.IsDirty && HasSavedFileOnDisk())
            Document.IsDirty = false;

        StartFileMonitoring();
        TabHeader = CreateTabHeader();
    }

    private DocumentTabHeader CreateTabHeader()
    {
        var header = new DocumentTabHeader { Title = Document.TabTitle };
        TabItem = new TabItem { Header = header, Content = View };
        return header;
    }

    public string Text
    {
        get
        {
            if (IsRichText)
                return RichTextHelper.GetPlainText(RichEditor!);
            return PlainEditor!.Document.Text;
        }
    }

    public int TextLength
    {
        get
        {
            if (IsRichText)
                return RichTextHelper.GetCharacterCount(RichEditor!);
            return PlainEditor!.Document.TextLength;
        }
    }

    public string SelectedText
    {
        get
        {
            if (IsRichText)
                return RichTextHelper.GetSelectedText(RichEditor!);
            return PlainEditor!.SelectedText;
        }
    }

    public int SelectionStart
    {
        get
        {
            if (IsRichText)
                return RichTextHelper.GetSelectionStart(RichEditor!);
            return PlainEditor!.SelectionStart;
        }
    }

    public int LineCount
    {
        get
        {
            if (IsRichText)
                return RichTextHelper.GetLineCount(RichEditor!);
            return PlainEditor!.LineCount;
        }
    }

    public byte[] GetRtfBytes()
    {
        // Theme remapping mutates the live FlowDocument. Unedited files keep
        // the bytes that were loaded so Save does not bake the theme into RTF.
        if (!Document.IsDirty && Document.RtfData is { Length: > 0 } original)
            return original;
        return RichTextHelper.SaveRtf(RichEditor!);
    }

    public void Focus()
    {
        if (IsRichText)
            RichEditor!.Focus();
        else
            PlainEditor!.Focus();
    }

    public void SetContextMenu(ContextMenu menu)
    {
        _contextMenu = menu;
        ApplyContextMenu();
    }

    private void ApplyContextMenu()
    {
        if (_contextMenu is null)
            return;

        if (RichEditor is not null)
            RichEditor.ContextMenu = _contextMenu;
        else if (PlainEditor is not null)
            PlainEditor.ContextMenu = _contextMenu;
    }

    public void Undo()
    {
        if (IsRichText) RichEditor!.Undo();
        else PlainEditor!.Undo();
    }

    public void Redo()
    {
        if (IsRichText) RichEditor!.Redo();
        else PlainEditor!.Redo();
    }

    public void Cut()
    {
        if (IsRichText) RichEditor!.Cut();
        else PlainEditor!.Cut();
    }

    public void Copy()
    {
        if (IsRichText) RichEditor!.Copy();
        else PlainEditor!.Copy();
    }

    public void Paste()
    {
        if (IsRichText) RichEditor!.Paste();
        else PlainEditor!.Paste();
    }

    public void PasteMatchStyle()
    {
        if (IsRichText)
            RichTextCommands.PasteAndMatchStyle(RichEditor!);
        else if (Clipboard.ContainsText() && PlainEditor is not null)
        {
            var segment = PlainEditor.TextArea.Selection.SurroundingSegment;
            PlainEditor.Document.Replace(segment.Offset, segment.Length, Clipboard.GetText());
        }
    }

    public void SelectAll()
    {
        if (IsRichText)
            RichEditor!.SelectAll();
        else
            PlainEditor!.SelectAll();
    }

    public void Zoom(int delta)
    {
        var prefs = EditorPreferences.Instance;
        prefs.FontSize = Math.Clamp(prefs.FontSize + delta, 8, 72);
        ApplyPreferences();
    }

    public void SetSyntaxLanguage(SyntaxLanguage language)
    {
        Document.SyntaxLanguage = language;
        ApplySyntaxHighlighting();
    }

    public void RefreshFileMonitoring() => StartFileMonitoring();

    public void NotifySavedToDisk() => _fileMonitor.SuppressBriefly();

    public void ReloadFromDisk()
    {
        Document.ReloadFromDisk();
        _suppressDirty = true;
        try
        {
            if (Document.IsRichText && Document.RtfData is not null && RichEditor is not null)
            {
                try
                {
                    RichTextHelper.LoadRtf(RichEditor, Document.RtfData);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Unable to read RTF content: {ex.Message}", ex);
                }
            }
            else if (PlainEditor is not null)
                LargeFileSupport.LoadPlainText(PlainEditor, Document.PlainContent ?? string.Empty);
        }
        finally
        {
            _suppressDirty = false;
        }

        Document.IsDirty = false;
        Document.NoteSavedToDisk();
        RefreshTabTitle();
        ApplySyntaxHighlighting();
        StartFileMonitoring();
        if (IsMarkdownPreviewVisible)
            RefreshMarkdownPreview();
    }

    public void Select(int start, int length)
    {
        if (IsRichText)
            RichTextHelper.SelectRange(RichEditor!, start, length);
        else
        {
            PlainEditor!.Select(start, length);
            PlainEditor.TextArea.Caret.BringCaretToView();
        }
    }

    public void ReplaceText(int start, int length, string replacement)
    {
        if (IsRichText)
            RichTextHelper.ReplaceRange(RichEditor!, start, length, replacement);
        else
            PlainEditor!.Document.Replace(start, length, replacement);
    }

    public void GoToLine(int line)
    {
        if (IsRichText)
            RichTextHelper.GoToLine(RichEditor!, line);
        else
        {
            PlainEditor!.ScrollToLine(line);
            PlainEditor.TextArea.Caret.Line = line;
            PlainEditor.TextArea.Caret.Column = 1;
        }
    }

    public (int Line, int Column) GetCaretPosition()
    {
        if (IsRichText)
            return RichTextHelper.GetCaretPosition(RichEditor!);

        var caret = PlainEditor!.TextArea.Caret;
        return (caret.Line, caret.Column);
    }

    public void RefreshTabTitle() => TabHeader.Title = Document.TabTitle;

    public void ApplyPreferences()
    {
        var prefs = EditorPreferences.Instance;
        if (IsRichText)
        {
            // Keep the RTF font table. Assigning FontFamily/FontSize here
            // replaced named faces such as Interlac Unicode with Segoe UI.
            _suppressDirty = true;
            try
            {
                ApplyTheme();
            }
            finally
            {
                _suppressDirty = false;
            }

            RefreshMarkdownPreviewIfVisible();
            return;
        }

        PlainEditor!.ShowLineNumbers = LargeFileSupport.ShouldShowLineNumbers(
            PlainEditor.Document.TextLength,
            LargeFileSupport.CountLogicalLines(PlainEditor.Document.Text));
        PlainEditor.WordWrap = prefs.WordWrap;
        PlainEditor.FontFamily = BundledFonts.Resolve(prefs.FontFamily);
        PlainEditor.FontSize = prefs.FontSize;
        PlainEditor.Options.IndentationSize = prefs.TabWidth;
        ApplyTheme();
        ApplySyntaxHighlighting();
        RefreshMarkdownPreviewIfVisible();
    }

    public void ApplySyntaxHighlighting()
    {
        if (IsRichText || PlainEditor is null)
            return;

        if (PlainEditor.Text.Length > LargeFileSupport.LargeDocumentCharacterThreshold)
        {
            PlainEditor.SyntaxHighlighting = null;
            return;
        }

        var definition = SyntaxHighlighterSetup.ForDocument(Document);
        if (PlainEditor.SyntaxHighlighting == definition)
            PlainEditor.SyntaxHighlighting = null;
        PlainEditor.SyntaxHighlighting = definition;
    }

    public void UpdateCurrentLineHighlight()
    {
        if (IsRichText || LineHighlighter is null || PlainEditor is null)
            return;

        if (!EditorPreferences.Instance.HighlightCurrentLine)
        {
            LineHighlighter.SetLine(-1);
            PlainEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            return;
        }

        LineHighlighter.SetLine(PlainEditor.TextArea.Caret.Line);
        PlainEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    private TextEditor CreatePlainEditor() => new()
    {
        ShowLineNumbers = EditorPreferences.Instance.ShowLineNumbers,
        WordWrap = EditorPreferences.Instance.WordWrap,
        FontFamily = BundledFonts.Resolve(EditorPreferences.Instance.FontFamily),
        FontSize = EditorPreferences.Instance.FontSize,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Options =
        {
            EnableHyperlinks = true,
            EnableEmailHyperlinks = true,
            ConvertTabsToSpaces = true,
            IndentationSize = EditorPreferences.Instance.TabWidth
        }
    };

    private RichTextBox CreateRichEditor() => new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontSize = EditorPreferences.Instance.FontSize,
        FontFamily = new FontFamily("Segoe UI"),
        AcceptsReturn = true,
        AcceptsTab = true,
        IsDocumentEnabled = true
    };

    private void ApplyTheme()
    {
        var theme = EditorPreferences.Instance.EffectiveTheme;
        if (IsRichText && RichEditor is not null)
        {
            RichTextHelper.ApplyTheme(RichEditor, theme);
            return;
        }

        if (PlainEditor is null)
            return;

        PlainEditor.Background = new SolidColorBrush(theme.Background);
        PlainEditor.Foreground = new SolidColorBrush(theme.Text);
        PlainEditor.LineNumbersForeground = new SolidColorBrush(theme.LineNumberText);

        var selectionBrush = new SolidColorBrush(theme.Selection);
        selectionBrush.Freeze();
        var selectionForeground = new SolidColorBrush(theme.Text);
        selectionForeground.Freeze();
        PlainEditor.TextArea.SelectionBrush = selectionBrush;
        PlainEditor.TextArea.SelectionForeground = selectionForeground;

        if (LineHighlighter is not null)
        {
            var brush = CreateHighlightBrush();
            brush.Freeze();
            LineHighlighter.SetBrush(brush);
            PlainEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }

        if (InvisibleRenderer is not null)
        {
            InvisibleRenderer.SetBrush(new SolidColorBrush(theme.LineNumberText));
            PlainEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
    }

    private void StartFileMonitoring()
    {
        _fileMonitor.Stop();
        if (string.IsNullOrEmpty(Document.FilePath) || !File.Exists(Document.FilePath))
            return;

        _fileMonitor.Watch(
            Document.FilePath,
            () => ExternalFileChanged?.Invoke(this, EventArgs.Empty),
            View.Dispatcher);
    }

    private static SolidColorBrush CreateHighlightBrush() =>
        new(EditorPreferences.Instance.EffectiveTheme.CurrentLineHighlight);

    private bool HasSavedFileOnDisk() =>
        !string.IsNullOrEmpty(Document.FilePath) && File.Exists(Document.FilePath);

    private void MarkDirty()
    {
        if (_suppressDirty)
            return;

        if (!Document.IsDirty)
        {
            Document.IsDirty = true;
            RefreshTabTitle();
        }

        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlainTextChanged(object? sender, EventArgs e)
    {
        if (!_suppressDirty
            && PlainEditor is not null
            && PlainEditor.Document.TextLength <= LargeFileSupport.LargeDocumentCharacterThreshold)
        {
            Document.LineEnding = EditorDocument.DetectLineEndings(PlainEditor.Document.Text);
        }

        MarkDirty();
        ScheduleMarkdownPreview();
    }

    private void OnRichTextChanged(object? sender, TextChangedEventArgs e)
    {
        MarkDirty();
        ScheduleMarkdownPreview();
    }
    private void OnCaretMoved(object? sender, EventArgs e) => CaretMoved?.Invoke(this, EventArgs.Empty);
    private void OnRichSelectionChanged(object? sender, RoutedEventArgs e) => CaretMoved?.Invoke(this, EventArgs.Empty);

    private void OnPlainPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (PlainEditor is null)
            return;

        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
        {
            PlainTextEditing.InsertTab(PlainEditor);
            e.Handled = true;
            return;
        }

        PlainTextEditing.HandlePreviewKeyDown(PlainEditor, e, Document.LineEnding);
    }

    private void OnPlainTextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (PlainEditor is null)
            return;

        if (PlainTextEditing.HandleTextInput(PlainEditor, e))
            e.Handled = true;
    }

    public void ToggleMarkdownPreview()
    {
        IsMarkdownPreviewVisible = !IsMarkdownPreviewVisible;
        EnsurePreviewSplit();
        ApplyPreviewVisibility();
        if (IsMarkdownPreviewVisible)
            RefreshMarkdownPreview();
        else
            _previewTimer?.Stop();

        View.Dispatcher.BeginInvoke(Focus, DispatcherPriority.Input);
    }

    private void EnsurePreviewSplit()
    {
        if (_previewGrid is not null)
            return;

        var editor = View;
        DetachFromParent(editor);

        var theme = EditorPreferences.Instance.EffectiveTheme;
        _editorColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        _splitterColumn = new ColumnDefinition { Width = new GridLength(0) };
        _previewColumn = new ColumnDefinition { Width = new GridLength(0) };
        _previewSplitter = new GridSplitter
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(theme.LineNumberText),
            Visibility = Visibility.Collapsed
        };
        _previewBrowser = new WebBrowser { Visibility = Visibility.Collapsed };
        editor.MinWidth = 160;
        Grid.SetColumn(editor, 0);
        Grid.SetColumn(_previewSplitter, 1);
        Grid.SetColumn(_previewBrowser, 2);

        _previewGrid = new Grid();
        _previewGrid.ColumnDefinitions.Add(_editorColumn);
        _previewGrid.ColumnDefinitions.Add(_splitterColumn);
        _previewGrid.ColumnDefinitions.Add(_previewColumn);
        _previewGrid.Children.Add(editor);
        _previewGrid.Children.Add(_previewSplitter);
        _previewGrid.Children.Add(_previewBrowser);

        View = _previewGrid;
        if (TabItem is not null)
            TabItem.Content = _previewGrid;
    }

    private static void DetachFromParent(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case ContentControl control when ReferenceEquals(control.Content, element):
                control.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
        }
    }

    private void ApplyPreviewVisibility()
    {
        if (_editorColumn is null || _splitterColumn is null || _previewColumn is null ||
            _previewSplitter is null || _previewBrowser is null)
            return;

        if (IsMarkdownPreviewVisible)
        {
            _editorColumn.Width = new GridLength(1.4, GridUnitType.Star);
            _splitterColumn.Width = new GridLength(5);
            _previewColumn.Width = new GridLength(1, GridUnitType.Star);
            _previewSplitter.Visibility = Visibility.Visible;
            _previewBrowser.Visibility = Visibility.Visible;
        }
        else
        {
            _editorColumn.Width = new GridLength(1, GridUnitType.Star);
            _splitterColumn.Width = new GridLength(0);
            _previewColumn.Width = new GridLength(0);
            _previewSplitter.Visibility = Visibility.Collapsed;
            _previewBrowser.Visibility = Visibility.Collapsed;
        }
    }

    private void ScheduleMarkdownPreview()
    {
        if (!IsMarkdownPreviewVisible || _suppressDirty)
            return;

        _previewTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _previewTimer.Stop();
        _previewTimer.Tick -= OnPreviewTick;
        _previewTimer.Tick += OnPreviewTick;
        _previewTimer.Start();
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        _previewTimer?.Stop();
        RefreshMarkdownPreview();
    }

    private void RefreshMarkdownPreviewIfVisible()
    {
        UpdatePreviewChrome();
        if (IsMarkdownPreviewVisible)
            RefreshMarkdownPreview();
    }

    private void UpdatePreviewChrome()
    {
        if (_previewSplitter is null)
            return;

        _previewSplitter.Background = new SolidColorBrush(EditorPreferences.Instance.EffectiveTheme.LineNumberText);
    }

    private void RefreshMarkdownPreview()
    {
        if (!IsMarkdownPreviewVisible || _previewBrowser is null)
            return;

        var title = string.IsNullOrEmpty(Document.FilePath)
            ? "Preview"
            : Path.GetFileNameWithoutExtension(Document.FilePath);
        var style = PreviewPageStyle();
        var source = Text;
        var html = source.Length > LargeFileSupport.LargeDocumentCharacterThreshold
            ? Markdown.HtmlDocument("Preview is not available for a document this large.", title, style)
            : Markdown.HtmlDocument(source, title, style);
        NavigatePreview(html);
    }

    private void NavigatePreview(string html)
    {
        if (_previewBrowser is null)
            return;

        if (!_previewBrowser.IsLoaded)
        {
            _pendingPreviewHtml = html;
            _previewBrowser.Loaded -= OnPreviewBrowserLoaded;
            _previewBrowser.Loaded += OnPreviewBrowserLoaded;
            return;
        }

        try
        {
            _previewBrowser.NavigateToString(html);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            _pendingPreviewHtml = html;
            _previewBrowser.Loaded -= OnPreviewBrowserLoaded;
            _previewBrowser.Loaded += OnPreviewBrowserLoaded;
        }
    }

    private void OnPreviewBrowserLoaded(object sender, RoutedEventArgs e)
    {
        if (_previewBrowser is null || _pendingPreviewHtml is null)
            return;

        var html = _pendingPreviewHtml;
        _pendingPreviewHtml = null;
        _previewBrowser.NavigateToString(html);
    }

    private static Markdown.PageStyle PreviewPageStyle()
    {
        var theme = EditorPreferences.Instance.EffectiveTheme;
        var (code, accent, border) = theme.Kind switch
        {
            EditorThemeKind.Dark => ("#2A2A30", "#8AB4FF", "#3A3A42"),
            EditorThemeKind.Solarized => ("#073642", "#2AA198", "#586E75"),
            EditorThemeKind.Sepia => ("#EFE6D6", "#8A5A12", "#D9CDB8"),
            _ => ("#F3F4F6", "#0B57D0", "#E4E4EA")
        };
        return new Markdown.PageStyle(
            Hex(theme.Background),
            Hex(theme.Text),
            Hex(theme.LineNumberText),
            code,
            accent,
            border);
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        _previewTimer?.Stop();
        _fileMonitor.Dispose();
        if (PlainEditor is not null)
        {
            PlainEditor.TextChanged -= OnPlainTextChanged;
            PlainEditor.TextArea.Caret.PositionChanged -= OnCaretMoved;
            PlainEditor.TextArea.PreviewKeyDown -= OnPlainPreviewKeyDown;
            PlainEditor.TextArea.TextEntered -= OnPlainTextEntered;
            if (LineHighlighter is not null)
                PlainEditor.TextArea.TextView.BackgroundRenderers.Remove(LineHighlighter);
            if (InvisibleRenderer is not null)
                PlainEditor.TextArea.TextView.BackgroundRenderers.Remove(InvisibleRenderer);
        }
        if (RichEditor is not null)
        {
            RichEditor.TextChanged -= OnRichTextChanged;
            RichEditor.SelectionChanged -= OnRichSelectionChanged;
        }
    }
}