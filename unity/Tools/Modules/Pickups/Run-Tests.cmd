@echo off
setlocal
for %%I in ("%~dp0..\..\..\..") do set "PICKUPS_ROOT=%%~fI"
set "PICKUPS_OUT=%PICKUPS_ROOT%\campaign-staging\20261104-three-chats\unity\Pickups\r2"
set "PICKUPS_EVIDENCE=%PICKUPS_ROOT%\docs\agent-runs\20261104-three-chats\unity\Pickups\r2"
set "PICKUPS_CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "PICKUPS_CORE=%PICKUPS_ROOT%\unity\Assets\Ashfall\Core\Contracts.cs"
set "PICKUPS_RULES=%PICKUPS_ROOT%\unity\Assets\Ashfall\Modules\Pickups\PickupRules.cs"
set "PICKUPS_PORTS=%PICKUPS_ROOT%\unity\Assets\Ashfall\Modules\Pickups\Ports.cs"
"%PICKUPS_CSC%" /nologo /langversion:5 /warnaserror+ /target:library /out:"%PICKUPS_OUT%\Ashfall.Modules.Pickups.dll" "%PICKUPS_CORE%" "%PICKUPS_PORTS%" "%PICKUPS_RULES%" > "%PICKUPS_EVIDENCE%\compile-library.log" 2>&1
if errorlevel 1 goto failed
"%PICKUPS_CSC%" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%PICKUPS_OUT%\Pickups.Tests.exe" "%PICKUPS_CORE%" "%PICKUPS_PORTS%" "%PICKUPS_RULES%" "%~dp0Tests.cs" > "%PICKUPS_EVIDENCE%\compile-tests.log" 2>&1
if errorlevel 1 goto failed
"%PICKUPS_OUT%\Pickups.Tests.exe" > "%PICKUPS_EVIDENCE%\test-results.log" 2>&1
if errorlevel 1 goto failed
type "%PICKUPS_EVIDENCE%\test-results.log"
"%PICKUPS_CSC%" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%PICKUPS_OUT%\AdversarialR1Adapted.exe" "%PICKUPS_CORE%" "%PICKUPS_PORTS%" "%PICKUPS_RULES%" "%~dp0AdversarialR1Adapted.cs" > "%PICKUPS_EVIDENCE%\compile-adversarial.log" 2>&1
if errorlevel 1 goto failed
"%PICKUPS_OUT%\AdversarialR1Adapted.exe" > "%PICKUPS_EVIDENCE%\adversarial-results.log" 2>&1
if errorlevel 1 goto failed
type "%PICKUPS_EVIDENCE%\adversarial-results.log"
"%PICKUPS_CSC%" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%PICKUPS_OUT%\BatchTests.exe" "%PICKUPS_CORE%" "%PICKUPS_PORTS%" "%PICKUPS_RULES%" "%~dp0BatchTests.cs" > "%PICKUPS_EVIDENCE%\compile-batch.log" 2>&1
if errorlevel 1 goto failed
"%PICKUPS_OUT%\BatchTests.exe" > "%PICKUPS_EVIDENCE%\batch-results.log" 2>&1
if errorlevel 1 goto failed
type "%PICKUPS_EVIDENCE%\batch-results.log"
exit /b 0
:failed
type "%PICKUPS_EVIDENCE%\compile-library.log"
type "%PICKUPS_EVIDENCE%\compile-tests.log"
type "%PICKUPS_EVIDENCE%\test-results.log"
type "%PICKUPS_EVIDENCE%\compile-adversarial.log"
type "%PICKUPS_EVIDENCE%\adversarial-results.log"
type "%PICKUPS_EVIDENCE%\compile-batch.log"
type "%PICKUPS_EVIDENCE%\batch-results.log"
exit /b 1
