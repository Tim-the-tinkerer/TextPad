using System.Windows;
using System.Windows.Controls;
using TextPad.Models;

namespace TextPad.Dialogs;

public partial class PreferencesDialog : Window
{
    public PreferencesDialog()
    {
        InitializeComponent();
        LoadOptions();
    }

    private void LoadOptions()
    {
        var prefs = EditorPreferences.Instance;

        ThemeBox.ItemsSource = Enum.GetValues<EditorThemeKind>();
        ThemeBox.SelectedItem = prefs.Theme;

        FontBox.ItemsSource = new[] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };
        FontBox.SelectedItem = prefs.FontFamily;

        FontSizeBox.ItemsSource = Enumerable.Range(8, 25).Select(i => (object)i).ToList();
        FontSizeBox.SelectedItem = prefs.FontSize;

        TabWidthBox.ItemsSource = new object[] { 2, 4, 8 };
        TabWidthBox.SelectedItem = prefs.TabWidth;

        LineEndingBox.ItemsSource = Enum.GetValues<LineEndingPolicy>();
        LineEndingBox.SelectedItem = prefs.LineEndingPolicy;

        WordWrapCheck.IsChecked = prefs.WordWrap;
        LineNumbersCheck.IsChecked = prefs.ShowLineNumbers;
        CurrentLineCheck.IsChecked = prefs.HighlightCurrentLine;
        InvisiblesCheck.IsChecked = prefs.ShowInvisibles;
        AutoSaveCheck.IsChecked = prefs.AutoSaveEnabled;
        AutoSaveIntervalBox.ItemsSource = new object[] { 15, 30, 60, 120, 300, 600 };
        AutoSaveIntervalBox.SelectedItem = prefs.AutoSaveIntervalSeconds;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var prefs = EditorPreferences.Instance;
        if (ThemeBox.SelectedItem is EditorThemeKind theme)
            prefs.Theme = theme;
        if (FontBox.SelectedItem is string font)
            prefs.FontFamily = font;
        if (FontSizeBox.SelectedItem is int size)
            prefs.FontSize = size;
        if (TabWidthBox.SelectedItem is int tabWidth)
            prefs.TabWidth = tabWidth;
        if (LineEndingBox.SelectedItem is LineEndingPolicy lineEnding)
            prefs.LineEndingPolicy = lineEnding;

        prefs.WordWrap = WordWrapCheck.IsChecked == true;
        prefs.ShowLineNumbers = LineNumbersCheck.IsChecked == true;
        prefs.HighlightCurrentLine = CurrentLineCheck.IsChecked == true;
        prefs.ShowInvisibles = InvisiblesCheck.IsChecked == true;
        prefs.AutoSaveEnabled = AutoSaveCheck.IsChecked == true;
        if (AutoSaveIntervalBox.SelectedItem is int interval)
            prefs.AutoSaveIntervalSeconds = interval;
        prefs.Save();

        DialogResult = true;
    }
}