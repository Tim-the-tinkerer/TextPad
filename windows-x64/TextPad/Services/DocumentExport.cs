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
using PdfSharp.Pdf;
using TextPad.Models;

namespace TextPad.Services;

public static class DocumentExport
{
    private static readonly Size PageSize = new(816, 1056); // 8.5" x 11" at 96 DPI

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

            var document = PrepareForPagination(
                BuildPlainFlowDocument(string.Empty, plainText ?? string.Empty, includeTitle: false));
            try
            {
                ExportFlowDocumentToPdf(document, dialog.FileName, safeTitle);
            }
            catch (Exception flowExportError)
            {
                var window = owner as Window ?? Window.GetWindow(owner);
                System.Windows.MessageBox.Show(
                    window,
                    $"Formatted PDF export failed. Saving as plain text instead.\n\n{flowExportError.Message}",
                    "TextPad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                SaveTextAsPdf(plainText ?? string.Empty, dialog.FileName, safeTitle);
            }
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
            FontFamily = new FontFamily(prefs.FontFamily),
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
        var width = Math.Max(1, (int)Math.Ceiling(PageSize.Width));
        var height = Math.Max(1, (int)Math.Ceiling(PageSize.Height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(documentPage.Visual);

        using var imageStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(imageStream);
        imageStream.Position = 0;

        var pdfPage = pdf.AddPage();
        pdfPage.Size = PdfSharp.PageSize.Letter;

        using var gfx = XGraphics.FromPdfPage(pdfPage);
        using var image = XImage.FromStream(imageStream);
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

        var bodyFont = CreateFont(prefs.FontFamily, prefs.FontSize, XFontStyleEx.Regular);
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.Letter;

        var gfx = XGraphics.FromPdfPage(page);
        var y = 48.0;
        var maxWidth = page.Width.Point - 96;
        var lineHeight = bodyFont.GetHeight();
        var tabSpaces = new string(' ', prefs.TabWidth);

        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrEmpty(normalized))
            normalized = " ";

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.Replace("\t", tabSpaces);
            if (y > page.Height.Point - 48 - lineHeight)
            {
                gfx.Dispose();
                page = pdf.AddPage();
                page.Size = PdfSharp.PageSize.Letter;
                gfx = XGraphics.FromPdfPage(page);
                y = 48;
            }

            gfx.DrawString(line, bodyFont, XBrushes.Black, new XRect(48, y, maxWidth, lineHeight), XStringFormats.TopLeft);
            y += lineHeight;
        }

        gfx.Dispose();
        AtomicFileWriter.SavePdf(pdf, filePath);
    }

    private static XFont CreateFont(string family, double size, XFontStyleEx style)
    {
        try
        {
            return new XFont(family, size, style);
        }
        catch
        {
            return new XFont("Consolas", size, style);
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