param(
    [Parameter(Mandatory=$true)][string]$RepoDir,
    [string]$Branch = 'DEVEL'
)

$ErrorActionPreference = 'Stop'
$UpdaterRevision = '1.00'
$RepoDir = [IO.Path]::GetFullPath($RepoDir).TrimEnd('\')
$LogDir = Join-Path $RepoDir 'logs'
$LogFile = Join-Path $LogDir 'upgrade.log'
$Phase = 'BOOTSTRAP'
$WarningCount = 0

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Step([string]$Text) { Write-Host $Text -ForegroundColor Gray; Add-Content $LogFile $Text }
function Write-Ok([string]$Text) { Write-Host $Text -ForegroundColor Green; Add-Content $LogFile $Text }
function Write-Warn([string]$Text) { $script:WarningCount++; Write-Host $Text -ForegroundColor Yellow; Add-Content $LogFile "WARNING: $Text" }
function Fail([string]$Text) { Write-Host "ERROR: $Text" -ForegroundColor Red; Add-Content $LogFile "ERROR: $Text"; Add-Content $LogFile "STATUS: FAILED - phase=$script:Phase"; exit 1 }
function Native([string]$File, [string[]]$Args) {
    & $File @Args 2>&1 | ForEach-Object { Add-Content $LogFile $_; Write-Host $_ }
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw "$File exited with code $code" }
}

try {
    Set-Content $LogFile "WindowsTerminalFlow upgrade runner $UpdaterRevision`r`nDate: $(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')`r`nRepository: $RepoDir`r`nBranch: $Branch"
    Set-Location $RepoDir

    $Phase = 'REPOSITORY'
    Write-Step '[REPOSITORY] Verifying working tree...'
    $dirty = (& git status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) { Fail 'Git status failed.' }
    if ($dirty) { Fail 'Tracked local changes detected. Commit or revert them before upgrade.' }
    Native git @('fetch','origin',$Branch)
    Native git @('checkout',$Branch)
    Native git @('reset','--hard',"origin/$Branch")
    $head = (& git rev-parse HEAD).Trim()
    Add-Content $LogFile "Synchronized commit: $head"

    $Phase = 'DEPENDENCIES'
    Write-Step '[DEPENDENCIES] Checking .NET 10 SDK...'
    $dotnetOk = $false
    try { $v = (& dotnet --version).Trim(); $dotnetOk = $v.StartsWith('10.'); Add-Content $LogFile ".NET SDK: $v" } catch {}
    if (-not $dotnetOk) {
        if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) { Fail '.NET 10 SDK is missing and winget is unavailable.' }
        Write-Step '[DEPENDENCIES] Installing .NET 10 SDK...'
        Native winget @('install','--id','Microsoft.DotNet.SDK.10','--exact','--accept-source-agreements','--accept-package-agreements','--silent')
        $env:PATH = [Environment]::GetEnvironmentVariable('PATH','Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH','User')
        $v = (& dotnet --version).Trim()
        if (-not $v.StartsWith('10.')) { Fail 'Installed .NET SDK is not version 10.x.' }
    }

    $Phase = 'CLEAN'
    foreach ($dir in @('src\WindowsTerminalFlow\bin','src\WindowsTerminalFlow\obj','dist')) {
        $path = Join-Path $RepoDir $dir
        if (Test-Path $path) { Remove-Item -Recurse -Force $path }
    }

    $Phase = 'RESTORE'
    Write-Step '[RESTORE] Restoring NuGet packages...'
    Native dotnet @('restore','WindowsTerminalFlow.sln')

    $Phase = 'BUILD'
    Write-Step '[BUILD] Building WindowsTerminalFlow 1.00...'
    Native dotnet @('build','WindowsTerminalFlow.sln','-c','Release','--no-restore')

    $Phase = 'DIST'
    Write-Step '[DIST] Publishing...'
    Native dotnet @('publish','src\WindowsTerminalFlow\WindowsTerminalFlow.csproj','-c','Release','--no-build','-r','win-x64','--self-contained','false','-o','dist')

    $Phase = 'VERIFY'
    $exe = Join-Path $RepoDir 'dist\wtf.exe'
    if (-not (Test-Path $exe)) { Fail 'Required file is missing: dist\wtf.exe' }

    $Phase = 'COMPLETE'
    Write-Ok "WindowsTerminalFlow 1.00 ready: $exe"
    if ($WarningCount -gt 0) { Add-Content $LogFile 'STATUS: WARNING - phase=COMPLETE' }
    else { Add-Content $LogFile 'STATUS: SUCCESS - phase=COMPLETE' }
    exit 0
}
catch {
    Fail $_.Exception.Message
}
