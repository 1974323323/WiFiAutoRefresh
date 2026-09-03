# WiFi Auto Refresh (Windows)

A lightweight Windows tray/console utility that scans nearby WiFi networks every 5 seconds
via the native `wlanapi` and displays them in a sortable, paginated WinForms window.
The currently-connected network is highlighted green; signal strength (real RSSI) drives
sorting and color-coding.

## Features

- **5-second background scan** using `WlanScan` / `WlanEnumInterfaces` (native wlanapi, no netsh dependency)
- **Sortable by real signal strength** (mixed-encoding safe parser; UTF-8 SSID + GBK localized labels)
- **Connected network pinned to top** with green highlight
- **Signal color coding**: >= 70% SeaGreen, >= 40% DarkOrange, else Firebrick
- **Smooth updates** — uses a 3-round grace window so weak APs don't flicker
- **Paginated list**, 10 networks per page
- **Three-mode auto-start** (user-selectable inside the app):
  1. **HKCU Run registry key** (no admin required)
  2. **SYSTEM scheduled task** (`ONSTART`, highest privilege)
  3. **Windows service** (`start= auto`)
- **Background modes**:
  - `-bg`: headless scan loop writing to `wifi_bg.log`
  - `-svc`: installable Windows service host
- **Custom application icon** embedded via `/win32icon`

## Repository layout

```
WiFiAutoRefresh.cs    # WinForms app + WlanApi P/Invoke (single file, ~480 lines)
WiFiAutoRefresh.ico   # Embedded icon
Build.ps1             # csc.exe compile script (kills stale process, then builds)
CleanupOld.ps1        # Removes old artifacts / stale schtasks
kill_old.ps1          # Force-kills a running instance
kill_old2.ps1         # Same, used by Build.ps1
WiFiAutoRefresh.iss   # Inno Setup installer script (see "Building an installer" below)
```

## Quick start (developer)

Requires: Windows 10/11, .NET Framework 4.x, `csc.exe` at
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

```powershell
cd C:\Scripts\WiFiRefresh
.\Build.ps1
```

Build.ps1 will:
1. Kill any running `WiFiAutoRefresh.exe` (via `kill_old2.ps1`)
2. Run `csc.exe /target:winexe /win32icon /out:WiFiAutoRefresh.exe WiFiAutoRefresh.cs`
3. Print size and PID of the freshly built EXE

Output: `WiFiAutoRefresh.exe` (~25 KB).

## Three-mode auto-start

Open the app, pick a mode from the **Auto-run** dropdown at the bottom:
- **Registry Run** — no admin, current-user only
- **Scheduled Task** — SYSTEM, fires at boot
- **Windows Service** — full service host, auto-restart on crash

Then click **Enable**. The state label turns green with the active mode.
Click **Disable** to remove the selected mode.

## Building a one-click installer (Inno Setup)

The repo ships `WiFiAutoRefresh.iss` so users can install via a single EXE.

Prerequisite: install [Inno Setup 6](https://jrsoftware.org/isdl.php)
(or via winget: `winget install --id JRSoftware.InnoSetup -e`).

```powershell
# Output goes to ./dist/WiFiAutoRefresh_Setup_<version>.exe
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\WiFiAutoRefresh.iss
```

The installer:
- Places EXE + ICO under `%ProgramFiles%\WiFiAutoRefresh\`
- Creates an uninstaller entry in "Add or Remove Programs"
- Optionally creates a desktop shortcut
- Offers to launch the app after install

The auto-start mode is **not** registered by the installer — the user picks
their preferred mode inside the app on first run.

## Background mode (`-bg`)

Run the app without a window; logs to `wifi_bg.log` next to the EXE every 5 seconds.

```powershell
.\WiFiAutoRefresh.exe -bg
```

## Service mode (`-svc`)

Host as a Windows service. After registering via the in-app "Windows Service"
auto-start mode, the service runs the same scan loop headlessly under
`LocalSystem`.

```powershell
# Manual install (normally the app does this):
sc.exe create WiFiAutoRefreshService binPath= "\"%~dp0WiFiAutoRefresh.exe\" -svc" start= auto
```

## Known caveats

- **netsh mixed encoding** — earlier revisions parsed `netsh wlan show networks` and
  hit mojibake because Windows localized labels are GBK while SSIDs come back as
  raw UTF-8 from the router. This release uses `wlanapi` directly, sidestepping
  the issue entirely.
- **WlanScan latency** — the scan is asynchronous; `netsh show` immediately
  afterwards may report cached/incomplete results. We `Thread.Sleep(2000)`
  before reading.
- **PowerShell 5.1 + Chinese strings** — calling the build script from a
  Windows PowerShell 5.1 host with GBK console encoding garbles inline Chinese.
  All Chinese in build scripts uses `.NET UTF-8 encoding` via Python or
  `[System.IO.File]::WriteAllBytes` to avoid this.

## License

MIT.
