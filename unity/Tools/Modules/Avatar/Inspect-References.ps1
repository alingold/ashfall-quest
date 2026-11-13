$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$reportPath = Join-Path $root 'docs/agent-runs/20261104-three-chats/unity/Avatar/source-references.json'
$required = @('Jacket','ChestPlate','Belt','LUpperArm','LForearm','LThigh','LShin','LBoot','RUpperArm','RForearm','RThigh','RShin','RBoot','AvatarHead')
$receipts = @()
foreach ($tier in @('pcvr','light')) {
    $path = Join-Path $root ('game/assets/models/custom_skins/pj/player_kit_pj_' + $tier + '.glb')
    $stream = [IO.File]::OpenRead($path)
    try {
        $reader = New-Object IO.BinaryReader($stream)
        if ($reader.ReadUInt32() -ne 0x46546C67 -or $reader.ReadUInt32() -ne 2) { throw 'Invalid GLB header' }
        if ($reader.ReadUInt32() -ne $stream.Length) { throw 'GLB length mismatch' }
        $length = $reader.ReadUInt32()
        if ($length -gt 4MB -or $reader.ReadUInt32() -ne 0x4E4F534A) { throw 'Invalid GLB JSON chunk' }
        $gltf = [Text.Encoding]::UTF8.GetString($reader.ReadBytes($length)) | ConvertFrom-Json
    } finally { $stream.Dispose() }
    $meshes = @($gltf.nodes | Where-Object { $null -ne $_.mesh } | ForEach-Object { $_.name })
    $missing = @($required | Where-Object { $_ -cnotin $meshes })
    $receipts += [ordered]@{ path=$path; sha256=(Get-FileHash $path).Hash; tier=$tier; meshNodes=$meshes; missing=$missing; materialNames=@($gltf.materials | ForEach-Object { $_.name }); approval='UNREVIEWED_SOURCE_REFERENCE'; coordinates='Godot/glTF source; conversion NOT established' }
}
foreach ($side in @('L','R')) {
    $path = Join-Path $root ('game/assets/hands/xrtools086/Hand_Glove_low_' + $side + '.gltf')
    $gltf = Get-Content $path -Raw | ConvertFrom-Json
    $dependencies = @()
    foreach ($uri in @($gltf.buffers | ForEach-Object { $_.uri }) + @($gltf.images | ForEach-Object { $_.uri })) {
        if (!$uri -or $uri.StartsWith('data:')) { continue }
        $dep = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($path)) $uri))
        if (!$dep.StartsWith(([IO.Path]::GetDirectoryName($path) + '\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Escaping GLTF dependency' }
        $dependencies += @{ path=$dep; sha256=(Get-FileHash $dep).Hash }
    }
    $names = @($gltf.nodes | ForEach-Object { $_.name })
    $receipts += [ordered]@{ path=$path; sha256=(Get-FileHash $path).Hash; side=$side; wristPresent=(('Wrist_' + $side) -cin $names); knucklePresent=(('Middle_Proximal_' + $side) -cin $names); dependencies=$dependencies; approval='UNREVIEWED_SOURCE_REFERENCE' }
}
$sources = @('game/scripts/player_body.gd','quest-r22/source/scripts/player_body.gd','game/scripts/gemini_player_models_099.gd','quest-r22/source/scripts/gemini_player_models_099.gd','game/scripts/grip_hand_086.gd','quest-r22/source/scripts/grip_hand_086.gd') | ForEach-Object { Get-FileHash (Join-Path $root $_) | Select-Object Path,Hash }
[ordered]@{ schema=1; baseline='bdba6e30012e1733efd5021fd8ded593f82f26e6'; sourceFiles=$sources; assets=$receipts; acceptance='NONE: source inspection only' } | ConvertTo-Json -Depth 12 | Set-Content $reportPath
