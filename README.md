# WindowsTerminalFlow (WTF)

WindowsTerminalFlow is a lightweight Windows terminal workspace manager. It turns the direct subdirectories of a selected directory into terminal tabs, remembers their order and visibility, and restores the workspace on the next launch.

Current development version: **1.01** on branch `DEVEL`.

## First installation / fresh folder

The supported bootstrap path is intentionally simple:

1. Create or use an empty target folder.
2. Put only `upgrade.cmd` into that folder.
3. Run `upgrade.cmd`.

If no Git repository exists, the launcher copies itself to `%TEMP%`, removes the bootstrap copy from the target, clones `Suenee/WindowsTerminalFlow` branch `DEVEL` directly into the same folder, and hands control to the current authoritative updater. The bootstrap refuses to clone over unrelated files. Mapped and UNC/network locations are supported.

For an existing checkout, the same `upgrade.cmd` self-updates through the current remote `upgrade.ps1`, synchronizes `DEVEL`, verifies dependencies, builds in isolated output, verifies artifacts, and only then replaces `dist`.

After a successful deployment, the updater derives the absolute `dist` path from the actual repository location and ensures that path is present exactly once in the current user's `PATH`. No hard-coded drive or repository path is used. The updater records the PATH entry it owns under `%LOCALAPPDATA%\WindowsTerminalFlow\path-entry.txt`; if the repository is later moved and `upgrade.cmd` is run from the new location, the previously tracked WTF PATH entry is removed and replaced with the new `dist` path without changing unrelated PATH entries. SYSTEM PATH is never modified.

An already-open command prompt cannot receive environment changes from a child process. Open a new terminal process after the first PATH registration; subsequent commands can then invoke `wtf` directly.

## Commands

```text
wtf                         Open the workspace for the current directory.
wtf "N:\WORK\GitHub"         Open the explicitly selected workspace.
wtf c                       Configure the current workspace.
wtf config                  Configure the current workspace.
wtf config "N:\WORK\GitHub" Configure the explicitly selected workspace.
wtf l                       Reload all current folders and enable their tabs.
wtf load                    Reload all current folders and enable their tabs.
wtf load "N:\WORK\GitHub"   Reload the explicitly selected workspace.
wtf setup                   Open application-wide settings.
```

Explicit workspace paths may be local paths, mapped-drive paths, or UNC paths.

## Workspace behavior

On first use, WTF discovers all direct subdirectories and sorts them by name. A known workspace uses its saved order. Closing a tab with its `×` button disables that folder for the next launch. `wtf config` provides checkboxes and drag-and-drop ordering. Newly discovered folders are appended and enabled by default. `wtf load` re-enables all folders that currently exist.

Application settings live under `%APPDATA%\WindowsTerminalFlow`. Runtime launch requests and logs live under `%LOCALAPPDATA%\WindowsTerminalFlow`. Repository location may be local, mapped, or UNC/network storage. Before crossing the UAC boundary WTF resolves mapped network drives to UNC paths. Embedded CMD sessions start from a safe local process directory and then use `pushd` for UNC workspace folders so CMD does not emit an unsupported-UNC-current-directory warning.

## Administrator mode

The default mode is elevated. The first unelevated launch stores the selected workspace, arguments, current executable path, and run identifier in a launch request and asks for UAC once to register `WindowsTerminalFlow Elevated Launcher` in Windows Task Scheduler with highest privileges.

The scheduled task does not point to the repository or to a mapped drive. It launches a small broker script stored under `%LOCALAPPDATA%\WindowsTerminalFlow`, and that broker starts the current WTF executable from the path stored in the request. This makes the elevated handoff independent of whether WTF itself lives on a local drive, a mapped network drive, or a UNC path. A stale 1.00 task is detected when it reports success but does not consume the pending request; WTF then asks for UAC once and repairs the task automatically.

The scheduled task is configured for parallel requests, allowing multiple WTF workspaces to coexist. WTF does not disable or weaken UAC globally.

## Logging

Logging modes are configured through `wtf setup`:

- `off` — no application log is written;
- `single` — the log is replaced at the beginning of each logical WTF launch and includes the complete elevation handoff for that run;
- `all` — log records are appended across launches.

The application log is `%LOCALAPPDATA%\WindowsTerminalFlow\logs\wtf.log`. Startup, elevation handoff, request lifecycle, workspace selection, folder discovery, tab creation/closing, configuration changes, and fatal errors are recorded when logging is enabled.

## Build

Requirements are handled by `upgrade.cmd` where possible. Manual build:

```text
dotnet restore WindowsTerminalFlow.sln
dotnet build WindowsTerminalFlow.sln -c Release
```

The project targets .NET 10 LTS and uses EasyWindowsTerminalControl 1.0.38 to host Windows Terminal/ConPTY sessions inside WPF tabs.

## Configuration

The application has complete CZ/EN user-interface localization, shell selection (`cmd.exe`, Windows PowerShell, or PowerShell 7), default handling for newly discovered folders, and logging modes `off`, `single`, and `all`.

## License

MIT.
