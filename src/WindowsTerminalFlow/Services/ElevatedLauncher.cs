using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using WindowsTerminalFlow.Models;

namespace WindowsTerminalFlow.Services;

public static class ElevatedLauncher
{
    private const string TaskName = "WindowsTerminalFlow Elevated Launcher";

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool TryRunRegisteredTask(string requestFile)
    {
        try
        {
            Logger.Info($"Starting registered elevated task for request: {requestFile}");
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("/Run");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            if (p?.ExitCode != 0)
            {
                Logger.Warn($"Scheduled task start failed; exit={p?.ExitCode}");
                return false;
            }

            for (var i = 0; i < 30; i++)
            {
                if (!File.Exists(requestFile))
                {
                    Logger.Info("Elevated task consumed the launch request.");
                    return true;
                }
                Thread.Sleep(100);
            }

            Logger.Warn("Scheduled task reported success but did not consume the launch request; task will be repaired via UAC.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unable to start registered elevated task");
            return false;
        }
    }

    public static void RegisterWithUacAndRun(string requestFile)
    {
        var executable = PathResolver.ForElevation(Environment.ProcessPath!);
        Logger.Info($"Requesting UAC repair through executable: {executable}");
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.SystemDirectory
        };
        psi.ArgumentList.Add("--register-and-run");
        psi.ArgumentList.Add(requestFile);
        Process.Start(psi);
    }

    public static void DispatchPendingRequests()
    {
        Logger.Info("Scheduled launcher dispatcher started.");
        foreach (var requestFile in LaunchRequestStore.GetPendingFiles())
        {
            try
            {
                var request = JsonSerializer.Deserialize<LaunchRequest>(File.ReadAllText(requestFile));
                var executable = request?.ExecutablePath;
                if (string.IsNullOrWhiteSpace(executable))
                    executable = PathResolver.ForElevation(Environment.ProcessPath!);

                if (!File.Exists(executable))
                {
                    Logger.Warn($"Cannot dispatch request because executable is unavailable: {executable}");
                    continue;
                }

                Logger.Info($"Scheduled dispatcher launching: {executable}");
                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    WorkingDirectory = Environment.SystemDirectory
                };
                psi.ArgumentList.Add("--consume-request");
                psi.ArgumentList.Add(requestFile);
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Scheduled dispatcher failed for request {requestFile}");
            }
        }
    }

    public static void RegisterTask()
    {
        if (!IsAdministrator())
            throw new InvalidOperationException("Administrator rights are required to register the launcher.");

        var executable = PathResolver.ForElevation(Environment.ProcessPath!);
        var executablePs = executable.Replace("'", "''");
        var user = WindowsIdentity.GetCurrent().Name.Replace("'", "''");
        var taskName = TaskName.Replace("'", "''");
        var ps = $"$a=New-ScheduledTaskAction -Execute '{executablePs}' -Argument '--scheduled-launcher';" +
                 $"$p=New-ScheduledTaskPrincipal -UserId '{user}' -LogonType Interactive -RunLevel Highest;" +
                 "$s=New-ScheduledTaskSettingsSet -MultipleInstances Parallel;" +
                 "$t=New-ScheduledTask -Action $a -Principal $p -Settings $s;" +
                 $"Register-ScheduledTask -TaskName '{taskName}' -InputObject $t -Force | Out-Null";

        Logger.Info($"Registering elevated launcher task directly against: {executable}");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(ps);
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode != 0)
            throw new InvalidOperationException($"Failed to register elevated launcher task. Exit code: {p?.ExitCode}");

        Logger.Info("Elevated launcher task registered successfully.");
    }
}
