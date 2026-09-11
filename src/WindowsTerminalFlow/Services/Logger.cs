namespace WindowsTerminalFlow.Services;

public static class Logger
{
    private static readonly object Sync = new();
    private static string _mode = "off";
    private static string _runId = string.Empty;

    public static void Initialize(string mode, string? runId = null, bool startNewRun = true)
    {
        _mode = (mode ?? "off").ToLowerInvariant();
        _runId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString("N") : runId;
        if (_mode == "off") return;

        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LogFile)!);
        if (_mode == "single" && startNewRun)
            File.WriteAllText(AppPaths.LogFile, string.Empty);

        Info($"LOG START mode={_mode}; run={_runId}; pid={Environment.ProcessId}");
    }

    public static void Info(string text) => Write("INFO", text);
    public static void Warn(string text) => Write("WARN", text);
    public static void Error(string text) => Write("ERROR", text);
    public static void Error(Exception ex, string context) => Write("ERROR", $"{context}: {ex}");

    private static void Write(string level, string text)
    {
        if (_mode == "off") return;
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LogFile)!);
                File.AppendAllText(AppPaths.LogFile,
                    $"{DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} [{level}] run={_runId} pid={Environment.ProcessId} {text}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}
