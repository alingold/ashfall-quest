@echo off
setlocal
for %%I in ("%~dp0..\..\..\..") do set "SESSIONS_ROOT=%%~fI"
set "SESSIONS_CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "SESSIONS_OUT=%SESSIONS_ROOT%\campaign-staging\20261104-three-chats\unity\Sessions"
set "SESSIONS_EVIDENCE=%SESSIONS_ROOT%\docs\agent-runs\20261104-three-chats\unity\Sessions"
set "SESSIONS_CODE=%SESSIONS_ROOT%\unity\Assets\Ashfall\Modules\Sessions"
"%SESSIONS_CSC%" /nologo /langversion:5 /warnaserror+ /target:library "/out:%SESSIONS_OUT%\Sessions.dll" "%SESSIONS_ROOT%\unity\Assets\Ashfall\Core\Contracts.cs" "%SESSIONS_CODE%\Ports.cs" "%SESSIONS_CODE%\Session.cs" > "%SESSIONS_EVIDENCE%\compile-library.txt" 2>&1
if errorlevel 1 exit /b 1
"%SESSIONS_CSC%" /nologo /langversion:5 /warnaserror+ /target:exe "/out:%SESSIONS_OUT%\SessionTests.exe" "%SESSIONS_ROOT%\unity\Assets\Ashfall\Core\Contracts.cs" "%SESSIONS_CODE%\Ports.cs" "%SESSIONS_CODE%\Session.cs" "%~dp0SessionTests.cs" > "%SESSIONS_EVIDENCE%\compile-tests.txt" 2>&1
if errorlevel 1 exit /b 1
"%SESSIONS_OUT%\SessionTests.exe" > "%SESSIONS_EVIDENCE%\test-results.txt" 2>&1
if errorlevel 1 exit /b 1
type "%SESSIONS_EVIDENCE%\test-results.txt"
exit /b 0
