# WindowsTerminalFlow (WTF)

WindowsTerminalFlow is a lightweight Windows terminal workspace manager. It turns the direct subdirectories of the current directory into terminal tabs, remembers their order and visibility, and restores the workspace on the next launch.

Current development version: **1.00** on branch `DEVEL`.

## Commands

```text
wtf            Open the workspace for the current directory.
wtf c          Configure the current workspace.
wtf config     Configure the current workspace.
wtf l          Reload all current folders and enable their tabs.
wtf load       Reload all current folders and enable their tabs.
wtf setup      Open application-wide settings.
```

## Workspace behavior

On first use, WTF discovers all direct subdirectories and sorts them by name. A known workspace uses its saved order. Closing a tab with its `×` button disables that folder for the next launch. `wtf config` provides checkboxes and drag-and-drop ordering. Newly discovered folders are appended and enabled by default. `wtf load` re-enables all folders that currently exist.

Application settings live under `%APPDATA%\WindowsTerminalFlow`. Runtime launch requests and logs live under `%LOCALAPPDATA%\WindowsTerminalFlow`. Repository location may be local, mapped, or UNC/network storage. Before crossing the UAC boundary WTF resolves mapped network drives to UNC paths; CMD sessions use `pushd` for UNC working directories.

## Administrator mode

The default mode is elevated. The first unelevated launch stores the current directory and arguments and asks for UAC once to register `WindowsTerminalFlow Elevated Launcher` in Windows Task Scheduler with highest privileges. Later starts request that registered task and do not prompt for UAC again. The scheduled task is configured for parallel requests, allowing multiple WTF workspaces to coexist. WTF does not disable or weaken UAC globally.

If the executable moves, use `wtf setup` and **Repair elevated launcher**.

## Build

Requirements are handled by `upgrade.cmd` where possible. Manual build:

```text
dotnet restore WindowsTerminalFlow.sln
dotnet build WindowsTerminalFlow.sln -c Release
```

The project targets .NET 10 LTS and uses EasyWindowsTerminalControl 1.0.38 to host Windows Terminal/ConPTY sessions inside WPF tabs.

## Configuration

The application has CZ/EN language selection, shell selection (`cmd.exe`, Windows PowerShell, or PowerShell 7), default handling for newly discovered folders, and logging modes `off`, `single`, and `all`.

## License

MIT.
