$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$stage=Join-Path $root 'campaign-staging/20261104-three-chats/unity/MatchDirector/r3/sessions3'
$inputs=Get-Content (Join-Path $stage 'inputs.json') -Raw | ConvertFrom-Json
foreach($i in $inputs){if((Get-FileHash (Join-Path $stage ('inputs/'+$i.path)) -Algorithm SHA256).Hash -ne $i.sha256){throw 'Input hash mismatch'}}
$accepted=Get-Content (Join-Path $stage '../accepted-neutral-v1.json') -Raw | ConvertFrom-Json
foreach($i in $accepted.sourceChecks | Where-Object {$_.name -eq 'Modes'}){if((Get-FileHash (Join-Path $stage ('inputs/unity/Assets/Ashfall/Modules/Modes/'+(Split-Path $i.path -Leaf))) -Algorithm SHA256).Hash -ne $i.expected){throw 'Accepted Modes hash mismatch'}}
$bin=Join-Path $stage ('reruns/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $bin | Out-Null
$rsp=Join-Path $bin 'tests.rsp'
Get-Content (Join-Path $stage 'frozen/tests.rsp') | ForEach-Object {if($_ -like '/out:*'){'/out:"'+(Join-Path $bin 'MatchDirector.Tests.exe')+'"'}else{$_}} | Set-Content $rsp
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& (Join-Path $bin 'MatchDirector.Tests.exe')
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
