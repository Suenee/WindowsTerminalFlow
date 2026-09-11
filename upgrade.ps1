param(
    [Parameter(Mandatory=$true)][string]$RepoDir,
    [string]$Branch = 'DEVEL'
)

$ErrorActionPreference = 'Stop'
$UpdaterRevision = '1.06'
$RepoDir = [IO.Path]::GetFullPath($RepoDir).TrimEnd('\')
$LogDir = Join-Path $RepoDir 'logs'
$LogFile = Join-Path $LogDir 'upgrade.log'
$StageDir = Join-Path $RepoDir '.upgrade-stage'
$BackupDir = Join-Path $RepoDir '.upgrade-dist-backup'
$DistDir = Join-Path $RepoDir 'dist'
$ConfigDir = Join-Path $RepoDir 'config'
$ConfigFile = Join-Path $ConfigDir 'config.json'
$WorkspacesFile = Join-Path $ConfigDir 'workspaces.json'
$LegacyRoamingDir = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'WindowsTerminalFlow'
$LegacyLocalDir = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'WindowsTerminalFlow'
$Phase = 'BOOTSTRAP'
$WarningCount = 0
$UseColor = -not $env:NO_COLOR -and -not [Console]::IsOutputRedirected

$Utf8 = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $Utf8
$OutputEncoding = $Utf8
try { & chcp.com 65001 *> $null } catch { }

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Out-Console([string]$Text, [ConsoleColor]$Color) {
    if ($script:UseColor) { Write-Host $Text -ForegroundColor $Color } else { Write-Host $Text }
}
function Write-Step([string]$Text) { Out-Console $Text Gray; Add-Content $LogFile $Text }
function Write-Ok([string]$Text) { Out-Console $Text Green; Add-Content $LogFile $Text }
function Write-Warn([string]$Text) { $script:WarningCount++; Out-Console $Text Yellow; Add-Content $LogFile "WARNING: $Text" }
function Fail([string]$Text) {
    Out-Console "ERROR: $Text" Red
    Add-Content $LogFile "ERROR: $Text"
    Add-Content $LogFile "STATUS: FAILED - phase=$script:Phase"
    exit 1
}
function Invoke-Native {
    param(
        [Parameter(Mandatory=$true)][string]$File,
        [Parameter(Mandatory=$true)][string[]]$ArgumentList,
        [switch]$Quiet
    )
    $oldPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $File @ArgumentList 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }
    foreach ($item in @($output)) {
        $line = $item.ToString()
        Add-Content $LogFile $line
        if (-not $Quiet) {
            if ($line -match '(?i)\b(error|failed|fatal)\b') { Out-Console $line Red }
            elseif ($line -match '(?i)\bwarning\b') { Out-Console $line Yellow }
            else { Out-Console $line Gray }
        }
    }
    if ($code -ne 0) { throw "$File exited with code $code" }
    return @($output)
}
function Invoke-GitQuietStatus([string[]]$ArgumentList) {
    $oldPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & git.exe @ArgumentList *> $null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }
}
function Remove-Generated([string]$Path) {
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
}
function Copy-FileWithRetry {
    param(
        [Parameter(Mandatory=$true)][string]$Source,
        [Parameter(Mandatory=$true)][string]$Destination,
        [int]$Attempts = 20,
        [int]$DelayMs = 500
    )
    $parent = Split-Path -Parent $Destination
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -ge $Attempts) {
                throw "Could not copy '$Source' to '$Destination' after $Attempts attempts. Last error: $($_.Exception.Message)"
            }
            if ($attempt -eq 1) { Write-Warn "Deployment target is temporarily unavailable; retrying: $Destination" }
            Start-Sleep -Milliseconds $DelayMs
        }
    }
}
function Copy-TreeWithRetry {
    param(
        [Parameter(Mandatory=$true)][string]$Source,
        [Parameter(Mandatory=$true)][string]$Destination
    )
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $sourceRoot = [IO.Path]::GetFullPath($Source).TrimEnd('\')
    foreach ($dir in Get-ChildItem -LiteralPath $sourceRoot -Directory -Recurse) {
        $relative = $dir.FullName.Substring($sourceRoot.Length).TrimStart('\')
        New-Item -ItemType Directory -Force -Path (Join-Path $Destination $relative) | Out-Null
    }
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -File -Recurse) {
        $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart('\')
        Copy-FileWithRetry -Source $file.FullName -Destination (Join-Path $Destination $relative)
    }
}
function Normalize-PathEntry([string]$Entry) {
    if ([string]::IsNullOrWhiteSpace($Entry)) { return '' }
    $value = $Entry.Trim().Trim('"')
    try {
        $expanded = [Environment]::ExpandEnvironmentVariables($value)
        if ([IO.Path]::IsPathRooted($expanded)) { $value = [IO.Path]::GetFullPath($expanded) }
    }
    catch { }
    $root = $null
    try { $root = [IO.Path]::GetPathRoot($value) } catch { }
    if (-not [string]::IsNullOrEmpty($root) -and $value.Length -gt $root.Length) { $value = $value.TrimEnd('\','/') }
    return $value
}
function Read-ProjectConfig {
    if (-not (Test-Path -LiteralPath $script:ConfigFile)) { return [ordered]@{} }
    try {
        $raw = Get-Content -LiteralPath $script:ConfigFile -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($raw)) { return [ordered]@{} }
        $obj = $raw | ConvertFrom-Json
        $map = [ordered]@{}
        foreach ($p in $obj.PSObject.Properties) { $map[$p.Name] = $p.Value }
        return $map
    }
    catch {
        throw "Unable to read project config '$script:ConfigFile': $($_.Exception.Message)"
    }
}
function Write-ProjectConfig($Config) {
    New-Item -ItemType Directory -Force -Path $script:ConfigDir | Out-Null
    $json = [pscustomobject]$Config | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($script:ConfigFile, $json + [Environment]::NewLine, $script:Utf8)
}
function Migrate-LegacyState {
    New-Item -ItemType Directory -Force -Path $script:ConfigDir | Out-Null

    $legacyConfig = Join-Path $script:LegacyRoamingDir 'config.json'
    $legacyWorkspaces = Join-Path $script:LegacyRoamingDir 'workspaces.json'
    $legacyPathState = Join-Path $script:LegacyLocalDir 'path-entry.txt'
    $legacyLog = Join-Path $script:LegacyLocalDir 'logs\wtf.log'

    if (-not (Test-Path -LiteralPath $script:ConfigFile) -and (Test-Path -LiteralPath $legacyConfig)) {
        Copy-FileWithRetry -Source $legacyConfig -Destination $script:ConfigFile
        Write-Step '[MIGRATE] Moved application settings from legacy APPDATA storage into project config.'
    }
    if (-not (Test-Path -LiteralPath $script:WorkspacesFile) -and (Test-Path -LiteralPath $legacyWorkspaces)) {
        Copy-FileWithRetry -Source $legacyWorkspaces -Destination $script:WorkspacesFile
        Write-Step '[MIGRATE] Moved workspace definitions from legacy APPDATA storage into project config.'
    }

    $config = Read-ProjectConfig
    if ((-not $config.Contains('ManagedPathEntry') -or [string]::IsNullOrWhiteSpace([string]$config['ManagedPathEntry'])) -and (Test-Path -LiteralPath $legacyPathState)) {
        try {
            $legacyManagedPath = (Get-Content -LiteralPath $legacyPathState -Raw -Encoding UTF8).Trim()
            if (-not [string]::IsNullOrWhiteSpace($legacyManagedPath)) {
                $config['ManagedPathEntry'] = $legacyManagedPath
                Write-ProjectConfig $config
                Write-Step '[MIGRATE] Moved managed PATH metadata into project config.'
            }
        }
        catch { Write-Warn "Could not migrate legacy PATH metadata: $($_.Exception.Message)" }
    }

    if ((Test-Path -LiteralPath $legacyLog) -and -not (Test-Path -LiteralPath (Join-Path $script:LogDir 'wtf.log'))) {
        Copy-FileWithRetry -Source $legacyLog -Destination (Join-Path $script:LogDir 'wtf.log')
    }

    foreach ($legacyDir in @($script:LegacyRoamingDir, $script:LegacyLocalDir)) {
        if (Test-Path -LiteralPath $legacyDir) {
            try {
                Remove-Item -LiteralPath $legacyDir -Recurse -Force -ErrorAction Stop
                Write-Step "[MIGRATE] Removed legacy C: storage: $legacyDir"
            }
            catch { Write-Warn "Unable to remove legacy WTF storage '$legacyDir': $($_.Exception.Message)" }
        }
    }
}
function Ensure-UserPathEntry([string]$TargetPath) {
    $target = [IO.Path]::GetFullPath($TargetPath).TrimEnd('\')
    $targetNormalized = Normalize-PathEntry $target
    $config = Read-ProjectConfig
    $previousTracked = if ($config.Contains('ManagedPathEntry')) { [string]$config['ManagedPathEntry'] } else { '' }
    $previousNormalized = if ([string]::IsNullOrWhiteSpace($previousTracked)) { '' } else { Normalize-PathEntry $previousTracked }

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($null -eq $userPath) { $userPath = '' }
    $entries = if ($userPath.Length -eq 0) { @() } else { @($userPath -split ';') }
    $newEntries = [System.Collections.Generic.List[string]]::new()
    $foundTarget = $false
    $removedDuplicate = $false
    $removedPrevious = $false

    foreach ($entry in $entries) {
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        $normalized = Normalize-PathEntry $entry
        if ($normalized.Equals($targetNormalized, [StringComparison]::OrdinalIgnoreCase)) {
            if (-not $foundTarget) { $newEntries.Add($entry.Trim()); $foundTarget = $true }
            else { $removedDuplicate = $true }
            continue
        }
        if ($previousNormalized -and -not $previousNormalized.Equals($targetNormalized, [StringComparison]::OrdinalIgnoreCase) -and $normalized.Equals($previousNormalized, [StringComparison]::OrdinalIgnoreCase)) {
            $removedPrevious = $true
            continue
        }
        $newEntries.Add($entry.Trim())
    }

    if (-not $foundTarget) { $newEntries.Add($target) }
    $newUserPath = [string]::Join(';', $newEntries)
    if ($newUserPath -ne $userPath) { [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User') }

    $config['ManagedPathEntry'] = $target
    Write-ProjectConfig $config

    $verifyPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $verifyFound = $false
    foreach ($entry in @($verifyPath -split ';')) {
        if ((Normalize-PathEntry $entry).Equals($targetNormalized, [StringComparison]::OrdinalIgnoreCase)) { $verifyFound = $true; break }
    }
    if (-not $verifyFound) { throw "Failed to register '$target' in USER PATH." }

    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $env:PATH = if ([string]::IsNullOrWhiteSpace($machinePath)) { $verifyPath } else { "$machinePath;$verifyPath" }

    if (-not $foundTarget) { Write-Ok "[PATH] Added WindowsTerminalFlow to USER PATH: $target" }
    elseif ($removedDuplicate) { Write-Ok "[PATH] Removed duplicate WindowsTerminalFlow USER PATH entries: $target" }
    else { Write-Step "[PATH] WindowsTerminalFlow already present in USER PATH: $target" }
    if ($removedPrevious) { Write-Step "[PATH] Removed previous tracked WindowsTerminalFlow path: $previousTracked" }
}

try {
    Set-Content $LogFile "WindowsTerminalFlow upgrade runner $UpdaterRevision`r`nDate: $(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')`r`nRepository: $RepoDir`r`nBranch: $Branch"
    Set-Location $RepoDir

    $Phase = 'REPOSITORY'
    Write-Step '[REPOSITORY] Verifying repository identity and working tree...'
    $origin = (Invoke-Native -File 'git.exe' -ArgumentList @('remote','get-url','origin') -Quiet | Select-Object -First 1).ToString().Trim()
    if ($origin -notmatch '(?i)github\.com[:/]Suenee/WindowsTerminalFlow(?:\.git)?$') { Fail "Unexpected origin URL: $origin" }
    Invoke-Native -File 'git.exe' -ArgumentList @('fetch','origin',$Branch) | Out-Null

    $worktreeRc = Invoke-GitQuietStatus @('diff','--quiet','--ignore-space-at-eol','--ignore-submodules','--','.',':(exclude)upgrade.cmd',':(exclude)upgrade.ps1')
    if ($worktreeRc -gt 1) { Fail "Git worktree check failed with exit code $worktreeRc." }
    $stagedRc = Invoke-GitQuietStatus @('diff','--cached','--quiet','--ignore-submodules','--','.',':(exclude)upgrade.cmd',':(exclude)upgrade.ps1')
    if ($stagedRc -gt 1) { Fail "Git staged-change check failed with exit code $stagedRc." }
    if ($worktreeRc -eq 1 -or $stagedRc -eq 1) {
        $changes = Invoke-Native -File 'git.exe' -ArgumentList @('status','--porcelain=v1','--untracked-files=no','--','.',':(exclude)upgrade.cmd',':(exclude)upgrade.ps1') -Quiet
        foreach ($change in @($changes)) { Add-Content $LogFile ("Local change: " + $change.ToString()) }
        Fail 'Tracked or staged local changes outside updater bootstrap files detected. Commit or revert them before upgrade.'
    }

    Write-Step '[REPOSITORY] Synchronizing authoritative updater and tracked tree to origin/DEVEL...'
    Invoke-Native -File 'git.exe' -ArgumentList @('checkout',$Branch) | Out-Null
    Invoke-Native -File 'git.exe' -ArgumentList @('reset','--hard',"origin/$Branch") | Out-Null
    $head = (Invoke-Native -File 'git.exe' -ArgumentList @('rev-parse','HEAD') -Quiet | Select-Object -First 1).ToString().Trim()
    $remoteHead = (Invoke-Native -File 'git.exe' -ArgumentList @('rev-parse',"origin/$Branch") -Quiet | Select-Object -First 1).ToString().Trim()
    if ($head -ne $remoteHead) { Fail "HEAD does not match origin/$Branch after synchronization." }
    Add-Content $LogFile "Synchronized commit: $head"

    $Phase = 'MIGRATE'
    Write-Step '[MIGRATE] Ensuring all persistent WTF data lives inside the project directory...'
    Migrate-LegacyState

    $Phase = 'DEPENDENCIES'
    Write-Step '[DEPENDENCIES] Checking .NET 10 SDK...'
    $dotnetOk = $false
    if (Get-Command dotnet.exe -ErrorAction SilentlyContinue) {
        $sdks = Invoke-Native -File 'dotnet.exe' -ArgumentList @('--list-sdks') -Quiet
        $dotnetOk = @($sdks | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^10\.' }).Count -gt 0
        foreach ($sdk in @($sdks)) { Add-Content $LogFile ("SDK: " + $sdk.ToString()) }
    }
    if (-not $dotnetOk) {
        if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) { Fail '.NET 10 SDK is missing and winget is unavailable.' }
        Write-Step '[DEPENDENCIES] Installing .NET 10 SDK...'
        Invoke-Native -File 'winget.exe' -ArgumentList @('install','--id','Microsoft.DotNet.SDK.10','--exact','--accept-source-agreements','--accept-package-agreements','--silent') | Out-Null
        $env:PATH = [Environment]::GetEnvironmentVariable('PATH','Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH','User')
        if (-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)) { Fail '.NET SDK installation completed but dotnet.exe is still unavailable.' }
        $sdks = Invoke-Native -File 'dotnet.exe' -ArgumentList @('--list-sdks') -Quiet
        $dotnetOk = @($sdks | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^10\.' }).Count -gt 0
        if (-not $dotnetOk) { Fail 'Installed .NET SDK does not include version 10.x.' }
    }

    $Phase = 'CLEAN'
    Write-Step '[CLEAN] Removing known generated build state...'
    Remove-Generated (Join-Path $RepoDir 'src\WindowsTerminalFlow\bin')
    Remove-Generated (Join-Path $RepoDir 'src\WindowsTerminalFlow\obj')
    Remove-Generated $StageDir
    Remove-Generated $BackupDir

    $Phase = 'RESTORE'
    Write-Step '[RESTORE] Restoring NuGet packages...'
    Invoke-Native -File 'dotnet.exe' -ArgumentList @('restore','WindowsTerminalFlow.sln') | Out-Null

    $Phase = 'BUILD'
    Write-Step '[BUILD] Building WindowsTerminalFlow 1.02...'
    Invoke-Native -File 'dotnet.exe' -ArgumentList @('build','WindowsTerminalFlow.sln','-c','Release','--no-restore') | Out-Null

    $Phase = 'DIST'
    Write-Step '[DIST] Publishing win-x64 into isolated staging directory...'
    Invoke-Native -File 'dotnet.exe' -ArgumentList @('publish','src\WindowsTerminalFlow\WindowsTerminalFlow.csproj','-c','Release','-r','win-x64','--self-contained','false','--no-build','-o',$StageDir) | Out-Null

    $Phase = 'VERIFY'
    $stagedExe = Join-Path $StageDir 'wtf.exe'
    if (-not (Test-Path -LiteralPath $stagedExe)) { Fail 'Required staged file is missing: wtf.exe' }
    $stagedDll = Join-Path $StageDir 'wtf.dll'
    if (-not (Test-Path -LiteralPath $stagedDll)) { Fail 'Required staged file is missing: wtf.dll' }

    $Phase = 'DEPLOY'
    Write-Step '[DEPLOY] Copying verified staged artifacts into dist with network-share-safe retries...'
    $hadPreviousDist = Test-Path -LiteralPath $DistDir
    if ($hadPreviousDist) {
        Write-Step '[DEPLOY] Backing up current dist before replacement...'
        Copy-TreeWithRetry -Source $DistDir -Destination $BackupDir
    }
    try {
        Remove-Generated $DistDir
        New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
        Copy-TreeWithRetry -Source $StageDir -Destination $DistDir
    }
    catch {
        $deployError = $_.Exception.Message
        try { Remove-Generated $DistDir } catch {}
        if ($hadPreviousDist -and (Test-Path -LiteralPath $BackupDir)) {
            try {
                New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
                Copy-TreeWithRetry -Source $BackupDir -Destination $DistDir
                Write-Warn 'Deployment failed; previous dist was restored from backup.'
            }
            catch { throw "Deployment failed and rollback also failed. Deployment error: $deployError; rollback error: $($_.Exception.Message)" }
        }
        throw "Deployment failed: $deployError"
    }

    $Phase = 'VERIFY'
    $exe = Join-Path $DistDir 'wtf.exe'
    $dll = Join-Path $DistDir 'wtf.dll'
    if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath $dll)) { Fail 'Deployment verification failed: required dist artifacts are missing.' }

    $Phase = 'PATH'
    Write-Step '[PATH] Ensuring the current dist directory is registered in USER PATH...'
    Ensure-UserPathEntry -TargetPath $DistDir

    Remove-Generated $StageDir
    Remove-Generated $BackupDir

    $Phase = 'COMPLETE'
    Write-Ok "WindowsTerminalFlow 1.02 ready: $exe"
    Write-Step '[PATH] New terminal processes can invoke WTF as: wtf'
    if ($WarningCount -gt 0) { Add-Content $LogFile 'STATUS: WARNING - phase=COMPLETE' }
    else { Add-Content $LogFile 'STATUS: SUCCESS - phase=COMPLETE' }
    exit 0
}
catch {
    if (Test-Path -LiteralPath $StageDir) { try { Remove-Generated $StageDir } catch {} }
    Fail $_.Exception.Message
}
