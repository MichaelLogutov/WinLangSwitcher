# WinLangSwitcher

[![CI](https://github.com/MichaelLogutov/WinLangSwitcher/actions/workflows/ci.yml/badge.svg)](https://github.com/MichaelLogutov/WinLangSwitcher/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/MichaelLogutov/WinLangSwitcher?include_prereleases&sort=semver)](https://github.com/MichaelLogutov/WinLangSwitcher/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A tiny Windows utility that turns the **CapsLock** key into a one-tap toggle between your two installed keyboard layouts (e.g. RU↔EN).

- **Reliable.** Replaces the flaky built-in `Ctrl+Shift` / `Win+Space` shortcut.
- **Headless.** No tray icon, no settings window, no logs on disk.
- **Tiny.** ~2 MB native AOT executable, single file, no .NET runtime needed.
- **Works in admin-elevated windows too** (UAC-elevated cmd, Task Manager run as admin, etc.).

## Trade-offs

CapsLock no longer toggles letter case at all. Use **Shift** for uppercase. The CapsLock LED stays off.

## Requirements

- Windows 10 or Windows 11 (x64)
- Exactly two keyboard layouts installed in Windows settings (any pair — the switcher just toggles between whichever two are present)

### To build from source

- .NET 10 SDK
- For the small Native AOT binary: Visual Studio Build Tools with the "Desktop development with C++" workload (provides `link.exe`). Install via winget (admin required):
  ```
  winget install Microsoft.VisualStudio.BuildTools --override "--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended" --accept-package-agreements --accept-source-agreements
  ```
  Without it, fall back to the self-contained non-AOT publish (see below).

## Build & install (from source)

```
./deploy.ps1
```

Stops the running instance (if any), rebuilds with Native AOT, deploys to `%LOCALAPPDATA%\WinLangSwitcher\WinLangSwitcher.exe`, then (re)installs the scheduled task and starts it via `schtasks /Run`. The install dir is stable, user-scoped, survives `dotnet clean`, and is what `--install` expects.

If the AOT publish fails (e.g. "Platform linker not found" because the C++ workload isn't installed), the script automatically falls back to a self-contained, non-AOT single-file build (~70 MB). Functionally identical at runtime, just much bigger.

## Install

Run once, from a shell launched as Administrator:
```
WinLangSwitcher.exe --install
```

This registers a scheduled task that auto-starts the utility on every logon — silently, with no UAC prompt.

### Why does install need admin rights?

Windows doesn't allow a regular-user process to send keystrokes into windows you launched with "Run as Administrator". Without elevation, CapsLock would silently do nothing whenever an admin-elevated window has focus (UAC-elevated cmd, Task Manager, regedit, etc.).

The installer works around this by registering WinLangSwitcher as a scheduled task with elevated privileges. The admin rights are only needed at install time to register the task — from then on it just launches itself on logon, no further prompts.

### Start it now without re-logon

From the same admin shell:
```
schtasks /Run /TN WinLangSwitcher
```

Verify with `Get-Process WinLangSwitcher` — one process should be listed. That's it; the utility runs headless.

> Launching `WinLangSwitcher.exe` directly from a non-elevated shell will *seem* to work but won't be able to switch layouts in admin-elevated windows. Always start through the scheduled task (or re-logon).

## Uninstall

From an elevated shell:
```
WinLangSwitcher.exe --uninstall
taskkill /IM WinLangSwitcher.exe /F
```

## Manual test checklist

Before relying on it, walk through these:

1. Launch → CapsLock toggles between your two layouts in Notepad.
2. Same in Chrome / Edge address bar.
3. Same in `cmd.exe`, PowerShell, Windows Terminal.
4. CapsLock LED on the keyboard stays off.
5. `Shift + letter` still produces uppercase. Pressing CapsLock and then a letter produces lowercase (CapsLock is fully disabled).
6. Launch a second copy → it exits silently; the first keeps running (Task Manager: one `WinLangSwitcher.exe`).
7. `--install`, reboot → the utility is running.
8. `--uninstall`, reboot → the utility is **not** running.
9. After install + reboot, CapsLock toggles layouts inside an admin-elevated cmd / Task Manager too.

## Diagnostics

The utility writes runtime errors via `OutputDebugString`. View them with [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview) (Sysinternals).

Startup errors (e.g. failed to install the keyboard hook) also go to stderr if launched from a console.

## License

[MIT](LICENSE) © Michael Logutov
