using System.Diagnostics;
using System.Security.Principal;

namespace WindowsTerminalFlow.Services;

public static class ElevatedLauncher
{
    private const string TaskName = "WindowsTerminalFlow Elevated Launcher";

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool TryRunRegisteredTask()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Run /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    public static void RegisterWithUacAndRun(string requestFile)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            Arguments = $"--register-and-run \"{requestFile}\"",
            UseShellExecute = true,
            Verb = "runas"
        });
    }

    public static void DispatchPendingRequests()
    {
        foreach (var requestFile in LaunchRequestStore.GetPendingFiles())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                Arguments = $"--consume-request \"{requestFile}\"",
                UseShellExecute = false
            });
        }
    }

    public static void RegisterTask()
    {
        if (!IsAdministrator()) throw new InvalidOperationException("Administrator rights are required to register the launcher.");

        var exe = Environment.ProcessPath!.Replace("'", "''");
        var user = WindowsIdentity.GetCurrent().Name.Replace("'", "''");
        var taskName = TaskName.Replace("'", "''");
        var ps = $"$a=New-ScheduledTaskAction -Execute '{exe}' -Argument '--scheduled-launcher';" +
                 $"$p=New-ScheduledTaskPrincipal -UserId '{user}' -LogonType Interactive -RunLevel Highest;" +
                 "$s=New-ScheduledTaskSettingsSet -MultipleInstances Parallel -ExecutionTimeLimit (New-TimeSpan -Minutes 1);" +
                 "$t=New-ScheduledTask -Action $a -Principal $p -Settings $s;" +
                 $"Register-ScheduledTask -TaskName '{taskName}' -InputObject $t -Force | Out-Null";

        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        p?.WaitForExit();
        if (p?.ExitCode != 0) throw new InvalidOperationException("Failed to register elevated launcher task.");
    }
}
