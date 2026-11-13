param([string]$Root='C:\Users\aling\Documents\ashfall')
$ErrorActionPreference='Stop'
$Root=[IO.Path]::GetFullPath($Root)
$out=Join-Path $Root 'campaign-staging\20261104-three-chats\unity\InventoryItems\r2'
New-Item -ItemType Directory -Force $out|Out-Null
$files=@((Join-Path $Root 'unity\Assets\Ashfall\Core\Contracts.cs'))
$files+=@(Get-ChildItem (Join-Path $Root 'unity\Assets\Ashfall\Modules\InventoryItems') -Filter *.cs|Sort-Object Name|ForEach-Object {$_.FullName})
$files+=Join-Path $Root 'unity\Tools\Modules\InventoryItems\InventoryTests.cs'
$options=@('/nologo','/langversion:5','/warnaserror+','/target:exe',('/out:"'+$out+'\InventoryTests.exe"'))
$options+=@($files|ForEach-Object {'"'+[IO.Path]::GetFullPath($_)+'"'})
[IO.File]::WriteAllLines("$out\tests.rsp",$options)
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$compileOutput=& $compiler "@$out\tests.rsp" 2>&1
$compileExit=$LASTEXITCODE
$compileOutput|Set-Content "$out\compiler.log"
if($compileExit -ne 0){throw "C#5 compile failed ($compileExit): $compileOutput"}
$testOutput=& "$out\InventoryTests.exe" 2>&1
$testExit=$LASTEXITCODE
$testOutput|Set-Content "$out\tests.log"
if($testExit -ne 0){throw "Tests failed ($testExit): $testOutput"}
Write-Output $testOutput
Write-Output 'C#5 /warnaserror+ compile exit 0; tests exit 0.'
