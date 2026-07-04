using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TextPad.Controls;

public partial class DocumentTabHeader : UserControl
{
    public event EventHandler? CloseRequested;

    private Color _closeHoverBackground;

    public DocumentTabHeader()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public void ApplyAppearance(bool selected, Color text, Color selectedText, Color accent, bool isDark)
    {
        var foreground = selected ? selectedText : text;
        TitleText.Foreground = new SolidColorBrush(foreground);
        TitleText.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        CloseButton.Foreground = new SolidColorBrush(foreground);
        CloseButton.Background = Brushes.Transparent;
        CloseButton.BorderThickness = new Thickness(0);
        _closeHoverBackground = isDark
            ? Color.FromArgb(51, 255, 255, 255)
            : Color.FromArgb(51, 0, 0, 0);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            e.Handled = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CloseButton_MouseEnter(object sender, MouseEventArgs e)
    {
        CloseButton.Background = new SolidColorBrush(_closeHoverBackground);
    }

    private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
    {
        CloseButton.Background = Brushes.Transparent;
    }
}