namespace WindowsTerminalFlow.Services;

public static class AppPaths
{
    public static string ProjectDirectory
    {
        get
        {
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(baseDir);
            return name.Equals("dist", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(baseDir)?.FullName ?? baseDir
                : baseDir;
        }
    }

    public static string ConfigDirectory => Path.Combine(ProjectDirectory, "config");
    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");
    public static string WorkspacesFile => Path.Combine(ConfigDirectory, "workspaces.json");
    public static string RuntimeDirectory => Path.Combine(ProjectDirectory, ".runtime");
    public static string RequestsDirectory => Path.Combine(RuntimeDirectory, "requests");
    public static string LogFile => Path.Combine(ProjectDirectory, "logs", "wtf.log");
}
