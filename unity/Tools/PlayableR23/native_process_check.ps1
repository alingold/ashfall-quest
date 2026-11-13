param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
if(-not [IO.Path]::IsPathRooted($OutputPath) -or (Test-Path -LiteralPath $OutputPath)){throw 'Fresh absolute process receipt required'}
$vrProcesses=@(Get-CimInstance Win32_Process)
$vrOwners=@();$vrServices=@()
foreach($vrProcess in $vrProcesses){
    $vrEngine=$vrProcess.Name -match '^(?i:Unity|UnityEditor|Blender|Godot.*|Ashfall.*)\.exe$'
    $vrWorker=$vrProcess.Name -match '^(?i:python.*)\.exe$' -and $vrProcess.CommandLine -match '(?i:gpu_worker\.py|engine_lane\.py)'
    if(-not($vrEngine -or $vrWorker)){continue}
    $vrImage=if($vrProcess.ExecutablePath){$vrProcess.ExecutablePath.ToLowerInvariant()}else{''}
    $vrCommand=if($vrProcess.CommandLine){$vrProcess.CommandLine.Trim()}else{''}
    $vrParent=$vrProcesses|Where-Object ProcessId -eq $vrProcess.ParentProcessId|Select-Object -First 1
    $vrParentHub=$vrParent.ExecutablePath -like 'C:\Program Files\WindowsApps\UnityTechnologies.UnityHub_*\app\Unity Hub.exe'
    $vrHubServe=$vrParentHub -and $vrImage -like 'c:\program files\windowsapps\unitytechnologies.unityhub_*\app\resources\unity.exe' -and $vrCommand -eq ('"'+$vrProcess.ExecutablePath+'" serve')
    $vrAuth=$vrParentHub -and $vrImage -eq 'c:\users\aling\appdata\local\unity\bin\unity.exe' -and ($vrCommand -eq ('"'+$vrProcess.ExecutablePath+'" --internal-auth-broker-serve') -or $vrCommand -eq ($vrProcess.ExecutablePath+' --internal-auth-broker-serve'))
    $vrRow=[ordered]@{pid=$vrProcess.ProcessId;image=$vrImage;command=$vrCommand;parentPid=$vrProcess.ParentProcessId}
    if($vrHubServe -or $vrAuth){$vrRow.identity='Exact image, sole argument and Hub parent via CIM; no disk signature claim';$vrServices+=$vrRow}else{$vrOwners+=$vrRow}
}
$vrReceipt=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');scope='Win32 process inventory only; no device query';clear=($vrOwners.Count -eq 0);heavyOrPlayerOwners=$vrOwners;excludedHubServices=$vrServices;processesStopped=$false;locksAcquired=$false}
$vrReceipt|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output ('Native process clear='+$vrReceipt.clear+'; owners='+$vrOwners.Count+'; exact Hub services='+$vrServices.Count+'; receipt='+$OutputPath)
if($vrOwners.Count -ne 0){exit 3}
