namespace WindowsTerminalFlow.Services;

public static class Logger
{
    private static string _mode = "off";

    public static void Initialize(string mode)
    {
        _mode = mode.ToLowerInvariant();
        if (_mode == "off") return;
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LogFile)!);
        if (_mode == "single") File.WriteAllText(AppPaths.LogFile, string.Empty);
    }

    public static void Info(string text)
    {
        if (_mode == "off") return;
        File.AppendAllText(AppPaths.LogFile, $"{DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} {text}{Environment.NewLine}");
    }
}
