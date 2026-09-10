# WindowsTerminalFlow Upgrade Protocol

WindowsTerminalFlow follows the shared upgrade rules proven in FolderHeatMap and related Wipe Codes projects. This file records the project-specific contract and the failure modes that must not be reintroduced.

The master standard is the current `UPGRADE.md` in `Suenee/FolderHeatMap` branch `devel`. When this project-specific file and the master standard differ, the safer proven master rule wins unless this file documents an intentional exception.

## Core contract

`upgrade.cmd` is the single supported entry point for both an existing checkout and a fresh empty target directory.

A fresh target may contain only `upgrade.cmd`. Repository data may be stored on local, mapped, or UNC/network paths. Network storage is a supported first-class configuration, not an exceptional case.

An interactive `upgrade.cmd` run MUST start with one `cls`. It MUST use the standard status colors when supported: normal/default for routine progress, yellow for warning/action required, red for error, green for successful completion. `NO_COLOR` disables color only; logs remain plain text.

## Required architecture

The repository copy of `upgrade.cmd` is deliberately tiny and disposable. It MUST NOT perform repository synchronization, build work, or wait in a state where Git can replace the file and CMD later resumes reading it.

Required handoff:

```text
repository upgrade.cmd
    -> copy itself to a unique %TEMP% launcher
    -> terminal one-way handoff to that temporary launcher
        -> discover/bootstrap repository
        -> fetch explicit target branch
        -> extract current origin/DEVEL:upgrade.ps1 to %TEMP%
        -> execute temporary upgrade.ps1
            -> repository sync/build/stage/verify/deploy
```

The temporary launcher is created from the already-running local launcher before any Git operation can modify the repository copy. The temporary launcher itself is outside the repository and therefore cannot be replaced by `git reset --hard`.

Do NOT introduce a second remote temporary `.cmd` self-update layer. The authoritative self-update payload is `origin/DEVEL:upgrade.ps1`. This deliberately follows the master FolderHeatMap rule `upgrade.cmd -> current temporary upgrade.ps1` while retaining only the minimum temporary CMD shim needed to make the initial repository entry point immune to self-overwrite.

The repository launcher handoff must be terminal. No later physical line in the repository copy may be required after the temporary child starts. A Git synchronization may replace `upgrade.cmd` while the child is running.

Never clone inside a populated arbitrary directory. Never use broad `git clean -fd` or `git stash -u`.

## Fresh bootstrap

For a directory without `.git`:

1. execute bootstrap only from the temporary launcher;
2. require the target to be empty except for `upgrade.cmd`;
3. delete only that allowed bootstrap copy from the target;
4. clone branch `DEVEL` directly into the exact target directory;
5. verify `.git` and authoritative `upgrade.ps1`;
6. continue from the same temporary launcher into the normal repository path;
7. fetch `DEVEL`, extract current `upgrade.ps1` to `%TEMP%`, and execute it.

Do not hand back to the freshly cloned repository `upgrade.cmd`; that creates unnecessary CMD nesting and reopens the self-overwrite class of bugs.

## Git rules

The authoritative branch is `DEVEL` during development. The expected repository is `Suenee/WindowsTerminalFlow`.

Before building, verify repository identity, fetch the explicit target branch, inspect tracked/staged local changes, synchronize deterministically, verify the active branch, and verify `HEAD == origin/DEVEL`.

`upgrade.cmd` and `upgrade.ps1` are authoritative updater/bootstrap files and may legitimately differ locally after bootstrap or due to line-ending materialization. Exclude them from user-change detection, then synchronize them from `origin/DEVEL` as part of the deterministic reset.

`safe.directory` must be process-scoped to the exact selected repository path. Never use global `safe.directory=*`.

A Git ownership/dubious-owner failure is not evidence that `.git` is missing and must never trigger a nested clone.

## Network-drive rules

Use `pushd`/`popd` at CMD boundaries. Do not hard-code drive letters or workstation paths. Trim unnecessary trailing backslashes at interpreter boundaries.

Do not rely on renaming whole staging directories on SMB/network shares during deployment. Use verified file-by-file copy with bounded retries, backup, and rollback.

## Line endings and encoding

