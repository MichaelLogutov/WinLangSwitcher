#!/usr/bin/env pwsh
# Download the latest WinLangSwitcher release, deploy to %LOCALAPPDATA%\WinLangSwitcher\,
# (re)install the scheduled task only when needed, and start it.
#
# Admin is needed for exactly one thing: creating/replacing the scheduled task,
# because the task has <RunLevel>HighestAvailable</RunLevel>. Everything else
# runs as the current user. We elevate that single step on demand via UAC.

[CmdletBinding()]
param(
    # Override the download URL (e.g. pin to a specific release tag).
    [string]$Url = 'https://github.com/MichaelLogutov/WinLangSwitcher/releases/latest/download/WinLangSwitcher.exe'
)

$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 'WinLangSwitcher'
$exe        = Join-Path $installDir 'WinLangSwitcher.exe'
$taskName   = 'WinLangSwitcher'

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

# schtasks /End runs as SYSTEM via Scheduler, so it can stop our elevated daemon
# from a non-elevated shell — unlike Stop-Process, which would be denied.
if (Test-TaskRunning) {
    Write-Host "Stopping $taskName task..."
    schtasks.exe /End /TN $taskName | Out-Null
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir | Out-Null
}

# Windows PowerShell 5.1 defaults to TLS 1.0; GitHub requires 1.2+.
[Net.ServicePointManager]::SecurityProtocol =
    [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

Write-Host "Downloading $Url..."
Invoke-WebRequest -Uri $Url -OutFile $exe -UseBasicParsing

if (-not (Test-Path $exe)) {
    Write-Error "Download failed: $exe not found."
    exit 1
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "Downloaded: $exe ($sizeMb MB)"

# Only re-create the task when missing or pointing elsewhere — this is the one
# step that needs admin. Upgrades typically skip this entirely.
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
