# Uninstall.ps1 - removes Desk Break Reminder for the current user.
$ErrorActionPreference = 'SilentlyContinue'

# Stop it if running
Get-Process DeskBreakReminder -ErrorAction SilentlyContinue | Stop-Process -Force

$installDir = Join-Path $env:LOCALAPPDATA 'DeskBreakReminder'
$startMenu  = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Desk Break Reminder.lnk'
$desktop    = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Desk Break Reminder.lnk'

Remove-Item $startMenu -Force -ErrorAction SilentlyContinue
Remove-Item $desktop   -Force -ErrorAction SilentlyContinue
Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue

# Remove startup entry
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'DeskBreakReminder' -ErrorAction SilentlyContinue

# Remove saved settings
Remove-Item (Join-Path $env:APPDATA 'DeskBreakReminder') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'Desk Break Reminder has been uninstalled.' -ForegroundColor Green
