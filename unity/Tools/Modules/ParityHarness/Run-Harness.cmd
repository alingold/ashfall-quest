@echo off
setlocal
for %%R in ("%~dp0..\..\..\..") do set "ROOT=%%~fR"
set "EVIDENCE=%ROOT%\docs\agent-runs\20261104-three-chats\unity\ParityHarness"
set "OUTPUT=%ROOT%\campaign-staging\20261104-three-chats\unity\ParityHarness"
set "ASSETS=%ROOT%\unity\Assets\Ashfall\Modules\ParityHarness"
if not exist "%EVIDENCE%" mkdir "%EVIDENCE%"
if not exist "%OUTPUT%" mkdir "%OUTPUT%"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /langversion:5 /warn:4 /warnaserror+ /main:Ashfall.Modules.ParityHarness.HarnessMain /reference:System.Web.Extensions.dll /out:"%OUTPUT%\ParityHarness.exe" "%ROOT%\unity\Assets\Ashfall\Core\Contracts.cs" "%ROOT%\unity\Tools\ToolchainProbe\ConversionProbe.cs" "%ASSETS%\Ports.cs" "%ASSETS%\ScenarioRunner.cs" "%ASSETS%\RepresentativeScenarios.cs" "%~dp0HarnessMain.cs" "%~dp0HarnessSelfTests.cs" > "%EVIDENCE%\compile.log" 2>&1
if errorlevel 1 goto failed
"%OUTPUT%\ParityHarness.exe" --self-test > "%EVIDENCE%\self-test.log" 2>&1
if errorlevel 1 goto failed
"%OUTPUT%\ParityHarness.exe" "%EVIDENCE%\results.json" 5 %* > "%EVIDENCE%\run.log" 2>&1
set "RESULT=%ERRORLEVEL%"
type "%EVIDENCE%\compile.log"
type "%EVIDENCE%\self-test.log"
type "%EVIDENCE%\run.log"
exit /b %RESULT%
:failed
type "%EVIDENCE%\compile.log"
if exist "%EVIDENCE%\self-test.log" type "%EVIDENCE%\self-test.log"
exit /b 1
