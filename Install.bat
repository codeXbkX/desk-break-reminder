@echo off
REM Double-click to install Stand Up Reminder (also enables start-at-login).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -RunAtStartup
echo.
pause
