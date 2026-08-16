using System.IO;
using System.Net;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using TextPad.Models;

namespace TextPad.Services;

public static class DocumentExport
{
    private static readonly Size PageSize = new(816, 1056); // 8.5" x 11" at 96 DPI

    /// <summary>
    /// Rasterize FlowDocument pages at print quality. 96 DPI produces soft/blurry PDFs;
    /// 300 DPI keeps text crisp when zoomed or printed.
    /// </summary>
    private const double PdfRenderDpi = 300;

    private static int _fontsConfigured;

    public static void Print(FrameworkElement owner, string title, string plainText, RichTextBox? richEditor = null)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;

        var flowDocument = BuildPrintDocument(title, plainText, richEditor);
        dialog.PrintDocument(((IDocumentPaginatorSource)flowDocument).DocumentPaginator, title);
    }

    public static bool ExportHtml(
        FrameworkElement owner,
        RichTextBox? editor,
        string title,
        string plainText)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Document" : title;
        var dialog = new SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html|All files (*.*)|*.*",
            FileName = safeTitle + ".html"
        };
        if (dialog.ShowDialog() != true)
            return false;

        try
        {
            var html = editor is not null
                ? BuildHtmlFromRichText(editor, safeTitle)
                : BuildHtmlFromPlainText(plainText, safeTitle);

            AtomicFileWriter.WriteAllBytes(
                dialog.FileName,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(html));
            return true;
        }
        catch (Exception ex)
        {
            var window = owner as Window ?? Window.GetWindow(owner);
            System.Windows.MessageBox.Show(
                window,
                $"Could not export HTML:\n{ex.Message}",
                "TextPad",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private static string BuildHtmlFromRichText(RichTextBox editor, string title)
    {
        var exportBox = new RichTextBox();
        CopyRtf(editor, exportBox);
        RichTextHelper.ApplyExportTheme(exportBox);

        var body = RichTextHtmlExporter.Export(exportBox.Document);
        return WrapHtmlDocument(body, title);
    }

    private static string BuildHtmlFromPlainText(string text, string title)
    {
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var body = $"<pre>{WebUtility.HtmlEncode(normalized)}</pre>";
        return WrapHtmlDocument(body, title);
    }

    private static string WrapHtmlDocument(string body, string title)
    {
        var escapedTitle = WebUtility.HtmlEncode(title);
        if (body.Contains("<html", StringComparison.OrdinalIgnoreCase))
            return body;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>{{escapedTitle}}</title>
            <style>
            body { background:#fff; color:#0d0d12; margin:1.5em; font-family:Segoe UI,Helvetica,Arial,sans-serif; line-height:1.4; }
            a { color:#0066cc; }
            pre { white-space:pre-wrap; word-wrap:break-word; }
            </style>
            </head>
            <body>
            {{body}}
            </body>
            </html>
            """;
    }

    public static bool ExportPdf(
        FrameworkElement owner,
        string title,
        string plainText,
        RichTextBox? richEditor = null)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Untitled" : title;
        var dialog = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf|All files (*.*)|*.*",
            FileName = safeTitle + ".pdf"
        };
        if (dialog.ShowDialog() != true)
            return false;

        try
        {
            if (richEditor is not null)
            {
                try
                {
                    ExportRichTextToPdf(richEditor, dialog.FileName, safeTitle);
                    return true;
                }
                catch (Exception richExportError)
                {
                    var window = owner as Window ?? Window.GetWindow(owner);
                    System.Windows.MessageBox.Show(
                        window,
                        $"Rich text PDF export failed. Saving as plain text instead.\n\n{richExportError.Message}",
                        "TextPad",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            // Plain text: draw real PDF text (vector fonts) — sharp at any zoom.
            // FlowDocument rasterization at 96 DPI produced blurry, distorted pages.
            SaveTextAsPdf(plainText ?? string.Empty, dialog.FileName, safeTitle);
            return true;
        }
        catch (Exception ex)
        {
            var window = owner as Window ?? Window.GetWindow(owner);
            System.Windows.MessageBox.Show(
                window,
                $"Could not export PDF:\n{ex.Message}",
                "TextPad",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    private static FlowDocument BuildPrintDocument(string title, string plainText, RichTextBox? richEditor)
    {
        if (richEditor is not null)
        {
            var exportBox = new RichTextBox { Width = PageSize.Width, FontSize = richEditor.FontSize };
            CopyRtf(richEditor, exportBox);
            RichTextHelper.ApplyExportTheme(exportBox);
            return PrepareForPagination(exportBox.Document);
        }

        return PrepareForPagination(BuildPlainFlowDocument(title, plainText, includeTitle: true));
    }

    private static void ExportRichTextToPdf(RichTextBox source, string filePath, string title)
    {
        var exportBox = new RichTextBox
        {
            Width = PageSize.Width,
            FontSize = source.FontSize,
            Background = Brushes.White
        };

        CopyRtf(source, exportBox);
        RichTextHelper.ApplyExportTheme(exportBox);

        var pageViewer = new FlowDocumentPageViewer { Document = exportBox.Document };
        var host = new Window
        {
            Content = pageViewer,
            Tag = exportBox,
            Width = PageSize.Width,
            Height = PageSize.Height,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };

        try
        {
            host.Show();
            PumpDispatcher();
            host.UpdateLayout();
            pageViewer.UpdateLayout();
            ExportFlowDocumentToPdf(exportBox.Document, filePath, title, pageViewer);
        }
        finally
        {
            host.Close();
        }
    }

    private static void CopyRtf(RichTextBox from, RichTextBox to)
    {
        using var stream = new MemoryStream();
        new TextRange(from.Document.ContentStart, from.Document.ContentEnd).Save(stream, DataFormats.Rtf);
        stream.Position = 0;
        new TextRange(to.Document.ContentStart, to.Document.ContentEnd).Load(stream, DataFormats.Rtf);
    }

    private static FlowDocument BuildPlainFlowDocument(string title, string text, bool includeTitle)
    {
        var prefs = EditorPreferences.Instance;
        var document = new FlowDocument
        {
            FontFamily = BundledFonts.Resolve(prefs.FontFamily),
            FontSize = prefs.FontSize,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PagePadding = new Thickness(48)
        };

        if (includeTitle && !string.IsNullOrWhiteSpace(title))
        {
            document.Blocks.Add(new Paragraph(new Run(title))
            {
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = prefs.FontSize * 1.2
        };

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrEmpty(normalized))
        {
            paragraph.Inlines.Add(new Run(" "));
        }
        else
        {
            var lines = normalized.Split('\n');
            var tabSpaces = new string(' ', prefs.TabWidth);
            for (var i = 0; i < lines.Length; i++)
            {
                paragraph.Inlines.Add(new Run(lines[i].Replace("\t", tabSpaces)));
                if (i < lines.Length - 1)
                    paragraph.Inlines.Add(new LineBreak());
            }
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    private static bool TryCloneFlowDocument(FlowDocument source, out FlowDocument clone)
    {
        clone = null!;

        if (TryCloneWithFormat(source, DataFormats.XamlPackage, out clone))
            return true;

        if (TryCloneWithFormat(source, DataFormats.Xaml, out clone))
            return true;

        if (TryCloneViaRtf(source, out clone))
            return true;

        return false;
    }

    private static bool TryCloneWithFormat(FlowDocument source, string format, out FlowDocument clone)
    {
        clone = null!;
        try
        {
            var range = new TextRange(source.ContentStart, source.ContentEnd);
            using var stream = new MemoryStream();
            range.Save(stream, format);
            if (stream.Length == 0)
                return false;

            stream.Position = 0;
            clone = (FlowDocument)System.Windows.Markup.XamlReader.Load(stream);
            return clone.Blocks.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCloneViaRtf(FlowDocument source, out FlowDocument clone)
    {
        clone = null!;
        try
        {
            var range = new TextRange(source.ContentStart, source.ContentEnd);
            using var rtfStream = new MemoryStream();
            range.Save(rtfStream, DataFormats.Rtf);
            if (rtfStream.Length == 0)
                return false;

            rtfStream.Position = 0;
            var host = new RichTextBox
            {
                Width = PageSize.Width,
                Visibility = Visibility.Hidden
            };

            var loadRange = new TextRange(host.Document.ContentStart, host.Document.ContentEnd);
            loadRange.Load(rtfStream, DataFormats.Rtf);

            return TryCloneWithFormat(host.Document, DataFormats.XamlPackage, out clone)
                   || TryCloneWithFormat(host.Document, DataFormats.Xaml, out clone);
        }
        catch
        {
            return false;
        }
    }

    private static FlowDocument PrepareForPagination(FlowDocument document)
    {
        document.PageWidth = PageSize.Width;
        document.PageHeight = PageSize.Height;
        if (document.PagePadding == new Thickness(0))
            document.PagePadding = new Thickness(48);
        document.Background = Brushes.White;
        if (document.Foreground is null)
            document.Foreground = Brushes.Black;
        document.ColumnWidth = PageSize.Width - document.PagePadding.Left - document.PagePadding.Right;
        if (document.Blocks.Count == 0)
            document.Blocks.Add(new Paragraph(new Run(" ")));
        return document;
    }

    private static void ExportFlowDocumentToPdf(
        FlowDocument document,
        string filePath,
        string title,
        FlowDocumentPageViewer? existingViewer = null)
    {
        PrepareForPagination(document);

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = PageSize;

        Window? host = null;
        FlowDocumentPageViewer? pageViewer = existingViewer;

        try
        {
            if (pageViewer is null)
            {
                pageViewer = new FlowDocumentPageViewer { Document = document };
                host = new Window
                {
                    Content = pageViewer,
                    Width = PageSize.Width,
                    Height = PageSize.Height,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Visibility = Visibility.Hidden
                };
                host.Show();
            }

            PumpDispatcher();
            pageViewer.UpdateLayout();

            var pageCount = ResolvePageCount(paginator);
            if (pageCount == 0)
                throw new InvalidOperationException("Document produced no pages.");

            var pdf = new PdfDocument();
            pdf.Info.Title = title;

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var documentPage = paginator.GetPage(pageIndex);
                try
                {
                    AddDocumentPageToPdf(pdf, documentPage);
                }
                finally
                {
                    if (documentPage is IDisposable disposable)
                        disposable.Dispose();
                }
            }

            AtomicFileWriter.SavePdf(pdf, filePath);
        }
        finally
        {
            host?.Close();
        }
    }

    private static void AddDocumentPageToPdf(PdfDocument pdf, DocumentPage documentPage)
    {
        // Scale the page visual up to print DPI so text stays sharp in the PDF viewer.
        var scale = PdfRenderDpi / 96.0;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(PageSize.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(PageSize.Height * scale));

        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, PageSize.Width, PageSize.Height));

            // DocumentPage.Visual is already laid out in 96-DPI DIPs; paint it via a brush
            // so we can scale without re-parenting the visual tree.
            var pageBrush = new VisualBrush(documentPage.Visual)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                Viewbox = new Rect(0, 0, PageSize.Width, PageSize.Height),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, PageSize.Width, PageSize.Height),
                ViewportUnits = BrushMappingMode.Absolute
            };
            dc.DrawRectangle(pageBrush, null, new Rect(0, 0, PageSize.Width, PageSize.Height));
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            PdfRenderDpi,
            PdfRenderDpi,
            PixelFormats.Pbgra32);
        bitmap.Render(drawingVisual);
        bitmap.Freeze();

        using var imageStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(imageStream);
        imageStream.Position = 0;

        var pdfPage = pdf.AddPage();
        pdfPage.Size = PdfSharp.PageSize.Letter;

        using var gfx = XGraphics.FromPdfPage(pdfPage);
        using var image = XImage.FromStream(imageStream);
        // Fill the letter page exactly — source and target share the same aspect ratio.
        gfx.DrawImage(image, 0, 0, pdfPage.Width.Point, pdfPage.Height.Point);
    }

    private static int ResolvePageCount(DocumentPaginator paginator)
    {
        if (paginator.IsPageCountValid && paginator.PageCount > 0)
            return paginator.PageCount;

        DocumentPage? firstPage = null;
        try
        {
            firstPage = paginator.GetPage(0);
            PumpDispatcher();
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (firstPage is IDisposable disposable)
                disposable.Dispose();
        }

        return paginator.IsPageCountValid ? paginator.PageCount : 1;
    }

    private static void SaveTextAsPdf(string text, string filePath, string title)
    {
        var prefs = EditorPreferences.Instance;
        var pdf = new PdfDocument();
        pdf.Info.Title = title;

        // Prefer a monospaced face for plain-text fidelity; fall back gracefully.
        var bodyFont = CreateFont(prefs.FontFamily, prefs.FontSize, XFontStyleEx.Regular);
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.Letter;

        var gfx = XGraphics.FromPdfPage(page);
        const double margin = 48.0;
        var y = margin;
        var maxWidth = page.Width.Point - margin * 2;
        var lineHeight = bodyFont.GetHeight() * 1.15;
        var tabSpaces = new string(' ', prefs.TabWidth);

        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrEmpty(normalized))
            normalized = " ";

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.Replace("\t", tabSpaces);
            foreach (var wrapped in WrapTextLine(gfx, line, bodyFont, maxWidth))
            {
                if (y > page.Height.Point - margin - lineHeight)
                {
                    gfx.Dispose();
                    page = pdf.AddPage();
                    page.Size = PdfSharp.PageSize.Letter;
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }

                gfx.DrawString(
                    wrapped,
                    bodyFont,
                    XBrushes.Black,
                    new XRect(margin, y, maxWidth, lineHeight),
                    XStringFormats.TopLeft);
                y += lineHeight;
            }
        }

        gfx.Dispose();
        AtomicFileWriter.SavePdf(pdf, filePath);
    }

    /// <summary>
    /// Word-wrap a single logical line to fit <paramref name="maxWidth"/>, breaking long
    /// tokens by character when needed (code, URLs, license keys).
    /// </summary>
    private static IEnumerable<string> WrapTextLine(XGraphics gfx, string line, XFont font, double maxWidth)
    {
        if (line.Length == 0)
        {
            yield return " ";
            yield break;
        }

        if (gfx.MeasureString(line, font).Width <= maxWidth)
        {
            yield return line;
            yield break;
        }

        var words = SplitPreservingSpaces(line);
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + word;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                current.Append(word);
                continue;
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }

            // Word itself is wider than the line — break by character.
            if (gfx.MeasureString(word, font).Width > maxWidth)
            {
                foreach (var chunk in BreakByCharacter(gfx, word, font, maxWidth))
                    yield return chunk;
            }
            else
            {
                current.Append(word);
            }
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    private static List<string> SplitPreservingSpaces(string line)
    {
        var parts = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                var start = i;
                while (i < line.Length && char.IsWhiteSpace(line[i]))
                    i++;
                parts.Add(line[start..i]);
            }
            else
            {
                var start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;
                parts.Add(line[start..i]);
            }
        }

        return parts;
    }

    private static IEnumerable<string> BreakByCharacter(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (text.Length == 0)
        {
            yield return " ";
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = start + 1;
            while (end < text.Length &&
                   gfx.MeasureString(text[start..(end + 1)], font).Width <= maxWidth)
            {
                end++;
            }

            yield return text[start..end];
            start = end;
        }
    }

    private static XFont CreateFont(string family, double size, XFontStyleEx style)
    {
        EnsurePdfFontsConfigured();

        foreach (var candidate in FontFamilyCandidates(family))
        {
            try
            {
                return new XFont(candidate, size, style);
            }
            catch
            {
                // try next candidate
            }
        }

        // Last resort — WindowsSystemFontResolver always maps this face.
        return new XFont("Arial", size, style);
    }

    private static IEnumerable<string> FontFamilyCandidates(string family)
    {
        if (!string.IsNullOrWhiteSpace(family))
            yield return family.Trim();

        yield return "Consolas";
        yield return "Cascadia Mono";
        yield return "Cascadia Code";
        yield return "Courier New";
        yield return "Segoe UI";
        yield return "Arial";
    }

    private static void EnsurePdfFontsConfigured()
    {
        if (Interlocked.Exchange(ref _fontsConfigured, 1) == 1)
            return;

        try
        {
            // PDFsharp 6 Core build has no built-in Windows font access unless configured.
            GlobalFontSettings.FontResolver = new WindowsSystemFontResolver();
        }
        catch
        {
            // Already configured elsewhere — leave as-is.
            try
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            }
            catch
            {
                // Ignore; CreateFont will surface a clearer error if fonts truly fail.
            }
        }
    }

    /// <summary>
    /// Loads TrueType/OpenType faces from the Windows Fonts folder for PDFsharp Core.
    /// </summary>
    private sealed class WindowsSystemFontResolver : IFontResolver
    {
        private static readonly string FontsDirectory =
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        // family (lower) → (regular, bold, italic, boldItalic) file names
        private static readonly Dictionary<string, string[]> KnownFamilies =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Arial"] = ["arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"],
                ["Times New Roman"] = ["times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf"],
                ["Courier New"] = ["cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf"],
                ["Verdana"] = ["verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf"],
                ["Tahoma"] = ["tahoma.ttf", "tahomabd.ttf", "tahoma.ttf", "tahomabd.ttf"],
                ["Segoe UI"] = ["segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"],
                ["Consolas"] = ["consola.ttf", "consolab.ttf", "consolai.ttf", "consolaz.ttf"],
                ["Cascadia Mono"] = ["CascadiaMono.ttf", "CascadiaMono.ttf", "CascadiaMono.ttf", "CascadiaMono.ttf"],
                ["Cascadia Code"] = ["CascadiaCode.ttf", "CascadiaCode.ttf", "CascadiaCode.ttf", "CascadiaCode.ttf"],
                ["Lucida Console"] = ["lucon.ttf", "lucon.ttf", "lucon.ttf", "lucon.ttf"],
                ["Calibri"] = ["calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf"],
                ["Cambria"] = ["cambria.ttc", "cambriab.ttf", "cambriai.ttf", "cambriaz.ttf"],
            };

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var path = ResolveFontFile(familyName, isBold, isItalic)
                       ?? ResolveFontFile("Arial", isBold, isItalic)
                       ?? ResolveFontFile("Consolas", false, false);
            if (path is null || !File.Exists(path))
                return null;

            // Face name is the absolute path; GetFont reads those bytes.
            return new FontResolverInfo(path);
        }

        public byte[] GetFont(string faceName)
        {
            if (string.IsNullOrWhiteSpace(faceName) || !File.Exists(faceName))
                return Array.Empty<byte>();

            return File.ReadAllBytes(faceName);
        }

        private static string? ResolveFontFile(string familyName, bool isBold, bool isItalic)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return null;

            var styleIndex = (isBold ? 1 : 0) + (isItalic ? 2 : 0);

            if (BundledFonts.FileForFamily(familyName) is { } bundled && File.Exists(bundled))
                return bundled;

            if (KnownFamilies.TryGetValue(familyName.Trim(), out var files))
            {
                var preferred = Path.Combine(FontsDirectory, files[styleIndex]);
                if (File.Exists(preferred))
                    return preferred;

                // Fall back within the family to regular if a style is missing.
                var regular = Path.Combine(FontsDirectory, files[0]);
                if (File.Exists(regular))
                    return regular;
            }

            // Heuristic: match file names that start with the family (no spaces).
            var stem = familyName.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
            try
            {
                foreach (var file in Directory.EnumerateFiles(FontsDirectory, "*.ttf")
                             .Concat(Directory.EnumerateFiles(FontsDirectory, "*.otf")))
                {
                    var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (name.StartsWith(stem, StringComparison.Ordinal) ||
                        name.Equals(stem, StringComparison.Ordinal))
                        return file;
                }
            }
            catch
            {
                // Fonts folder inaccessible
            }

            return null;
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }),
            null);
        Dispatcher.PushFrame(frame);
    }
}