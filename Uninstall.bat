@echo off
REM Double-click to uninstall Desk Break Reminder.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
echo.
pause
