@echo off
setlocal
for %%I in ("%~dp0..\..\..\..") do set "HS_ROOT=%%~fI"
set "HS_OUT=%HS_ROOT%\campaign-staging\20261104-three-chats\unity\HealthStates"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /langversion:5 /warnaserror+ /target:library /out:"%HS_OUT%\HealthStates.dll" "%HS_ROOT%\unity\Assets\Ashfall\Core\Contracts.cs" "%HS_ROOT%\unity\Assets\Ashfall\Modules\HealthStates\Ports.cs" "%HS_ROOT%\unity\Assets\Ashfall\Modules\HealthStates\HealthService.cs"
if errorlevel 1 exit /b 1
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%HS_OUT%\HealthStates.Tests.exe" /reference:"%HS_OUT%\HealthStates.dll" "%~dp0HealthTests.cs"
if errorlevel 1 exit /b 1
"%HS_OUT%\HealthStates.Tests.exe"
exit /b %errorlevel%
