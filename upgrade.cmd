@echo off
cls
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

set "WTF_UPDATER_REV=1.07-bootstrap"
set "WTF_BRANCH=DEVEL"
set "WTF_REPO_URL=https://github.com/Suenee/WindowsTerminalFlow.git"

rem Backward-compatible internal entry points used by older WTF launchers.
rem Never remove an internal handoff option until all supported older launchers
rem can reach a newer authoritative runner without using it.
if /I "%~1"=="--temp-runner" goto :temp_runner
if /I "%~1"=="--repository-internal" goto :legacy_repository_internal
if /I "%~1"=="--bootstrap-internal" goto :legacy_bootstrap_internal
if not "%~1"=="" (
  call :msg red "ERROR: Unknown upgrade option: %~1"
  exit /b 2
)

rem The repository copy is never allowed to perform Git synchronization or wait for
rem a child that may overwrite it. It only materializes a stable temporary launcher,
rem transfers control, then exits from the same already-parsed physical line.
set "REPO_DIR=%~dp0"
if "!REPO_DIR:~-1!"=="\" set "REPO_DIR=!REPO_DIR:~0,-1!"
set "TEMP_LAUNCHER=%TEMP%\wtf-bootstrap-%RANDOM%-%RANDOM%.cmd"
copy /y "%~f0" "!TEMP_LAUNCHER!" >nul
if errorlevel 1 (
  call :msg red "ERROR: Could not create temporary upgrade launcher."
  exit /b 1
)

rem IMPORTANT: keep the repository path in the environment as well as argv.
rem Older launchers and a self-replaced batch can lose argv after Git rewrites the
rem working-tree copy, while environment state survives the child handoff.
set "WTF_REPO_DIR=!REPO_DIR!"

rem IMPORTANT: keep this as one physical line. The temporary child may replace this
rem repository file. No later line from this file may be required for completion.
call "!TEMP_LAUNCHER!" --temp-runner "!REPO_DIR!" & set "WTF_RC=!ERRORLEVEL!" & del /q "!TEMP_LAUNCHER!" >nul 2>&1 & exit /b !WTF_RC!

:legacy_repository_internal
rem WTF 1.03/1.04 launchers call the freshly fetched launcher this way.
rem Treat it exactly as the current temporary-runner entry point.
set "REPO_DIR=%~2"
goto :temp_runner_common

:legacy_bootstrap_internal
rem Older bootstrap launchers passed the target folder as argument 2.
rem The caller itself already runs from a temporary copy, so continue through the
rem same safe bootstrap/repository discovery path rather than chaining another CMD.
set "REPO_DIR=%~2"
goto :temp_runner_common

:temp_runner
set "REPO_DIR=%~2"

:temp_runner_common
if not defined REPO_DIR if defined WTF_REPO_DIR set "REPO_DIR=!WTF_REPO_DIR!"
if not defined REPO_DIR (
  call :msg red "ERROR: Upgrade repository path is empty."
  exit /b 2
)
if "!REPO_DIR:~-1!"=="\" set "REPO_DIR=!REPO_DIR:~0,-1!"
set "WTF_REPO_DIR=!REPO_DIR!"

pushd "!REPO_DIR!" >nul 2>&1
if errorlevel 1 (
  call :msg red "ERROR: Cannot access project directory: !REPO_DIR!"
  exit /b 1
)
set "ACTIVE_DIR=%CD%"

where git.exe >nul 2>nul
if errorlevel 1 (
  call :msg red "ERROR: Git was not found in PATH. Install Git for Windows, then run upgrade.cmd again."
  popd
  exit /b 1
)

set "GIT_CONFIG_COUNT=1"
set "GIT_CONFIG_KEY_0=safe.directory"
set "GIT_CONFIG_VALUE_0=!ACTIVE_DIR!"

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 goto :bootstrap_from_temp

:run_repository
if not exist "logs" mkdir "logs" >nul 2>&1
set "UPGRADE_LOG=!ACTIVE_DIR!\logs\upgrade.log"
>"!UPGRADE_LOG!" echo WindowsTerminalFlow upgrade !WTF_UPDATER_REV!
>>"!UPGRADE_LOG!" echo Repository source: !REPO_DIR!
>>"!UPGRADE_LOG!" echo Active path: !ACTIVE_DIR!
>>"!UPGRADE_LOG!" echo Branch: !WTF_BRANCH!

call :msg cyan "[SELF-UPDATE] Fetching !WTF_BRANCH!..."
git fetch origin !WTF_BRANCH! >>"!UPGRADE_LOG!" 2>&1
if errorlevel 1 goto :fail_repo

