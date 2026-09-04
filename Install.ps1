# Install.ps1 - installs Stand Up Reminder for the current user (no admin needed).
# Copies the app to %LocalAppData%\StandUpReminder and creates Start Menu +
# Desktop shortcuts. Optionally starts it at Windows login.

param(
    [switch]$RunAtStartup,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

# Resolve a source file, checking both the release-zip (flat) and repo (dist\/assets\) layouts.
function Find-Src($names) {
    foreach ($n in $names) {
        $p = Join-Path $PSScriptRoot $n
        if (Test-Path $p) { return $p }
    }
    return $null
}

$srcExe = Find-Src @('StandUpReminder.exe', 'dist\StandUpReminder.exe')
if (-not $srcExe) {
    # Source checkout without a build yet - compile it (needs src\Program.cs).
    if (Test-Path (Join-Path $PSScriptRoot 'build.ps1')) {
        Write-Host "Executable not found. Building it first..." -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot 'build.ps1')
        $srcExe = Find-Src @('dist\StandUpReminder.exe')
    }
}
if (-not $srcExe) { throw "StandUpReminder.exe not found and could not be built." }

$installDir = Join-Path $env:LOCALAPPDATA 'StandUpReminder'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Copy-Item $srcExe -Destination $installDir -Force
$exe = Join-Path $installDir 'StandUpReminder.exe'

$srcIco = Find-Src @('app.ico', 'dist\app.ico')
if ($srcIco) { Copy-Item $srcIco -Destination (Join-Path $installDir 'app.ico') -Force }

# Animated character (optional)
$srcGif = Find-Src @('standup.gif', 'dist\standup.gif', 'assets\standup.gif')
if ($srcGif) { Copy-Item $srcGif -Destination (Join-Path $installDir 'standup.gif') -Force }

$WShell = New-Object -ComObject WScript.Shell

function New-Shortcut($path) {
    $sc = $WShell.CreateShortcut($path)
    $sc.TargetPath = $exe
    $sc.WorkingDirectory = $installDir
    $sc.IconLocation = "$exe,0"
    $sc.Description = 'Reminds you to stand up on a schedule'
    $sc.Save()
}

# Start Menu shortcut
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
New-Shortcut (Join-Path $startMenu 'Stand Up Reminder.lnk')

# Desktop shortcut
$desktop = [Environment]::GetFolderPath('Desktop')
New-Shortcut (Join-Path $desktop 'Stand Up Reminder.lnk')

# Optional: run at login
if ($RunAtStartup) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    Set-ItemProperty -Path $runKey -Name 'StandUpReminder' -Value "`"$exe`""
    Write-Host 'Enabled: start automatically at login.' -ForegroundColor Green
}

Write-Host "`nInstalled to: $installDir" -ForegroundColor Green
Write-Host 'Shortcuts added to Start Menu and Desktop.'

if (-not $NoLaunch) {
    Start-Process $exe
    Write-Host 'Launched. Look for the icon in your system tray (bottom-right).' -ForegroundColor Cyan
}
