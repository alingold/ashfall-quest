@echo off
setlocal
set "ROOT=%~dp0..\..\..\.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
set "OUT=%ROOT%\campaign-staging\20261104-three-chats\unity\Combat\CombatTests.exe"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%OUT%" "%ROOT%\unity\Assets\Ashfall\Core\Contracts.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Combat\Ports.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Combat\CombatRules.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Combat\SwordContacts.cs" "%ROOT%\unity\Assets\Ashfall\Modules\Combat\BulletGuard.cs" "%~dp0CombatTests.cs"
if errorlevel 1 exit /b 1
"%OUT%"
exit /b %errorlevel%

