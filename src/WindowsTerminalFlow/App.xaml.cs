using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
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
            var cwd = Environment.CurrentDirectory;
            var args = e.Args.ToList();

            if (args.Contains("--register-and-run", StringComparer.OrdinalIgnoreCase))
            {
                ElevatedLauncher.RegisterTask();
                var request = LaunchRequestStore.Read();
                StartUi(request.WorkingDirectory, request.Arguments);
                return;
            }

            if (args.Contains("--scheduled", StringComparer.OrdinalIgnoreCase))
            {
                var request = LaunchRequestStore.Read();
                StartUi(request.WorkingDirectory, request.Arguments);
                return;
            }

            if (!ElevatedLauncher.IsAdministrator())
            {
                LaunchRequestStore.Write(new LaunchRequest(cwd, args.ToArray()));
                if (ElevatedLauncher.TryRunRegisteredTask())
                {
                    Shutdown();
                    return;
                }

                ElevatedLauncher.RegisterWithUacAndRun();
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
