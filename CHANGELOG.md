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
- Added Windows CI build and publish verification.
