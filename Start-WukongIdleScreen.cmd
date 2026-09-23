@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Set-Wallpaper.ps1" -Install
if errorlevel 1 (
    pause
    exit /b 1
)
start "" "%~dp0WukongCinema.exe" --background
