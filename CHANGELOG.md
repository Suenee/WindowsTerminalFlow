# Changelog

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
