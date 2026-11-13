# Solo-developer multiplayer regression: every suite is real Main processes on loopback ENet.
# Runs sequentially (one suite at a time) so frame timing is not distorted by parallel suites.
#   pwsh tools/r18net/run-all.ps1 [-BasePort 24900] [-Tag r18] [-Only flow,combat]
param([int]$BasePort=24900,[string]$Tag='r18',[string[]]$Only=@(),[string]$Engine=$env:ASHFALL_GODOT,[string]$Pack)
$ErrorActionPreference='Stop'
$Only=@($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$suites=@(
	@{Key='solo';   Script='tests/r18net/solo_start_r18.gd';       Roles='host';          Timeout=200},
	@{Key='flow';   Script='tests/r18net/match_flow_peers_r18.gd'; Roles='host,p1,p2';    Timeout=420},
	@{Key='combat'; Script='tests/r18net/combat_peers_r18.gd';     Roles='host,p1,p2';    Timeout=400},
	@{Key='uzi10';  Script='tests/peer_runtime_r10.gd';            Roles='host,friend';   Timeout=300},
	@{Key='ghost13';Script='tests/ghost_peers_r13.gd';             Roles='host,Ally,Enemy';Timeout=300},
	@{Key='fx14';   Script='tests/fx_peers_r14.gd';                Roles='host,Ally,Enemy';Timeout=300}
)
$runner=Join-Path $PSScriptRoot 'run-peers.ps1'
$results=@();$port=$BasePort
foreach($s in $suites){
	if($Only.Count -and $s.Key -notin $Only){continue}
	$name="$Tag-$($s.Key)"
	Write-Host "=== $name ($($s.Script)) port $port"
	$a=@('-NoProfile','-File',$runner,'-Script',$s.Script,'-Port',"$port",'-Name',$name,'-Roles',$s.Roles,'-TimeoutSeconds',"$($s.Timeout)")
	if($Engine){$a+=@('-Engine',$Engine)}
	if($Pack){$a+=@('-Pack',$Pack)}
	& (Get-Process -Id $PID).Path @a
	$results+=[pscustomobject]@{Suite=$name;Passed=($LASTEXITCODE -eq 0)}
	$port+=3
}
$results | Format-Table -AutoSize
if(@($results | Where-Object { -not $_.Passed }).Count){exit 1}
exit 0
