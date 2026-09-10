# WindowsTerminalFlow Upgrade Protocol

WindowsTerminalFlow follows the shared upgrade rules proven in FolderHeatMap and related Wipe Codes projects. This file records the project-specific contract and the failure modes that must not be reintroduced.

## Core contract

`upgrade.cmd` is the single supported entry point for both an existing checkout and a fresh empty target directory.

A fresh target may contain only `upgrade.cmd`. The launcher must execute bootstrap logic from `%TEMP%`, remove the copied launcher from the target, clone branch `DEVEL` directly into that same directory, verify `.git` and the authoritative repository `upgrade.cmd`, then hand control to it.

The updater must work from local, mapped, and UNC/network paths. Repository data on a network drive is supported and must never be treated as an exceptional configuration.

## Architecture

```text
upgrade.cmd -> current temporary upgrade.ps1 -> restore/build/stage/verify/deploy
```

The batch launcher owns only bootstrap, repository discovery, network-path entry, self-update transport, and handoff. Build/deploy logic belongs in `upgrade.ps1`.

Never overwrite a running updater and continue executing that same file. Never clone inside a populated arbitrary directory. Never use broad `git clean -fd` or `git stash -u`.

## Git rules

The authoritative branch is `DEVEL` during development. The expected repository is `Suenee/WindowsTerminalFlow`.

Before building, verify the repository identity, reject tracked/staged local changes, fetch the explicit target branch, synchronize deterministically, and verify that `HEAD == origin/DEVEL`.

`safe.directory` must be narrowly scoped to the exact repository path for the updater process. Do not use global `safe.directory=*`.

## Network-drive rules

Use `pushd`/`popd` at CMD boundaries. Do not hard-code drive letters or workstation paths. A mapped or UNC checkout must reach the same result as a local checkout.

## Native commands

Native stderr is not failure. Git, .NET and installers may write valid progress or warnings to stderr. Capture `$LASTEXITCODE` immediately and use it as the authoritative result. Do not let `$ErrorActionPreference='Stop'` turn harmless native stderr into an upgrade failure.

## Build and deployment

Never publish directly into live `dist`.

Required lifecycle:

```text
CLEAN -> RESTORE -> BUILD -> DIST(staging) -> VERIFY -> DEPLOY -> VERIFY -> COMPLETE
```

Publish into `.upgrade-stage`, verify required artifacts there, then replace `dist`. Preserve the previous `dist` as `.upgrade-dist-backup` during deployment so a failed replacement does not intentionally destroy the last known build.

Only known generated directories may be deleted.

## Logging

Every repository upgrade replaces `logs\upgrade.log` with one diagnostic run. Bootstrap before the repository exists writes `WindowsTerminalFlow-bootstrap.log` next to the target directory.

Final repository log markers are:

```text
STATUS: SUCCESS - phase=COMPLETE
STATUS: WARNING - phase=COMPLETE
STATUS: FAILED - phase=<PHASE>
```

Console colors are presentation only: normal/default, yellow for warnings, red for errors, green for success.

## Dependencies

The updater verifies a .NET 10 SDK. If missing and `winget` is available, it may install `Microsoft.DotNet.SDK.10`, refresh the process PATH, and verify that a 10.x SDK is actually visible before continuing.

Third-party dependencies should use the newest stable, well-documented version unless the project has a documented reason to pin another version.

## Mandatory acceptance checks

Before calling the updater stable, test at least: fresh folder containing only `upgrade.cmd`; existing clean checkout; immediate second run; mapped network drive; path containing spaces; wrong/missing repository; tracked and staged local changes; harmless native stderr; missing .NET SDK; build failure before deploy; missing staged artifact; and successful publish of `dist\wtf.exe`.

GitHub Actions on `windows-latest` must restore, build, publish `win-x64`, and verify `wtf.exe` and `wtf.dll` before a development revision is treated as buildable.

## Known traps already encountered

Do not reintroduce these classes of bugs:

- assuming `.git` already exists in a new project directory;
- interpreting a Git ownership problem as permission to clone a nested repository;
- executing a batch updater while Git replaces that same file;
- LF-only executable temporary `.cmd` files;
- deep CMD/PowerShell nesting and fragile quoting;
- build output written directly into live `dist`;
- deleting the previous `dist` before the new artifacts are verified;
- treating native stderr as a fatal PowerShell error;
- failing to verify final branch/commit identity;
- relying on mapped drive visibility after elevation;
- hiding failure details by appending multiple runs into one log.

If the same class of upgrade failure is attempted three times without a solution, stop variations, preserve the failing log, roll back to the last known-good state, research authoritative/maintainer guidance, then implement a different evidence-based approach and document the new failure mode here.
