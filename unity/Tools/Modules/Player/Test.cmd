@echo off
setlocal
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%~dp0PlayerTests.exe" "%~dp0..\..\..\Assets\Ashfall\Core\Contracts.cs" "%~dp0..\..\..\Assets\Ashfall\Modules\Player\PlayerPorts.cs" "%~dp0..\..\..\Assets\Ashfall\Modules\Player\PlayerInput.cs" "%~dp0..\..\..\Assets\Ashfall\Modules\Player\Locomotion.cs" "%~dp0PlayerTests.cs"
if errorlevel 1 exit /b 1
"%~dp0PlayerTests.exe"
exit /b %errorlevel%
