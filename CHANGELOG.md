# Changelog

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
