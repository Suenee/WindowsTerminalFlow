using System.Text.Json;
using WindowsTerminalFlow.Models;

namespace WindowsTerminalFlow.Services;

public static class LaunchRequestStore
{
    public static void Write(LaunchRequest request)
    {
        Directory.CreateDirectory(AppPaths.LocalDirectory);
        File.WriteAllText(AppPaths.LaunchRequestFile, JsonSerializer.Serialize(request));
    }

    public static LaunchRequest Read()
    {
        if (!File.Exists(AppPaths.LaunchRequestFile))
            return new LaunchRequest(Environment.CurrentDirectory, []);
        return JsonSerializer.Deserialize<LaunchRequest>(File.ReadAllText(AppPaths.LaunchRequestFile))
               ?? new LaunchRequest(Environment.CurrentDirectory, []);
    }
}
