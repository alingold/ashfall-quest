# R18 local multi-peer launcher (PowerShell 7).
# Starts one real host Main plus N real client Mains as separate headless Godot
# processes on loopback ENet. Each peer has an isolated APPDATA/LOCALAPPDATA.
# Peer scripts receive user args: <role> <receipt.json> <port> <peer_count> then
# the ordinary desktop flags. Exit code 0 only when every peer passes.
#
#   pwsh tools/r18net/run-peers.ps1 -Script tests/r18net/match_flow_peers_r18.gd -Port 24871 -Name flow
#
param(
	[Parameter(Mandatory)][string]$Script,
	[int]$Port=24870,
	[string]$Name='peers',
	[string[]]$Roles=@('host','p1','p2'),
	[string]$Engine=$env:ASHFALL_GODOT,
	[string]$Project,
	[string]$Evidence,
	[string]$MapsPack=$env:ASHFALL_MAPS_PACK,
	[string]$Pack,
	[int]$TimeoutSeconds=300,
	[string[]]$ExtraUserArgs=@(),
	[switch]$Bare
)
$ErrorActionPreference='Stop'
# pwsh -File passes "a,b,c" as one string; accept both forms.
$Roles=@($Roles | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$ExtraUserArgs=@($ExtraUserArgs | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$sourceRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $Project){$Project=$sourceRoot}
if(-not $Evidence){$Evidence=Join-Path (Split-Path $sourceRoot -Parent) 'evidence'}
if(-not $Engine){$Engine='D:\Codex\ASHFALL-R18-Quest\engine\Godot_v4.7.2-stable_win64.exe'}
if(-not $MapsPack){$MapsPack='D:/Codex/ASHFALL-R15-Quest3/candidate/builds/ASHFALL-Local-Maps.pck'}
if(-not (Test-Path $Engine)){throw "Godot engine not found: $Engine (set -Engine or ASHFALL_GODOT)"}
New-Item -ItemType Directory -Force $Evidence | Out-Null
$scriptPath=if([IO.Path]::IsPathRooted($Script)){$Script}else{Join-Path $Project $Script}
$profiles=Join-Path $Evidence ('profiles\'+$Name)
$procs=@()
$index=0
foreach($role in $Roles){
	$receipt=Join-Path $Evidence "$Name-$role.json"
	if(Test-Path $receipt){Remove-Item $receipt -Force}
	$info=[Diagnostics.ProcessStartInfo]::new($Engine)
	$info.UseShellExecute=$false;$info.CreateNoWindow=$true
	$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
	$info.Environment['APPDATA']=Join-Path $profiles "$role\r"
	$info.Environment['LOCALAPPDATA']=Join-Path $profiles "$role\l"
	New-Item -ItemType Directory -Force $info.Environment['APPDATA'],$info.Environment['LOCALAPPDATA'] | Out-Null
	$a=@('--headless','--xr-mode','off','--audio-driver','Dummy','--log-file',(Join-Path $Evidence "$Name-$role.log"))
	if($Pack){$a+=@('--main-pack',$Pack)}else{$a+=@('--path',$Project)}
	$a+=@('--script',$scriptPath,'--',$role,$receipt,"$Port","$($Roles.Count)")
	if(-not $Bare){$a+=@('--desktop','--extended','--playtest-catalog',"--local-maps-pack=$MapsPack")}
	$a+=$ExtraUserArgs
	foreach($x in $a){$info.ArgumentList.Add($x)}
	$p=[Diagnostics.Process]::Start($info)
	try{$p.PriorityClass='BelowNormal'}catch{}
	$procs+=[pscustomobject]@{Role=$role;Proc=$p;Out=$p.StandardOutput.ReadToEndAsync();Err=$p.StandardError.ReadToEndAsync();Receipt=$receipt}
	# Host binds first; clients stagger so join order (and therefore team balance) is deterministic.
	Start-Sleep -Milliseconds ($(if($index -eq 0){1500}else{700}))
	$index++
}
$watch=[Diagnostics.Stopwatch]::StartNew()
foreach($entry in $procs){
	while(-not $entry.Proc.WaitForExit(500)){
		if($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds){
			# Only processes started by this launcher are stopped.
			foreach($e in $procs){if(-not $e.Proc.HasExited){$e.Proc.Kill()}}
			Write-Host "TIMEOUT after $TimeoutSeconds s"
			break
		}
	}
}
$allPassed=$true
foreach($entry in $procs){
	$entry.Proc.WaitForExit()
	$body=$entry.Out.Result+[Environment]::NewLine+$entry.Err.Result
	$body | Set-Content -LiteralPath (Join-Path $Evidence "$Name-$($entry.Role).txt") -Encoding utf8
	$summary='(no receipt)'
	$passed=$false
	if(Test-Path $entry.Receipt){
		$r=Get-Content $entry.Receipt -Raw | ConvertFrom-Json
		$passed=[bool]$r.passed
		$failures=@()
		if($r.failures -is [ValueType]){if([double]$r.failures -gt 0){$failures=@("count=$($r.failures)")}}
		elseif($null -ne $r.failures){$failures=@($r.failures)}
		$summary="checks=$($r.checks) failures=$($failures.Count)"
		if($failures.Count){$summary+=' :: '+($failures -join ' | ');$passed=$false}
	}
	$scriptErrors=([regex]::Matches($body,'SCRIPT ERROR:')).Count
	if($scriptErrors){$summary+=" SCRIPT_ERRORS=$scriptErrors";$passed=$false}
	$engineErrors=([regex]::Matches($body,'(?m)^ERROR:')).Count
	if($engineErrors){$summary+=" ENGINE_ERRORS=$engineErrors";$passed=$false}
	if($entry.Proc.ExitCode -ne 0){$passed=$false}
	if(-not $passed){$allPassed=$false}
	Write-Host ("{0,-5} exit={1} passed={2} {3}" -f $entry.Role,$entry.Proc.ExitCode,$passed,$summary)
}
if($allPassed){Write-Host "ALL PEERS PASSED ($Name)";exit 0}else{Write-Host "PEER FAILURE ($Name)";exit 1}
