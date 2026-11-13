$ErrorActionPreference='Stop'
$root='C:\Users\aling\Documents\ashfall'
$out=Join-Path $root 'campaign-staging\20261104-three-chats\unity\NetworkRuntimeR1\cpu-01'
$evidence=Join-Path $root 'docs\agent-runs\20261104-three-chats\unity\NetworkRuntimeR1'
New-Item -ItemType Directory -Force $out | Out-Null
$inputRoot=Join-Path $root 'campaign-staging\20261104-three-chats\unity\NetworkRuntimeR1\initial'
$sources=@((Join-Path $inputRoot 'unity\Assets\Ashfall\Core\Contracts.cs'))
foreach($dir in @('Modules\Networking','Integration\Sessions','Integration\SessionReplication')) {
 $sources+=@(Get-ChildItem (Join-Path $inputRoot ('unity\Assets\Ashfall\'+$dir)) -Filter *.cs | ForEach-Object FullName)
}
# Deliberately narrow admission closure: lifecycle/Health partial APIs are not exercised.
foreach($file in @('Session.cs','Ports.cs')) { $sources+=Join-Path $inputRoot ('unity\Assets\Ashfall\Modules\Sessions\'+$file) }
$sources+=@(Get-ChildItem (Join-Path $root 'unity\Assets\Ashfall\Integration\NetworkRuntime') -Recurse -Filter *.cs | ForEach-Object FullName)
$sources+=Join-Path $root 'unity\Tools\Integration\NetworkRuntime\SyntheticSessionFixture.cs'
$sources+=Join-Path $root 'unity\Tools\Integration\NetworkRuntime\ChannelTests.cs'
$sources+=Join-Path $root 'unity\Tools\Integration\NetworkRuntime\NativeFixture.cs'
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$argsList=@('/nologo','/langversion:5','/warnaserror+','/target:exe',('/out:"'+$out+'\ChannelTests.exe"'))
$argsList+=@($sources | ForEach-Object { '"'+$_+'"' })
($csc+' '+($argsList -join ' ')) | Set-Content (Join-Path $evidence 'cpu-01-command.txt')
& $csc @argsList 2>&1 | Tee-Object (Join-Path $evidence 'cpu-01-compile.txt')
if($LASTEXITCODE -ne 0) { throw 'compile failed' }
& (Join-Path $out 'ChannelTests.exe') 2>&1 | Tee-Object (Join-Path $evidence 'cpu-01-run.txt')
if($LASTEXITCODE -ne 0) { throw 'tests failed' }
