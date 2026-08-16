using System.IO;
using System.Text;

namespace TextPad.Services;

public static class CrashLogger
{
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "com.textpad.editor",
        "crash.log");

    public static void Log(Exception exception, string source)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(directory);

            var entry = new StringBuilder()
                .AppendLine($"[{DateTime.UtcNow:O}] {source}")
                .AppendLine(exception.ToString())
                .AppendLine()
                .ToString();

            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Best-effort logging.
        }
    }
}