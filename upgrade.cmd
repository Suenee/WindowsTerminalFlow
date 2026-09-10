@echo off
setlocal EnableExtensions
cls

set "WTF_UPDATER_REV=1.00"
set "WTF_BRANCH=DEVEL"
set "REPO_DIR=%~dp0"
if "%REPO_DIR:~-1%"=="\" set "REPO_DIR=%REPO_DIR:~0,-1%"

pushd "%REPO_DIR%" >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Cannot access repository: %REPO_DIR%
  exit /b 1
)

if not exist "logs" mkdir "logs" >nul 2>&1
set "UPGRADE_LOG=%CD%\logs\upgrade.log"
>"%UPGRADE_LOG%" echo WindowsTerminalFlow upgrade %WTF_UPDATER_REV%

where git.exe >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Git was not found.
  >>"%UPGRADE_LOG%" echo STATUS: FAILED - phase=BOOTSTRAP
  popd
  exit /b 1
)

set "GIT_CONFIG_COUNT=1"
set "GIT_CONFIG_KEY_0=safe.directory"
set "GIT_CONFIG_VALUE_0=%CD%"

echo [SELF-UPDATE] Fetching %WTF_BRANCH%...
git fetch origin %WTF_BRANCH% >>"%UPGRADE_LOG%" 2>&1
if errorlevel 1 goto :fail_repo

set "TEMP_RUNNER=%TEMP%\wtf-upgrade-%RANDOM%-%RANDOM%.ps1"
git show origin/%WTF_BRANCH%:upgrade.ps1 > "%TEMP_RUNNER%" 2>>"%UPGRADE_LOG%"
if errorlevel 1 goto :fail_self

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%TEMP_RUNNER%" -RepoDir "%CD%" -Branch "%WTF_BRANCH%"
set "RC=%ERRORLEVEL%"
del /q "%TEMP_RUNNER%" >nul 2>&1
popd
exit /b %RC%

:fail_repo
echo [ERROR] Unable to fetch origin/%WTF_BRANCH%.
>>"%UPGRADE_LOG%" echo STATUS: FAILED - phase=REPOSITORY
popd
exit /b 1

:fail_self
echo [ERROR] Unable to load current upgrade.ps1.
>>"%UPGRADE_LOG%" echo STATUS: FAILED - phase=SELF-UPDATE
popd
exit /b 1
