# Cozy - Windows setup.
#
# There is NO custom Cozy app for Windows: the PC runs stock *deskflow* as the
# SERVER (the machine whose keyboard/mouse you want to share). This script
# installs deskflow and prints the next steps.
#
# Run in an ELEVATED PowerShell (Right-click PowerShell -> Run as administrator):
#   powershell -ExecutionPolicy Bypass -File install-cozy-windows.ps1

Write-Host "== Cozy Windows setup ==" -ForegroundColor Cyan

# 1. deskflow via winget (needs admin elevation)
Write-Host "Installing deskflow (accept the admin prompt if it appears)..."
winget install --id Deskflow.Deskflow --accept-package-agreements --accept-source-agreements
if ($LASTEXITCODE -ne 0) {
    Write-Host "winget install did not complete (exit $LASTEXITCODE)." -ForegroundColor Yellow
    Write-Host "If it was an admin error, re-run this script as administrator." -ForegroundColor Yellow
    Write-Host "Or download the installer from https://deskflow.org"
}

# 2. Show this PC's LAN IP (you'll type it into the tablet / Mac)
Write-Host "`nThis PC's LAN IP address(es):" -ForegroundColor Cyan
Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.*' } |
    Select-Object IPAddress, InterfaceAlias | Format-Table -AutoSize

# 3. Open the firewall for the deskflow port (24800/TCP) on private networks
Write-Host "Allowing deskflow port 24800/TCP through Windows Firewall (private profile)..."
try {
    if (-not (Get-NetFirewallRule -DisplayName "Cozy deskflow 24800" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName "Cozy deskflow 24800" -Direction Inbound -Action Allow `
            -Protocol TCP -LocalPort 24800 -Profile Private | Out-Null
        Write-Host "Firewall rule added." -ForegroundColor Green
    } else { Write-Host "Firewall rule already present." }
} catch {
    Write-Host "Could not add firewall rule (need admin). deskflow will usually prompt you instead." -ForegroundColor Yellow
}

Write-Host @"

== Next steps ==
1. Launch Deskflow. Choose SERVER (this PC has the keyboard/mouse).
2. Open the server configuration / screen layout. Add a screen for each device:
     - the Android tablet (name must match what you type in the Cozy app)
     - the Mac (if sharing with it too)
   Drag them to the sides where you want the cursor to cross over.
   (Or generate the layout in cozy-layout-editor\index.html and paste the .conf.)
3. For the FIRST connection, disable encryption/TLS:
     Settings -> Security -> turn OFF 'Enable encryption' / TLS.
   Then untick 'Use TLS' in the Cozy app too (they must match).
4. Start the server. It will wait for clients.

Then install the Cozy app on the tablet (dist\install-android.ps1) and connect.
See ..\SETUP.md for the full walkthrough and troubleshooting.
"@ -ForegroundColor White
