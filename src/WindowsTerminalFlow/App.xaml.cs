using System.Windows;
using WindowsTerminalFlow.Models;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class App : Application
{
    public const string Version = "1.00";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var args = e.Args.ToList();

            if (args.Contains("--scheduled-launcher", StringComparer.OrdinalIgnoreCase))
            {
                ElevatedLauncher.DispatchPendingRequests();
                Shutdown();
                return;
            }

            var consumeIndex = args.FindIndex(x => x.Equals("--consume-request", StringComparison.OrdinalIgnoreCase));
            if (consumeIndex >= 0 && consumeIndex + 1 < args.Count)
            {
                var request = LaunchRequestStore.ReadAndDelete(args[consumeIndex + 1]);
                StartUi(request.WorkingDirectory, request.Arguments);
                return;
            }

            var registerIndex = args.FindIndex(x => x.Equals("--register-and-run", StringComparison.OrdinalIgnoreCase));
            if (registerIndex >= 0 && registerIndex + 1 < args.Count)
            {
                ElevatedLauncher.RegisterTask();
                var request = LaunchRequestStore.ReadAndDelete(args[registerIndex + 1]);
                StartUi(request.WorkingDirectory, request.Arguments);
                return;
            }

            var cwd = PathResolver.ForElevation(Environment.CurrentDirectory);
            if (!ElevatedLauncher.IsAdministrator())
            {
                var requestFile = LaunchRequestStore.Write(new LaunchRequest(cwd, args.ToArray()));
                if (ElevatedLauncher.TryRunRegisteredTask())
                {
                    Shutdown();
                    return;
                }

                ElevatedLauncher.RegisterWithUacAndRun(requestFile);
                Shutdown();
                return;
            }

            StartUi(cwd, args.ToArray());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "WindowsTerminalFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void StartUi(string workingDirectory, string[] args)
    {
        if (!Directory.Exists(workingDirectory))
            throw new DirectoryNotFoundException(workingDirectory);

        Environment.CurrentDirectory = workingDirectory;
        SettingsService.Initialize();
        Logger.Initialize(SettingsService.Settings.LoggingMode);
        Logger.Info($"WindowsTerminalFlow {Version}; cwd={workingDirectory}; elevated={ElevatedLauncher.IsAdministrator()}");

        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        Window window = command switch
        {
            "setup" => new SetupWindow(),
            "config" or "c" => new WorkspaceConfigWindow(workingDirectory),
            _ => new MainWindow(workingDirectory, command is "load" or "l")
        };

        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }
}