Repository rules must keep Windows executable scripts explicit (`*.cmd`/`*.ps1` CRLF policy in `.gitattributes`). Git blobs obtained with `git show` may still be LF-only, so any temporary `.cmd` materialized from a Git blob would require explicit CRLF normalization. The current architecture intentionally avoids materializing a remote `.cmd` at all.

The initial temporary launcher is made with ordinary file copy from the local executable `upgrade.cmd`, preserving the working-tree CRLF form.

Use UTF-8 console/output encoding so localized .NET/Git output remains readable.

Never use raw byte equality between Git blobs and CRLF working-tree scripts as a cleanliness check. Use Git semantics.

## Native commands

Native stderr is not failure. Git, .NET and installers may write valid progress or warnings to stderr. Capture `$LASTEXITCODE` immediately and use it as the authoritative result.

Avoid PowerShell parameter names that collide with automatic variables such as `$args`. Native-command wrappers use explicit names such as `$ArgumentList` and explicit named invocation.

## Build and deployment

Never publish directly into live `dist`.

Required lifecycle:

```text
CLEAN -> RESTORE -> BUILD -> DIST(staging) -> VERIFY -> DEPLOY -> VERIFY -> COMPLETE
```

Publish into `.upgrade-stage`, verify required artifacts there, then deploy by network-safe file copy. Preserve the previous `dist` as `.upgrade-dist-backup` until the new deployment passes verification. Delete only known generated directories.

## Logging

Every repository upgrade replaces `logs\upgrade.log` with one diagnostic run. Bootstrap before the repository exists writes `WindowsTerminalFlow-bootstrap.log` next to the target directory.

Final repository markers are:

```text
STATUS: SUCCESS - phase=COMPLETE
STATUS: WARNING - phase=COMPLETE
STATUS: FAILED - phase=<PHASE>
```

The process exit code and final marker must agree. Logs must remain understandable without console colors.

## Dependencies

The updater verifies a .NET 10 SDK. If missing and `winget` is available, it may install `Microsoft.DotNet.SDK.10`, refresh the current process PATH, and verify that a 10.x SDK is actually visible before continuing.

Third-party dependencies use the newest stable, well-documented version unless a documented project reason requires a pin.

## Mandatory acceptance checks

Before the updater is treated as stable, verify at least:

- fresh folder containing only `upgrade.cmd`;
- existing clean checkout;
- immediate second run (idempotence);
- mapped network drive;
- UNC path where applicable;
- path containing spaces;
- wrong/missing repository;
- dubious-owner/safe.directory handling;
- tracked and staged real local changes;
- updater-only local differences;
- harmless native stderr;
- missing .NET SDK;
- build failure before deploy;
- missing staged artifact;
- successful network-safe publish of `dist\wtf.exe`;
- previous `dist` preserved after failed deployment;
- self-update where Git replaces repository `upgrade.cmd` while the temporary launcher is running;
- readable localized UTF-8 console output.

GitHub Actions on `windows-latest` must restore, build, publish `win-x64`, and verify `wtf.exe` and `wtf.dll` before a development revision is treated as buildable.

## Known traps already encountered

Do not reintroduce these classes of bugs:

- assuming `.git` already exists in a new project directory;
- interpreting Git dubious ownership as permission to bootstrap/clone;
- letting a repository batch file continue reading after Git has replaced that same running file;
- adding a remote temporary CMD layer when temporary `upgrade.ps1` is sufficient;
- LF-only executable temporary `.cmd` files;
- deep `CMD -> PowerShell -> CMD -> PowerShell` nesting and fragile quoting;
- bootstrap updater files being misclassified as user changes;
- PowerShell native wrappers using automatic variable names such as `$args`;
- ambiguous positional argument-array binding that invokes bare `git.exe`;
- treating native stderr as fatal PowerShell failure;
- raw CRLF/LF byte comparison used as Git cleanliness logic;
- build output written directly into live `dist`;
- renaming whole staging directories as the deployment primitive on SMB/network shares;
- deleting previous `dist` before new artifacts are verified;
- failing to verify final branch/commit identity;
- relying on mapped-drive visibility after elevation;
- hiding failure details by appending multiple runs into one log;
- unreadable localized console output caused by codepage/UTF-8 mismatch.

If the same class of upgrade failure is attempted three times without a solution, stop variants, preserve the failing log, roll back to the last known-good design, research authoritative guidance, then implement a different evidence-based approach and document the failure mode here.
