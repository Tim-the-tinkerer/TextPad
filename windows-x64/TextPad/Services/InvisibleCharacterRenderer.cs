using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using TextPad.Models;

namespace TextPad.Services;

public sealed class InvisibleCharacterRenderer : IBackgroundRenderer
{
    private static readonly Typeface Typeface = new("Consolas");
    private Brush _brush = Brushes.Gray;

    public KnownLayer Layer => KnownLayer.Background;

    public void SetBrush(Brush brush) => _brush = brush;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!EditorPreferences.Instance.ShowInvisibles)
            return;

        var doc = textView.Document;
        if (doc is null)
            return;

        if (doc.TextLength > LargeFileSupport.LargeDocumentCharacterThreshold)
            return;

        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
            var text = doc.GetText(line);
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch is not (' ' or '\t'))
                    continue;

                var visualColumn = doc.GetLocation(line.Offset + i).Column;
                var pos = textView.GetVisualPosition(
                    new TextViewPosition(line.LineNumber, visualColumn),
                    VisualYPosition.LineMiddle);
                var glyph = ch == '\t' ? "→" : "·";
                var formatted = new FormattedText(
                    glyph,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface,
                    textView.DefaultLineHeight * 0.75,
                    _brush,
                    VisualTreeHelper.GetDpi(textView).PixelsPerDip);
                drawingContext.DrawText(formatted, new Point(pos.X, pos.Y - formatted.Height / 2));
            }
        }
    }
}