using System.IO;

namespace TextPad.Services;

public static class SafeFileReader
{
    private const int MaxAttempts = 5;

    public static byte[] ReadAllBytes(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found.", path);

        Exception? lastError = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(50 * attempt);

            try
            {
                var sizeBefore = new FileInfo(path).Length;
                var bytes = File.ReadAllBytes(path);
                var sizeAfter = new FileInfo(path).Length;
                if (bytes.Length == sizeBefore && bytes.Length == sizeAfter)
                    return bytes;
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
            }
        }

        throw new IOException(
            $"Unable to read a stable copy of \"{path}\".",
            lastError ?? new IOException("The file changed while it was being read."));
    }
}