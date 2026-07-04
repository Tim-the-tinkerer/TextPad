using System.IO;

namespace TextPad.Services;

public static class AppHelp
{
    public static string HelpFileName => "Help.md";

    public static string HelpFilePath => Path.Combine(AppContext.BaseDirectory, HelpFileName);

    public static bool IsAvailable => File.Exists(HelpFilePath);
}