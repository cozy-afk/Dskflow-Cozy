# Cozy — install the APK onto a USB-connected Android tablet.
#
# Prereqs on the tablet (one time):
#   Settings -> About -> tap "Build number" 7x to unlock Developer options
#   Settings -> Developer options -> enable "USB debugging"
#   Plug in USB, and on the tablet tap "Allow" when asked to trust this computer.
#
# Then run:  powershell -ExecutionPolicy Bypass -File install-android.ps1

$adb = "C:\Users\theco\Dskflow-Cozy\.tools\android-sdk\platform-tools\adb.exe"
$apk = Join-Path $PSScriptRoot "Cozy-0.1.0-debug.apk"

if (-not (Test-Path $adb)) { Write-Host "adb not found at $adb" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $apk)) { Write-Host "APK not found at $apk" -ForegroundColor Red; exit 1 }

Write-Host "Devices:"
& $adb devices
$devices = (& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match "device$" }
if (-not $devices) {
    Write-Host "`nNo authorized device detected." -ForegroundColor Yellow
    Write-Host "Enable USB debugging on the tablet, plug it in, accept the trust prompt, and re-run."
    Write-Host "(Alternatively, copy Cozy-0.1.0-debug.apk to the tablet and tap it to sideload.)"
    exit 1
}

Write-Host "`nInstalling Cozy..." -ForegroundColor Cyan
& $adb install -r $apk
Write-Host "`nDone. On the tablet, open Cozy and follow the on-screen permission steps." -ForegroundColor Green
