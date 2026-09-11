namespace WindowsTerminalFlow.Services;

public static class AppPaths
{
    public static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsTerminalFlow");
    public static string LocalDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsTerminalFlow");
    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");
    public static string WorkspacesFile => Path.Combine(ConfigDirectory, "workspaces.json");
    public static string RequestsDirectory => Path.Combine(LocalDirectory, "requests");
    public static string LogFile => Path.Combine(LocalDirectory, "logs", "wtf.log");
    public static string ElevatedLauncherScript => Path.Combine(LocalDirectory, "elevated-launcher.ps1");
}
