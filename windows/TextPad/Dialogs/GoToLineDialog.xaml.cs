using System.Windows;

namespace TextPad.Dialogs;

public partial class GoToLineDialog : Window
{
    private readonly int _maxLine;

    public int? SelectedLine { get; private set; }

    public GoToLineDialog(int maxLine)
    {
        _maxLine = Math.Max(1, maxLine);
        InitializeComponent();
        PromptText.Text = $"Line number (1–{_maxLine}):";
        LineBox.Focus();
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(LineBox.Text, out var line) && line >= 1 && line <= _maxLine)
        {
            SelectedLine = line;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(this, $"Enter a line number between 1 and {_maxLine}.", "Go to Line",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}