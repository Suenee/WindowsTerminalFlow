using System.Windows;
using WindowsTerminalFlow.Models;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class App : Application
{
    public const string Version = "1.02";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string? runId = null;
        try
        {
            SettingsService.Initialize();
            var args = e.Args.ToList();

            if (args.Contains("--scheduled-launcher", StringComparer.OrdinalIgnoreCase))
            {
                Logger.Initialize(SettingsService.Settings.LoggingMode, startNewRun: false);
                Logger.Info("Scheduled launcher entry point started.");
                ElevatedLauncher.DispatchPendingRequests();
                Shutdown();
                return;
            }

            var consumeIndex = args.FindIndex(x => x.Equals("--consume-request", StringComparison.OrdinalIgnoreCase));
            if (consumeIndex >= 0 && consumeIndex + 1 < args.Count)
            {
                var requestPath = args[consumeIndex + 1];
                var request = LaunchRequestStore.ReadAndDelete(requestPath);
                runId = request.RunId;
                Logger.Initialize(SettingsService.Settings.LoggingMode, runId, startNewRun: false);
                Logger.Info($"Elevated request consumed; file={requestPath}");
                StartUi(request.WorkingDirectory, request.Arguments);
                return;
            }

            var registerIndex = args.FindIndex(x => x.Equals("--register-and-run", StringComparison.OrdinalIgnoreCase));
            if (registerIndex >= 0 && registerIndex + 1 < args.Count)
            {
                var requestPath = args[registerIndex + 1];
                var request = LaunchRequestStore.ReadAndDelete(requestPath);
                runId = request.RunId;
                Logger.Initialize(SettingsService.Settings.LoggingMode, runId, startNewRun: false);
                Logger.Info("UAC registration process started.");
                ElevatedLauncher.RegisterTask();
                StartUi(request.WorkingDirectory, request.Arguments);
                return;
            }

            runId = Guid.NewGuid().ToString("N");
            Logger.Initialize(SettingsService.Settings.LoggingMode, runId, startNewRun: true);
            Logger.Info($"WindowsTerminalFlow {Version} startup; raw cwd={Environment.CurrentDirectory}; elevated={ElevatedLauncher.IsAdministrator()}; args=[{string.Join(" | ", args)}]");

            var cwd = PathResolver.ForElevation(Environment.CurrentDirectory);
            var normalizedArgs = NormalizePathArgumentsForElevation(args, Environment.CurrentDirectory);
            var executable = PathResolver.ForElevation(Environment.ProcessPath!);

            if (!ElevatedLauncher.IsAdministrator())
            {
                var requestFile = LaunchRequestStore.Write(new LaunchRequest(cwd, normalizedArgs, executable, runId));
                if (ElevatedLauncher.TryRunRegisteredTask(requestFile))
                {
                    Logger.Info("Launch handed off to registered elevated task.");
                    Shutdown();
                    return;
                }

                Logger.Warn("Registered elevated launcher is unavailable or stale; requesting one-time UAC repair.");
                ElevatedLauncher.RegisterWithUacAndRun(requestFile);
                Shutdown();
                return;
            }

            StartUi(cwd, normalizedArgs);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Fatal startup error");
            MessageBox.Show(ex.ToString(), "WindowsTerminalFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static string[] NormalizePathArgumentsForElevation(List<string> args, string baseDirectory)
    {
        var result = args.ToArray();
        if (result.Length == 0) return result;

        var first = result[0].ToLowerInvariant();
        var pathIndex = first is "config" or "c" or "load" or "l" ? 1 : first == "setup" ? -1 : 0;
        if (pathIndex < 0 || pathIndex >= result.Length) return result;

        var candidate = result[pathIndex];
        var absolute = Path.IsPathRooted(candidate) ? candidate : Path.Combine(baseDirectory, candidate);
        if (Directory.Exists(absolute))
            result[pathIndex] = PathResolver.ForElevation(absolute);
        return result;
    }

    private void StartUi(string workingDirectory, string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        var workspace = workingDirectory;

        if (command is "config" or "c" or "load" or "l")
        {
            if (args.Length > 1)
                workspace = ResolveWorkspace(args[1], workingDirectory);
        }
        else if (command != "setup" && args.Length > 0)
        {
            workspace = ResolveWorkspace(args[0], workingDirectory);
            command = string.Empty;
        }

        if (command != "setup" && !Directory.Exists(workspace))
            throw new DirectoryNotFoundException($"{LocalizationService.Get("path_not_found")}: {workspace}");

        Environment.CurrentDirectory = Environment.SystemDirectory;

        Logger.Info($"Opening UI; command={command}; workspace={workspace}; elevated={ElevatedLauncher.IsAdministrator()}");
        Window window = command switch
        {
            "setup" => new SetupWindow(),
            "config" or "c" => new WorkspaceConfigWindow(workspace),
            _ => new MainWindow(workspace, command is "load" or "l")
        };

        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Closed += (_, _) => Logger.Info("Main window closed.");
        window.Show();
    }

    private static string ResolveWorkspace(string path, string baseDirectory)
    {
        var absolute = Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path);
        return PathResolver.ForElevation(absolute);
    }
}
