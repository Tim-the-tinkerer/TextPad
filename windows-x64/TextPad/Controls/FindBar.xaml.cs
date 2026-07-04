using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextPad.Models;

namespace TextPad.Controls;

public partial class FindBar : UserControl
{
    public event EventHandler<string>? SearchChanged;
    public event EventHandler? FindNextRequested;
    public event EventHandler? FindPreviousRequested;

    public bool MatchCase => CaseToggle.IsChecked == true;
    public string Query => SearchBox.Text;

    public FindBar()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => SearchChanged?.Invoke(this, SearchBox.Text);
    }

    public void Show(string? initialQuery = null)
    {
        Visibility = Visibility.Visible;
        if (!string.IsNullOrEmpty(initialQuery))
            SearchBox.Text = initialQuery;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        SearchBox.Text = string.Empty;
        MatchCountText.Text = string.Empty;
    }

    public void SetMatchCount(int current, int total)
    {
        MatchCountText.Text = total == 0 ? "No matches" : $"{current} of {total}";
    }

    public void ApplyTheme(EditorTheme theme)
    {
        Background = new SolidColorBrush(theme.TabSelectedBackground);
        Foreground = new SolidColorBrush(theme.TabText);
        MatchCountText.Foreground = new SolidColorBrush(theme.LineNumberText);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                FindPreviousRequested?.Invoke(this, EventArgs.Empty);
            else
                FindNextRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e) => FindNextRequested?.Invoke(this, EventArgs.Empty);
    private void Previous_Click(object sender, RoutedEventArgs e) => FindPreviousRequested?.Invoke(this, EventArgs.Empty);
}