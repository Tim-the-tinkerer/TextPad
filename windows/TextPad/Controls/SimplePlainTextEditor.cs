using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TextPad.Controls;

public sealed class SimplePlainTextEditor : IDisposable
{
    private readonly TextBox _textBox;
    private readonly Canvas _lineNumberCanvas;
    private readonly ScrollViewer _lineNumberScroller;
    private readonly Border _lineNumberBorder;
    private readonly Grid _host;
    private ScrollViewer? _textBoxScrollViewer;
    private readonly Dictionary<int, double> _lineTops = new();
    private CancellationTokenSource? _measureCts;
    private bool _showLineNumbers = true;
    private bool _lineNumberUpdateScheduled;
    private bool _isMeasuringLines;
    private int _measuredLineCount;
    private Color _lineNumberColor = Colors.Gray;

    public SimplePlainTextEditor()
    {
        _textBox = new TextBox
        {
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            AcceptsTab = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2)
        };

        _lineNumberCanvas = new Canvas { Width = 48 };

        _lineNumberScroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Content = _lineNumberCanvas
        };

        _lineNumberBorder = new Border
        {
            Padding = _textBox.Padding,
            Child = _lineNumberScroller
        };

        _host = new Grid();
        _host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_lineNumberBorder, 0);
        Grid.SetColumn(_textBox, 1);
        _host.Children.Add(_lineNumberBorder);
        _host.Children.Add(_textBox);

        Host = _host;

        _textBox.Loaded += OnTextBoxLoaded;
        _textBox.TextChanged += (_, _) => OnTextContentChanged();
        _textBox.SizeChanged += (_, _) => ScheduleLineNumberUpdate();
    }

    public Grid Host { get; }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            _showLineNumbers = value;
            ScheduleLineNumberUpdate();
        }
    }

    public event TextChangedEventHandler? TextChanged
    {
        add => _textBox.TextChanged += value;
        remove => _textBox.TextChanged -= value;
    }

    public event EventHandler? SelectionChanged
    {
        add
        {
            _textBox.SelectionChanged += OnSelectionChanged;
            _textBox.PreviewMouseLeftButtonUp += OnMouseUp;
            _textBox.PreviewKeyUp += OnKeyUp;
            _selectionChanged += value;
        }
        remove
        {
            _textBox.SelectionChanged -= OnSelectionChanged;
            _textBox.PreviewMouseLeftButtonUp -= OnMouseUp;
            _textBox.PreviewKeyUp -= OnKeyUp;
            _selectionChanged -= value;
        }
    }

    private event EventHandler? _selectionChanged;

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isMeasuringLines)
            _selectionChanged?.Invoke(sender, e);

        if (!_isMeasuringLines && _textBox.SelectionLength == 0 && _measuredLineCount < CountLogicalLines(_textBox.Text))
            ScheduleLinePositionMeasure();
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _selectionChanged?.Invoke(sender, e);

    private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e) =>
        _selectionChanged?.Invoke(sender, e);

    public string Text
    {
        get => _textBox.Text;
        set
        {
            _textBox.Text = value;
            OnTextContentChanged();
        }
    }

    public int TextLength => _textBox.Text.Length;

    public string SelectedText => _textBox.SelectedText;

    public int SelectionStart => _textBox.SelectionStart;

    public int LineCount => CountLogicalLines(_textBox.Text);

    public void Focus() => _textBox.Focus();

    public void SetContextMenu(ContextMenu? menu) => _textBox.ContextMenu = menu;

    public void SelectAll() => _textBox.SelectAll();

    public void Undo() => _textBox.Undo();

    public void Redo() { }

    public void Cut() => _textBox.Cut();

    public void Copy() => _textBox.Copy();

    public void Paste() => _textBox.Paste();

    public void Select(int start, int length)
    {
        _textBox.Select(start, Math.Max(0, length));
        _textBox.ScrollToLine(_textBox.GetLineIndexFromCharacterIndex(start));
    }

    public void ReplaceText(int start, int length, string replacement)
    {
        _textBox.Select(start, length);
        _textBox.SelectedText = replacement;
    }

    public void GoToLine(int line)
    {
        var target = Math.Clamp(line, 1, Math.Max(1, LineCount));
        var index = GetCharacterIndexForLogicalLine(_textBox.Text, target);
        if (index < 0)
            return;

        _textBox.Select(index, 0);
        _textBox.ScrollToLine(_textBox.GetLineIndexFromCharacterIndex(index));
    }

    public (int Line, int Column) GetCaretPosition() =>
        GetLogicalPosition(_textBox.Text, _textBox.SelectionStart);

    public void ApplyTheme(Color background, Color foreground, Color lineNumberText, double fontSize, Color selection)
    {
        _textBox.Background = new SolidColorBrush(background);
        _textBox.Foreground = new SolidColorBrush(foreground);
        _textBox.FontSize = fontSize;

        var selectionBrush = new SolidColorBrush(selection);
        selectionBrush.Freeze();
        var selectionForeground = new SolidColorBrush(foreground);
        selectionForeground.Freeze();
        _textBox.SelectionBrush = selectionBrush;
        _textBox.SelectionTextBrush = selectionForeground;
        _textBox.SelectionOpacity = 1.0;

        _lineNumberColor = lineNumberText;
        _lineNumberBorder.Background = new SolidColorBrush(background);
        _host.Background = new SolidColorBrush(background);
        ScheduleLineNumberUpdate();
    }

    public void Dispose()
    {
        _measureCts?.Cancel();
        _measureCts?.Dispose();
    }

    private void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        _textBoxScrollViewer = FindVisualChild<ScrollViewer>(_textBox);
        if (_textBoxScrollViewer is not null)
        {
            _textBoxScrollViewer.ScrollChanged += (_, e) =>
            {
                SyncLineNumberScroll(e.VerticalOffset);
                ScheduleLineNumberUpdate();
            };
        }

        OnTextContentChanged();
    }

    private void OnTextContentChanged()
    {
        _lineTops.Clear();
        _measuredLineCount = 0;
        ScheduleLineNumberUpdate();
        ScheduleLinePositionMeasure();
    }

    private void ScheduleLineNumberUpdate()
    {
        if (_lineNumberUpdateScheduled)
            return;

        _lineNumberUpdateScheduled = true;
        _textBox.Dispatcher.BeginInvoke(() =>
        {
            _lineNumberUpdateScheduled = false;
            UpdateLineNumbers();
        }, DispatcherPriority.Background);
    }

    private void ScheduleLinePositionMeasure()
    {
        _measureCts?.Cancel();
        _measureCts?.Dispose();
        _measureCts = new CancellationTokenSource();
        _ = MeasureLinePositionsAsync(_measureCts.Token);
    }

    private async Task MeasureLinePositionsAsync(CancellationToken token)
    {
        if (_textBoxScrollViewer is null)
            return;

        if (_textBox.SelectionLength > 0)
            return;

        var text = _textBox.Text;
        var lineCount = CountLogicalLines(text);
        if (lineCount <= 1)
        {
            _lineTops[1] = 0;
            _measuredLineCount = lineCount;
            UpdateCanvasHeight();
            UpdateLineNumbers();
            return;
        }

        var savedCaret = _textBox.SelectionStart;
        var savedAnchor = _textBox.SelectionLength;
        var savedScroll = _textBoxScrollViewer.VerticalOffset;
        _isMeasuringLines = true;

        try
        {
            for (var line = 1; line <= lineCount; line++)
            {
                token.ThrowIfCancellationRequested();

                if (_textBox.SelectionLength > 0)
                    return;

                var index = GetCharacterIndexForLogicalLine(text, line);
                _textBox.SelectionStart = index;
                _textBox.SelectionLength = 0;

                var visualLine = _textBox.GetLineIndexFromCharacterIndex(index);
                if (visualLine >= 0)
                    _textBox.ScrollToLine(visualLine);

                await _textBox.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);

                if (TryGetDocumentTop(index, out var documentTop))
                    _lineTops[line] = documentTop;

                if (line == 1 || line == lineCount || line % 4 == 0)
                    UpdateLineNumbers();
            }

            _measuredLineCount = lineCount;
            UpdateCanvasHeight();
            UpdateLineNumbers();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer measure pass.
        }
        finally
        {
            _isMeasuringLines = false;
            _textBox.SelectionStart = savedCaret;
            _textBox.SelectionLength = savedAnchor;
            _textBoxScrollViewer.ScrollToVerticalOffset(savedScroll);
            SyncLineNumberScroll(savedScroll);
            UpdateLineNumbers();
        }
    }

    private bool TryGetDocumentTop(int charIndex, out double documentTop)
    {
        documentTop = 0;
        if (_textBoxScrollViewer is null)
            return false;

        var rect = _textBox.GetRectFromCharacterIndex(charIndex);
        if (rect.IsEmpty)
            return false;

        documentTop = _textBoxScrollViewer.VerticalOffset + rect.Top;
        return true;
    }

    private void UpdateCanvasHeight()
    {
        if (_textBoxScrollViewer is null)
            return;

        _lineNumberCanvas.Height = _textBoxScrollViewer.ExtentHeight;
    }

    private void UpdateLineNumbers()
    {
        _lineNumberCanvas.Children.Clear();

        if (!_showLineNumbers)
        {
            _lineNumberBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _lineNumberBorder.Visibility = Visibility.Visible;
        UpdateCanvasHeight();

        var text = _textBox.Text;
        var lineCount = CountLogicalLines(text);
        var drawn = new HashSet<int>();

        for (var line = 1; line <= lineCount; line++)
        {
            if (!TryGetLineDocumentTop(text, line, out var top))
                continue;

            DrawLineNumber(line, top, drawn);
        }

        SyncLineNumberScroll(_textBoxScrollViewer?.VerticalOffset ?? 0);
    }

    private bool TryGetLineDocumentTop(string text, int line, out double documentTop)
    {
        documentTop = 0;
        if (_lineTops.TryGetValue(line, out documentTop))
            return true;

        var index = GetCharacterIndexForLogicalLine(text, line);
        return TryGetDocumentTop(index, out documentTop);
    }

    private void DrawLineNumber(int line, double documentTop, HashSet<int> drawn)
    {
        if (!drawn.Add(line))
            return;

        var label = new TextBlock
        {
            Text = line.ToString(),
            FontFamily = _textBox.FontFamily,
            FontSize = _textBox.FontSize,
            Foreground = new SolidColorBrush(_lineNumberColor),
            TextAlignment = TextAlignment.Right,
            Width = 40
        };

        Canvas.SetTop(label, documentTop);
        Canvas.SetLeft(label, 0);
        _lineNumberCanvas.Children.Add(label);
    }

    private void SyncLineNumberScroll(double verticalOffset)
    {
        if (Math.Abs(_lineNumberScroller.VerticalOffset - verticalOffset) > 0.5)
            _lineNumberScroller.ScrollToVerticalOffset(verticalOffset);
    }

    private static int CountLogicalLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        var count = 1;
        foreach (var ch in text)
        {
            if (ch == '\n')
                count++;
        }

        return count;
    }

    private static int GetCharacterIndexForLogicalLine(string text, int line)
    {
        if (line <= 1)
            return 0;

        var currentLine = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            currentLine++;
            if (currentLine == line)
                return Math.Min(i + 1, text.Length);
        }

        return text.Length;
    }

    private static (int Line, int Column) GetLogicalPosition(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var line = 1;
        var column = 1;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else if (text[i] != '\r')
            {
                column++;
            }
        }

        return (line, column);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}