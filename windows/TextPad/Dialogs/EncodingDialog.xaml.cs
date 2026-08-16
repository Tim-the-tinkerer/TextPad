using System.Text;
using System.Windows;
using TextPad.Models;
using TextPad.Services;

namespace TextPad.Dialogs;

public partial class EncodingDialog : Window
{
    public Encoding? SelectedEncoding { get; private set; }
    public LineEndingPolicy SelectedLineEndingPolicy { get; private set; }

    public EncodingDialog(bool forOpen, Encoding? currentEncoding = null, LineEndingPolicy? currentPolicy = null, string? status = null)
    {
        InitializeComponent();

        Title = forOpen ? "Open with Encoding" : "Document Encoding";
        ApplyButton.Content = forOpen ? "Open" : "Apply";
        StatusText.Text = status ?? (forOpen ? "Choose how to decode the file." : string.Empty);

        foreach (var item in DocumentEncoding.Supported)
            EncodingBox.Items.Add(item.name);

        LineEndingBox.ItemsSource = Enum.GetValues<LineEndingPolicy>();

        var encoding = currentEncoding ?? EditorPreferences.Instance.DefaultEncoding;
        var encodingName = DocumentEncoding.NameFor(encoding);
        EncodingBox.SelectedItem = DocumentEncoding.Supported
            .Select(s => s.name)
            .FirstOrDefault(n => n == encodingName) ?? "UTF-8";

        LineEndingBox.SelectedItem = currentPolicy ?? EditorPreferences.Instance.LineEndingPolicy;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (EncodingBox.SelectedItem is not string name)
            return;

        foreach (var item in DocumentEncoding.Supported)
        {
            if (item.name == name)
            {
                SelectedEncoding = item.encoding;
                break;
            }
        }

        if (LineEndingBox.SelectedItem is LineEndingPolicy policy)
            SelectedLineEndingPolicy = policy;

        DialogResult = true;
    }
}