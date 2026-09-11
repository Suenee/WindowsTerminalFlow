# Changelog

## 1.02 - 11.09.2026

- Moved all persistent WTF-owned configuration, workspace state, launch requests, and logs into the project directory.
- Added project-local `config\config.json`, `config\workspaces.json`, `.runtime\requests`, and `logs\wtf.log` paths.
- Added `config\` and `.runtime\` to `.gitignore` so runtime state can safely coexist with a Git checkout, including network-hosted repositories.
- Removed the persistent elevated-launcher broker from `%LOCALAPPDATA%`.
- Registered the elevated Task Scheduler action directly against the current WTF executable using an elevation-safe path; mapped executable locations are converted to UNC before registration.
- Converted launch-request paths to elevation-safe form before the one-time UAC registration handoff, so a mapped repository does not depend on the drive letter being visible after elevation.
- Added migration of settings, workspace definitions, PATH ownership metadata, and logs from the earlier `%APPDATA%` / `%LOCALAPPDATA%` development layout into the project directory.
- Added cleanup of the obsolete WTF directories under `%APPDATA%` and `%LOCALAPPDATA%` after migration.
- Moved the updater-owned USER PATH metadata into the existing project `config.json`; no separate `path-entry.txt` is created.
- Kept dynamic USER PATH registration based on the actual repository `dist` location, including duplicate prevention and replacement of a previously tracked WTF entry.
- Bumped the application and package version to 1.02.

## 1.01 - 11.09.2026

- Added explicit workspace-path arguments: `wtf "path"`, `wtf config "path"`, and `wtf load "path"`.
- Made elevated launching independent of mapped-drive letters by registering a stable local broker under `%LOCALAPPDATA%\WindowsTerminalFlow`.
- Added automatic detection and one-time repair of stale elevated tasks that report launch success but fail to consume the pending request.
- Preserved compatibility with the 1.00 scheduled-launcher entry point during migration.
- Prevented embedded CMD sessions from inheriting UNC as the process current directory; UNC tabs now enter their target through `pushd` without the CMD UNC warning.
- Completed CZ/EN localization for the setup and workspace-configuration controls, including live setup-language switching.
- Implemented operational application logging for `off`, `single`, and `all` modes.
- Added run IDs and logging for startup, elevation handoff, launch requests, workspace discovery, tab creation/closing, settings, and failures.
- Added current executable and logical-run metadata to elevated launch requests so network and relocated installations can be launched safely.
- The updater now derives the current `dist` directory from its actual repository location and registers it in the current user's `PATH` without duplicate entries.
- The updater tracks the PATH entry it owns under `%LOCALAPPDATA%\WindowsTerminalFlow` so a later repository move can replace the previous WTF path safely without touching unrelated PATH entries.
- Updated documentation for local, mapped, and UNC operation.

## 1.00 - 10.09.2026

- Initial WindowsTerminalFlow implementation.
- Added directory-based terminal workspaces.
- Added persistent tab ordering and enabled/disabled state.
- Added drag-and-drop workspace configuration with checkboxes.
- Added `wtf`, `wtf c/config`, `wtf l/load`, and `wtf setup` commands.
- Added one-time UAC registration through an elevated Task Scheduler launcher.
- Added CZ/EN language setting foundation.
- Added `off`, `single`, and `all` logging modes.
- Added mapped/UNC network-drive support.
- Added fresh-folder bootstrap through `upgrade.cmd` for an empty target directory.
- Hardened upgrade handling for native stderr, branch verification, staged publish, artifact verification, and rollback-safe `dist` deployment.
- Fixed PowerShell native-command argument forwarding in the updater.
- Excluded authoritative bootstrap files from false dirty-tree detection.
- Added UTF-8 console handling for localized build output.
- Reworked deployment for mapped/UNC repositories to use network-share-safe file copying with retry and rollback.
- Added one-way temporary launcher handoff so Git cannot replace a running repository `upgrade.cmd` and then resume execution from the modified file.
- Added Windows CI build and publish verification.
