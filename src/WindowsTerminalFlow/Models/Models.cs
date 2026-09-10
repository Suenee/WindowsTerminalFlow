namespace WindowsTerminalFlow.Models;

public sealed record LaunchRequest(string WorkingDirectory, string[] Arguments);

public sealed class AppSettings
{
    public string Language { get; set; } = "cs";
    public string LoggingMode { get; set; } = "single";
    public string Shell { get; set; } = "cmd.exe";
    public int FontSize { get; set; } = 12;
    public bool NewFoldersEnabled { get; set; } = true;
}

public sealed class WorkspaceStore
{
    public Dictionary<string, WorkspaceDefinition> Workspaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkspaceDefinition
{
    public List<WorkspaceFolder> Folders { get; set; } = [];
}

public sealed class WorkspaceFolder
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
