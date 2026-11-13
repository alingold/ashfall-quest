@echo off
setlocal
set "TOOLCHAIN_CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
for %%I in ("%~dp0..\..\..\..") do set "TOOLCHAIN_ROOT=%%~fI"
set "TOOLCHAIN_OUTPUT=%TOOLCHAIN_ROOT%\campaign-staging\20261104-three-chats\unity\Toolchain\r2\bootstrap-%RANDOM%-%RANDOM%"
if exist "%TOOLCHAIN_OUTPUT%" exit /b 2
mkdir "%TOOLCHAIN_OUTPUT%"
"%TOOLCHAIN_CSC%" /noconfig /nologo /langversion:5 /warn:4 /warnaserror+ /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll" /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll" /target:exe /out:"%TOOLCHAIN_OUTPUT%\Toolchain.exe" "%TOOLCHAIN_ROOT%\unity\Assets\Ashfall\Modules\Toolchain\Ports.cs" "%TOOLCHAIN_ROOT%\unity\Assets\Ashfall\Modules\Toolchain\CompilerRunner.cs" "%~dp0Program.cs"
if errorlevel 1 exit /b 1
"%TOOLCHAIN_OUTPUT%\Toolchain.exe" "%TOOLCHAIN_ROOT%" %*
exit /b %errorlevel%
