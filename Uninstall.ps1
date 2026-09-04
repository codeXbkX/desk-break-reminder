# Uninstall.ps1 - removes Stand Up Reminder for the current user.
$ErrorActionPreference = 'SilentlyContinue'

# Stop it if running
Get-Process StandUpReminder -ErrorAction SilentlyContinue | Stop-Process -Force

$installDir = Join-Path $env:LOCALAPPDATA 'StandUpReminder'
$startMenu  = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Stand Up Reminder.lnk'
$desktop    = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Stand Up Reminder.lnk'

Remove-Item $startMenu -Force -ErrorAction SilentlyContinue
Remove-Item $desktop   -Force -ErrorAction SilentlyContinue
Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue

# Remove startup entry
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'StandUpReminder' -ErrorAction SilentlyContinue

# Remove saved settings
Remove-Item (Join-Path $env:APPDATA 'StandUpReminder') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'Stand Up Reminder has been uninstalled.' -ForegroundColor Green
