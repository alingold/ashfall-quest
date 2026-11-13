@echo off
setlocal
for %%I in ("%~dp0..\..\..\..") do set "ROOT=%%~fI"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%~dp0FlightTests.exe" "%ROOT%\unity\Assets\Ashfall\Core\Contracts.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Flight\FlightPorts.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Flight\FlightMachine.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Flight\Capsules.cs" "%~dp0FlightTests.cs"
if errorlevel 1 exit /b 1
"%~dp0FlightTests.exe"
exit /b %errorlevel%
