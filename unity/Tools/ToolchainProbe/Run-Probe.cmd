@echo off
setlocal
set "PROBE_COMPILER=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%PROBE_COMPILER%" /nologo /target:exe /langversion:5 /warn:4 /warnaserror+ /out:"%~dp0ConversionProbe.exe" "%~dp0ConversionProbe.cs"
if errorlevel 1 exit /b 1
"%~dp0ConversionProbe.exe"
exit /b %errorlevel%
