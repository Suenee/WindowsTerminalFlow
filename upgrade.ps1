param(
    [Parameter(Mandatory=$true)][string]$RepoDir,
    [string]$Branch = 'DEVEL'
)

$ErrorActionPreference = 'Stop'
$UpdaterRevision = '1.03'
$RepoDir = [IO.Path]::GetFullPath($RepoDir).TrimEnd('\')
$LogDir = Join-Path $RepoDir 'logs'
$LogFile = Join-Path $LogDir 'upgrade.log'
$StageDir = Join-Path $RepoDir '.upgrade-stage'
$BackupDir = Join-Path $RepoDir '.upgrade-dist-backup'
$DistDir = Join-Path $RepoDir 'dist'
$Phase = 'BOOTSTRAP'
$WarningCount = 0
$UseColor = -not $env:NO_COLOR -and -not [Console]::IsOutputRedirected

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

try {
    Set-Content $LogFile "WindowsTerminalFlow upgrade runner $UpdaterRevision`r`nDate: $(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')`r`nRepository: $RepoDir`r`nBranch: $Branch"
    Set-Location $RepoDir

    $Phase = 'REPOSITORY'
    Write-Step '[REPOSITORY] Verifying repository identity and working tree...'
    $origin = (Invoke-Native -File 'git.exe' -ArgumentList @('remote','get-url','origin') -Quiet | Select-Object -First 1).ToString().Trim()
    if ($origin -notmatch '(?i)github\.com[:/]Suenee/WindowsTerminalFlow(?:\.git)?$') {
        Fail "Unexpected origin URL: $origin"
    }

    Invoke-Native -File 'git.exe' -ArgumentList @('fetch','origin',$Branch) | Out-Null

    # upgrade.cmd and upgrade.ps1 are authoritative bootstrap files. They may differ locally
    # after a fresh-folder bootstrap or because of line-ending materialization. They are
    # intentionally excluded from user-change detection and will be synchronized below.
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
    Write-Step '[BUILD] Building WindowsTerminalFlow 1.00...'
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
    Write-Step '[DEPLOY] Replacing dist only after staged artifacts passed verification...'
    try {
        if (Test-Path -LiteralPath $DistDir) { Move-Item -LiteralPath $DistDir -Destination $BackupDir }
        Move-Item -LiteralPath $StageDir -Destination $DistDir
    }
    catch {
        if ((-not (Test-Path -LiteralPath $DistDir)) -and (Test-Path -LiteralPath $BackupDir)) {
            try { Move-Item -LiteralPath $BackupDir -Destination $DistDir } catch {}
        }
        throw "Deployment failed without intentionally deleting the previous dist: $($_.Exception.Message)"
    }

    $Phase = 'VERIFY'
    $exe = Join-Path $DistDir 'wtf.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        if (Test-Path -LiteralPath $BackupDir) {
            Remove-Generated $DistDir
            Move-Item -LiteralPath $BackupDir -Destination $DistDir
        }
        Fail 'Deployment verification failed: dist\wtf.exe is missing.'
    }
    Remove-Generated $BackupDir

    $Phase = 'COMPLETE'
    Write-Ok "WindowsTerminalFlow 1.00 ready: $exe"
    if ($WarningCount -gt 0) { Add-Content $LogFile 'STATUS: WARNING - phase=COMPLETE' }
    else { Add-Content $LogFile 'STATUS: SUCCESS - phase=COMPLETE' }
    exit 0
}
catch {
    if (Test-Path -LiteralPath $StageDir) { try { Remove-Generated $StageDir } catch {} }
    Fail $_.Exception.Message
}
