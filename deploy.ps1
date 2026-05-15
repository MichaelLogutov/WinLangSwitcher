#!/usr/bin/env pwsh
# Stop the running WinLangSwitcher task, rebuild Native AOT into
# %LOCALAPPDATA%\WinLangSwitcher\, (re)install the scheduled task only when it
# doesn't already point at our exe, and start it.
#
# Admin is needed for exactly one thing: creating/replacing the scheduled task,
# because the task has <RunLevel>HighestAvailable</RunLevel>. Everything else
# runs as the current user. We elevate that single step on demand via UAC.

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$csproj      = Join-Path $projectRoot 'src/WinLangSwitcher/WinLangSwitcher.csproj'
$installDir  = Join-Path $env:LOCALAPPDATA 'WinLangSwitcher'
$exe         = Join-Path $installDir 'WinLangSwitcher.exe'
$taskName    = 'WinLangSwitcher'

function Get-TaskExePath {
    $query = schtasks.exe /Query /TN $taskName /V /FO LIST 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    $m = $query | Select-String -Pattern '^Task To Run:\s*(.+)$'
    if (-not $m) { return $null }
    $m.Matches[0].Groups[1].Value.Trim()
}

function Test-TaskRunning {
    $query = schtasks.exe /Query /TN $taskName /FO LIST 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }
    [bool]($query | Select-String -Pattern '^Status:\s*Running' -Quiet)
}

# schtasks /End signals Task Scheduler to terminate the task; Scheduler runs as
# SYSTEM, so this works on our elevated daemon from a non-elevated shell —
# unlike Stop-Process, which would be denied.
if (Test-TaskRunning) {
    Write-Host "Stopping $taskName task..."
    schtasks.exe /End /TN $taskName | Out-Null
    Start-Sleep -Milliseconds 500
}

# Native AOT's MSBuild targets call vswhere.exe to locate the VS C++ linker.
$vsInstaller = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
if ((Test-Path (Join-Path $vsInstaller 'vswhere.exe')) -and ($env:Path -notlike "*$vsInstaller*")) {
    $env:Path = "$vsInstaller;$env:Path"
}

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir | Out-Null
}

Write-Host "Publishing Native AOT to $installDir..."
dotnet publish $csproj -c Release -r win-x64 -o $installDir
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Native AOT publish failed (exit $LASTEXITCODE). Falling back to self-contained, non-AOT build (~70 MB)."
    dotnet publish $csproj -c Release -r win-x64 -o $installDir --self-contained -p:PublishAot=false -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Self-contained publish also failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $exe)) {
    Write-Error "Expected exe not found at $exe"
    exit 1
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "Published: $exe ($sizeMb MB)"

# Only re-create the task when missing or pointing elsewhere — this is the one
# step that needs admin. Re-deploys typically skip this entirely.
$currentTaskExe = Get-TaskExePath
if ($currentTaskExe -ne $exe) {
    if ($currentTaskExe) {
        Write-Host "Task $taskName points at '$currentTaskExe', want '$exe'. Re-installing (UAC prompt incoming)..."
    } else {
        Write-Host "Task $taskName not found. Installing (UAC prompt incoming)..."
    }
    Start-Process -FilePath $exe -ArgumentList '--install' -Verb RunAs -Wait
    # Verify rather than rely on the elevated child's exit code, which the UAC
    # mediator doesn't reliably propagate back to a non-elevated parent.
    if ((Get-TaskExePath) -ne $exe) {
        Write-Error "Task install did not point $taskName at $exe (UAC declined?)."
        exit 1
    }
} else {
    Write-Host "Task $taskName already points at $exe; skipping --install."
}

Write-Host "Starting $taskName task..."
schtasks.exe /Run /TN $taskName | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "schtasks /Run failed (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host "Done."
