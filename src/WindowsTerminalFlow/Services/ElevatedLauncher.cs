using System.Diagnostics;
using System.Security.Principal;
using System.Text;
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
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Run /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit(5000);
            if (p?.ExitCode != 0)
            {
                Logger.Warn($"Scheduled task start failed; exit={p?.ExitCode}");
                return false;
            }

            // schtasks /Run only confirms that Task Scheduler accepted the request.
            // A stale task may still point to an inaccessible mapped drive. Confirm that
            // the elevated side really consumed this launch request.
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
        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"--register-and-run \"{requestFile}\"",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.SystemDirectory
        });
    }

    // Compatibility path for tasks registered by 1.00. Do not delete the request here;
    // the spawned --consume-request process is the single owner that consumes it.
    public static void DispatchPendingRequests()
    {
        Logger.Info("Legacy scheduled launcher dispatcher started.");
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

                Logger.Info($"Legacy dispatcher launching: {executable}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"--consume-request \"{requestFile}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Environment.SystemDirectory
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Legacy dispatcher failed for request {requestFile}");
            }
        }
    }

    public static void RegisterTask()
    {
        if (!IsAdministrator())
            throw new InvalidOperationException("Administrator rights are required to register the launcher.");

        Directory.CreateDirectory(AppPaths.LocalDirectory);
        WriteLocalBrokerScript();

        var script = AppPaths.ElevatedLauncherScript.Replace("'", "''");
        var user = WindowsIdentity.GetCurrent().Name.Replace("'", "''");
        var taskName = TaskName.Replace("'", "''");
        var ps = "$a=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"';" +
                 $"$p=New-ScheduledTaskPrincipal -UserId '{user}' -LogonType Interactive -RunLevel Highest;" +
                 "$s=New-ScheduledTaskSettingsSet -MultipleInstances Parallel;" +
                 "$t=New-ScheduledTask -Action $a -Principal $p -Settings $s;" +
                 $"Register-ScheduledTask -TaskName '{taskName}' -InputObject $t -Force | Out-Null";

        Logger.Info($"Registering elevated launcher task with local broker: {AppPaths.ElevatedLauncherScript}");
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        });
        p?.WaitForExit();
        if (p?.ExitCode != 0)
            throw new InvalidOperationException($"Failed to register elevated launcher task. Exit code: {p?.ExitCode}");

        Logger.Info("Elevated launcher task registered successfully.");
    }

    private static void WriteLocalBrokerScript()
    {
        var requestsDirectory = AppPaths.RequestsDirectory.Replace("'", "''");
        var logFile = AppPaths.LogFile.Replace("'", "''");
        var script = $$"""
$ErrorActionPreference = 'Continue'
$requests = '{{requestsDirectory}}'
$log = '{{logFile}}'
function Write-WtfLog([string]$message) {
    try {
        $dir = Split-Path -Parent $log
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Add-Content -LiteralPath $log -Value (('{0:dd.MM.yyyy HH:mm:ss.fff} [INFO] broker pid={1} {2}' -f (Get-Date), $PID, $message)) -Encoding UTF8
    } catch {}
}
if (-not (Test-Path -LiteralPath $requests)) { exit 0 }
Get-ChildItem -LiteralPath $requests -Filter '*.json' -File | Sort-Object CreationTimeUtc | ForEach-Object {
    $requestFile = $_.FullName
    try {
        $request = Get-Content -LiteralPath $requestFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $exe = [string]$request.ExecutablePath
        if ([string]::IsNullOrWhiteSpace($exe) -or -not (Test-Path -LiteralPath $exe)) {
            Write-WtfLog "Executable unavailable for request $requestFile : $exe"
            return
        }
        Write-WtfLog "Launching $exe for request $requestFile"
        Start-Process -FilePath $exe -ArgumentList @('--consume-request', ('"' + $requestFile + '"')) -WorkingDirectory $env:SystemRoot
    } catch {
        Write-WtfLog "Broker error for $requestFile : $($_.Exception.Message)"
    }
}
""";
        File.WriteAllText(AppPaths.ElevatedLauncherScript, script, new UTF8Encoding(false));
    }
}
