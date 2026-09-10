namespace WindowsTerminalFlow.Services;

public static class AppPaths
{
    public static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsTerminalFlow");
    public static string LocalDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsTerminalFlow");
    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");
    public static string WorkspacesFile => Path.Combine(ConfigDirectory, "workspaces.json");
    public static string LaunchRequestFile => Path.Combine(LocalDirectory, "launch-request.json");
    public static string LogFile => Path.Combine(LocalDirectory, "logs", "wtf.log");
}
