using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TextPad.Dialogs;

public partial class ColorPickerDialog : Window
{
    private static readonly Color[] PresetColors =
    [
        Colors.Black, Colors.White, Colors.Red, Colors.Orange, Colors.Gold,
        Colors.Green, Colors.Teal, Colors.Blue, Colors.Purple, Colors.Magenta,
        Color.FromRgb(128, 128, 128), Color.FromRgb(192, 192, 192),
        Color.FromRgb(139, 69, 19), Color.FromRgb(255, 255, 224)
    ];

    public Color? SelectedColor { get; private set; }

    public ColorPickerDialog()
    {
        InitializeComponent();
        foreach (var color in PresetColors)
        {
            var button = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(3),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                Tag = color
            };
            button.Click += (_, _) =>
            {
                SelectedColor = color;
                DialogResult = true;
            };
            ColorPanel.Children.Add(button);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedColor is null)
            DialogResult = false;
    }
}