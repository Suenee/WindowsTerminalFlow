using System.Text.Json;
using WindowsTerminalFlow.Models;

namespace WindowsTerminalFlow.Services;

public static class WorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static WorkspaceDefinition Load(string root, bool forceLoadAll = false)
    {
        root = Normalize(root);
        var store = ReadStore();
        if (!store.Workspaces.TryGetValue(root, out var ws))
        {
            ws = new WorkspaceDefinition();
            store.Workspaces[root] = ws;
            Logger.Info($"New workspace discovered: {root}");
        }

        var current = Directory.GetDirectories(root)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (ws.Folders.Count == 0)
        {
            ws.Folders = current.Select(x => new WorkspaceFolder { Name = x, Enabled = true }).ToList();
            Logger.Info($"Workspace initialized: {root}; folders={current.Count}");
        }
        else
        {
            var known = new HashSet<string>(ws.Folders.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var folder in current.Where(x => !known.Contains(x)))
            {
                ws.Folders.Add(new WorkspaceFolder { Name = folder, Enabled = SettingsService.Settings.NewFoldersEnabled });
                Logger.Info($"New workspace folder discovered: {folder}; enabled={SettingsService.Settings.NewFoldersEnabled}");
            }
        }

        if (forceLoadAll)
        {
            foreach (var f in ws.Folders.Where(f => current.Contains(f.Name, StringComparer.OrdinalIgnoreCase))) f.Enabled = true;
            Logger.Info($"Workspace load requested: all current folders enabled for {root}");
        }

        SaveStore(store);
        return ws;
    }

    public static void Save(string root, WorkspaceDefinition definition)
    {
        var store = ReadStore();
        store.Workspaces[Normalize(root)] = definition;
        SaveStore(store);
        Logger.Info($"Workspace state saved: {root}; folders={definition.Folders.Count}");
    }

    public static void SetEnabled(string root, string name, bool enabled)
    {
        var ws = Load(root);
        var item = ws.Folders.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item != null) item.Enabled = enabled;
        Save(root, ws);
        Logger.Info($"Workspace folder enabled state changed: {name}={enabled}");
    }

    private static WorkspaceStore ReadStore()
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        if (!File.Exists(AppPaths.WorkspacesFile)) return new WorkspaceStore();
        return JsonSerializer.Deserialize<WorkspaceStore>(File.ReadAllText(AppPaths.WorkspacesFile), JsonOptions) ?? new WorkspaceStore();
    }

    private static void SaveStore(WorkspaceStore store) => File.WriteAllText(AppPaths.WorkspacesFile, JsonSerializer.Serialize(store, JsonOptions));
    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
