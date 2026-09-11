using System.Text.Json;
using WindowsTerminalFlow.Models;

namespace WindowsTerminalFlow.Services;

public static class LaunchRequestStore
{
    public static string Write(LaunchRequest request)
    {
        Directory.CreateDirectory(AppPaths.RequestsDirectory);
        var path = Path.Combine(AppPaths.RequestsDirectory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        Logger.Info($"Launch request created: {path}; cwd={request.WorkingDirectory}; exe={request.ExecutablePath}; args=[{string.Join(" | ", request.Arguments)}]");
        return path;
    }

    public static LaunchRequest ReadAndDelete(string path)
    {
        if (!File.Exists(path))
        {
            Logger.Warn($"Launch request missing: {path}");
            return new LaunchRequest(Environment.CurrentDirectory, []);
        }

        var request = JsonSerializer.Deserialize<LaunchRequest>(File.ReadAllText(path))
                      ?? new LaunchRequest(Environment.CurrentDirectory, []);
        try
        {
            File.Delete(path);
            Logger.Info($"Launch request consumed: {path}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not delete consumed launch request {path}: {ex.Message}");
        }
        return request;
    }

    public static IEnumerable<string> GetPendingFiles()
    {
        if (!Directory.Exists(AppPaths.RequestsDirectory)) return [];
        return Directory.GetFiles(AppPaths.RequestsDirectory, "*.json").OrderBy(File.GetCreationTimeUtc).ToArray();
    }
}