set "TEMP_RUNNER=%TEMP%\wtf-upgrade-%RANDOM%-%RANDOM%.ps1"
git show origin/!WTF_BRANCH!:upgrade.ps1 > "!TEMP_RUNNER!" 2>>"!UPGRADE_LOG!"
if errorlevel 1 goto :fail_self

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "!TEMP_RUNNER!" -RepoDir "!ACTIVE_DIR!" -Branch "!WTF_BRANCH!"
set "RC=!ERRORLEVEL!"
del /q "!TEMP_RUNNER!" >nul 2>&1
popd
exit /b !RC!

:bootstrap_from_temp
set "BOOTSTRAP_TARGET=!REPO_DIR!"
for %%I in ("!BOOTSTRAP_TARGET!\..") do set "BOOTSTRAP_PARENT=%%~fI"
set "BOOTSTRAP_LOG=!BOOTSTRAP_PARENT!\WindowsTerminalFlow-bootstrap.log"

call :msg cyan "WindowsTerminalFlow repository not found. Starting fresh-folder bootstrap..."
>"!BOOTSTRAP_LOG!" echo WindowsTerminalFlow bootstrap !WTF_UPDATER_REV!
>>"!BOOTSTRAP_LOG!" echo Target: !BOOTSTRAP_TARGET!
>>"!BOOTSTRAP_LOG!" echo Branch: !WTF_BRANCH!

set "BOOTSTRAP_EXTRA=0"
for /f "delims=" %%F in ('dir /b /a "!BOOTSTRAP_TARGET!" 2^>nul') do (
  if /I not "%%F"=="upgrade.cmd" set "BOOTSTRAP_EXTRA=1"
)
if "!BOOTSTRAP_EXTRA!"=="1" (
  call :msg red "ERROR: Bootstrap target must be empty except for upgrade.cmd."
  >>"!BOOTSTRAP_LOG!" echo STATUS: FAILED - phase=BOOTSTRAP-SAFETY
  popd
  exit /b 1
)

popd
del /q "!BOOTSTRAP_TARGET!\upgrade.cmd" >nul 2>&1
call :msg cyan "[BOOTSTRAP] Cloning !WTF_BRANCH! into: !BOOTSTRAP_TARGET!"
git clone --branch !WTF_BRANCH! --single-branch "!WTF_REPO_URL!" "!BOOTSTRAP_TARGET!" >>"!BOOTSTRAP_LOG!" 2>&1
if errorlevel 1 (
  call :msg red "ERROR: Git clone failed. See WindowsTerminalFlow-bootstrap.log."
  >>"!BOOTSTRAP_LOG!" echo STATUS: FAILED - phase=BOOTSTRAP-CLONE
  exit /b 1
)
if not exist "!BOOTSTRAP_TARGET!\.git" (
  call :msg red "ERROR: Clone completed, but .git is missing."
  >>"!BOOTSTRAP_LOG!" echo STATUS: FAILED - phase=BOOTSTRAP-VERIFY
  exit /b 1
)
if not exist "!BOOTSTRAP_TARGET!\upgrade.ps1" (
  call :msg red "ERROR: Clone completed, but authoritative upgrade.ps1 is missing."
  >>"!BOOTSTRAP_LOG!" echo STATUS: FAILED - phase=BOOTSTRAP-VERIFY
  exit /b 1
)

pushd "!BOOTSTRAP_TARGET!" >nul 2>&1
if errorlevel 1 (
  call :msg red "ERROR: Cannot enter cloned repository."
  >>"!BOOTSTRAP_LOG!" echo STATUS: FAILED - phase=BOOTSTRAP-VERIFY
  exit /b 1
)
set "REPO_DIR=!BOOTSTRAP_TARGET!"
set "WTF_REPO_DIR=!REPO_DIR!"
set "ACTIVE_DIR=%CD%"
set "GIT_CONFIG_COUNT=1"
set "GIT_CONFIG_KEY_0=safe.directory"
set "GIT_CONFIG_VALUE_0=!ACTIVE_DIR!"
>>"!BOOTSTRAP_LOG!" echo STATUS: CLONE OK - handing off to authoritative temporary upgrade.ps1
goto :run_repository

:fail_repo
call :msg red "ERROR: Unable to fetch origin/!WTF_BRANCH!."
>>"!UPGRADE_LOG!" echo STATUS: FAILED - phase=SELF-UPDATE
popd
exit /b 1

:fail_self
call :msg red "ERROR: Unable to load current upgrade.ps1."
>>"!UPGRADE_LOG!" echo STATUS: FAILED - phase=SELF-UPDATE
if defined TEMP_RUNNER del /q "!TEMP_RUNNER!" >nul 2>&1
popd
exit /b 1

:msg
if defined NO_COLOR (
  echo %~2
  exit /b 0
)
set "WTF_MSG=%~2"
powershell.exe -NoProfile -Command "Write-Host $env:WTF_MSG -ForegroundColor %~1" 2>nul
set "WTF_MSG="
if errorlevel 1 echo %~2
exit /b 0
