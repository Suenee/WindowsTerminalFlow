using System.Text.Json;
using WindowsTerminalFlow.Models;

namespace WindowsTerminalFlow.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static AppSettings Settings { get; private set; } = new();

    public static void Initialize()
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        if (File.Exists(AppPaths.ConfigFile))
        {
            Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.ConfigFile), JsonOptions) ?? new AppSettings();
        }
        Save();
    }

    public static void Save() => File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(Settings, JsonOptions));
}
