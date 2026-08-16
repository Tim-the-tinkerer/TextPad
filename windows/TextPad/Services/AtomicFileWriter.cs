using System.IO;

namespace TextPad.Services;

public static class AtomicFileWriter
{
    public static void WriteAllBytes(string targetPath, byte[] data)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Target path has no directory.");

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       options: FileOptions.WriteThrough))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }

            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, null);
            else
                File.Move(tempPath, targetPath);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup.
            }

            throw;
        }
    }

    public static void WriteAllText(string targetPath, string content)
    {
        WriteAllBytes(targetPath, System.Text.Encoding.UTF8.GetBytes(content));
    }

    public static void SavePdf(PdfSharp.Pdf.PdfDocument pdf, string targetPath)
    {
        using var stream = new MemoryStream();
        pdf.Save(stream, closeStream: false);
        WriteAllBytes(targetPath, stream.ToArray());
    }
}