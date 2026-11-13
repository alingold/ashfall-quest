@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Quest.ps1" %*
set "installerExit=%ERRORLEVEL%"
if not "%installerExit%"=="0" echo Installation failed. Read the message above before trying again.
pause
exit /b %installerExit%
