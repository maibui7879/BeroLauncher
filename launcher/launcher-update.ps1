param(
    [Parameter(Mandatory = $true)][string]$Stage,
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$Executable
)

$ErrorActionPreference = 'Stop'
$log = Join-Path $env:LOCALAPPDATA 'StellaViet\launcher-update.log'
$backup = Join-Path $env:TEMP ('BeroLauncher-backup-' + [guid]::NewGuid().ToString('N'))
$scriptPath = $MyInvocation.MyCommand.Path
$updated = $false

function Write-UpdateLog([string]$Message) {
    try {
        Add-Content -LiteralPath $log -Value "$(Get-Date -Format o) $Message"
    }
    catch { }
}

try {
    $deadline = (Get-Date).AddMinutes(5)
    while (Get-Process -Name BeroLauncher -ErrorAction SilentlyContinue) {
        if ((Get-Date) -ge $deadline) {
            throw 'Launcher or language-session guardian did not exit in time.'
        }
        Start-Sleep -Seconds 1
    }

    if (!(Test-Path -LiteralPath (Join-Path $Stage 'BeroLauncher.exe'))) {
        throw 'Staged update is missing BeroLauncher.exe.'
    }
    New-Item -ItemType Directory -Path $backup | Out-Null
    if (Test-Path -LiteralPath $InstallDir) {
        Copy-Item -Path (Join-Path $InstallDir '*') -Destination $backup -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $InstallDir | Out-Null
    }

    try {
        Copy-Item -Path (Join-Path $Stage '*') -Destination $InstallDir -Recurse -Force
        Start-Process -FilePath $Executable
        $updated = $true
    }
    catch {
        if (Test-Path -LiteralPath $backup) {
            Copy-Item -Path (Join-Path $backup '*') -Destination $InstallDir -Recurse -Force
        }
        throw
    }
}
catch {
    Write-UpdateLog $_.Exception.Message
    if (!(Get-Process -Name BeroLauncher -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $Executable)) {
        Start-Process -FilePath $Executable
    }
}
finally {
    if ($updated) {
        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Split-Path -Parent $Stage) -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
}