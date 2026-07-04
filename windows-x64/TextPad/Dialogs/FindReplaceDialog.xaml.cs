using System.Windows;
using TextPad.Services;

namespace TextPad.Dialogs;

public partial class FindReplaceDialog : Window
{
    public event EventHandler? FindNextRequested;
    public event EventHandler? ReplaceRequested;
    public event EventHandler? ReplaceAllRequested;

    public string FindText => FindBox.Text;
    public string ReplaceText => ReplaceBox.Text;
    public bool MatchCase => CaseCheck.IsChecked == true;
    public bool WholeWord => WholeWordCheck.IsChecked == true;
    public bool UseRegex => RegexCheck.IsChecked == true;
    public TextSearchOptions SearchOptions => new(MatchCase, WholeWord, UseRegex);

    public FindReplaceDialog()
    {
        InitializeComponent();
    }

    public void SetFindText(string text)
    {
        FindBox.Text = text;
        FindBox.SelectAll();
        FindBox.Focus();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNextRequested?.Invoke(this, EventArgs.Empty);
    private void Replace_Click(object sender, RoutedEventArgs e) => ReplaceRequested?.Invoke(this, EventArgs.Empty);
    private void ReplaceAll_Click(object sender, RoutedEventArgs e) => ReplaceAllRequested?.Invoke(this, EventArgs.Empty);
}