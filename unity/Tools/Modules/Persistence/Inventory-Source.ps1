param([string]$Workspace = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference = 'Stop'
$rows = @()
$files = @()
$references = @()
foreach ($tree in @('quest-r22/source','game')) {
    foreach ($name in @('main.gd','menu.gd','save_store.gd','campaign_progress.gd','front_menu1000.gd','net_rules1000.gd','net_bridge1000.gd','arsenal_data.gd','map_runtime_rules_r14.gd')) {
        $relative = "$tree/scripts/$name"
        $path = Join-Path $Workspace $relative
        if (-not (Test-Path -LiteralPath $path)) {
            $files += [ordered]@{path=$relative;state='absent in this tree'}
            continue
        }
        $lines = Get-Content -LiteralPath $path
        $files += [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
        for ($i=0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -match 'user://|SAVE_PATH|EXPANDED_SAVE_PATH|func get_settings|func apply_settings|func personal_settings|const DEFAULTS|const AUDIO_BUSES|WEAPON_SETTINGS_DEFAULTS|original_tuning') {
                $references += [ordered]@{path=$relative;line=$i+1;text=$line.Trim()}
            }
            $group = $null
            if ($name -eq 'menu.gd' -and $line -match '^\s*return \{"character_style_r8"') {$group='settings.json'}
            elseif ($name -eq 'net_rules1000.gd' -and $line -match '^const DEFAULTS') {$group='lobby-preset1000.json'}
            elseif ($name -eq 'menu.gd' -and $line -match '^const WEAPON_SETTINGS_DEFAULTS') {$group='settings.json/weapon_settings'}
            elseif ($name -eq 'menu.gd' -and $line -match '^var audio_volumes') {$group='settings.json/audio_volumes'}
            if ($null -ne $group) {
                foreach ($match in [regex]::Matches($line, '"([^"\r\n]+)"\s*:')) {
                    $rows += [ordered]@{featureId='PERSIST-'+$group+'-'+$match.Groups[1].Value;tree=$tree;file=$group;key=$match.Groups[1].Value;source=$relative;line=$i+1}
                }
            }
        }
    }
    foreach($name in @('project.godot','assets/data/one_star_050.json')) {
        $relative="$tree/$name";$path=Join-Path $Workspace $relative
        $files += [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
        if($name -like '*.json') {
            $data=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
            foreach($key in $data.original_tuning.PSObject.Properties.Name) {
                $rows += [ordered]@{featureId='PERSIST-explosive-tuning-'+$key;tree=$tree;file='explosive-tuning-overrides.json';key=$key;source="$tree/scripts/arsenal_data.gd";line=11;default=$data.original_tuning.$key}
            }
            $rows += [ordered]@{featureId='PERSIST-weapon-timing';tree=$tree;file='weapon-timing-overrides.json';keys=@($data.profiles | ForEach-Object id);source="$tree/scripts/arsenal_data.gd";line=26}
        }
    }
    foreach($file in @('campaign.json','campaign-expanded.json')) {
        $keys = @('version','chapter','kills','play_time')
        if($file -eq 'campaign-expanded.json') {$keys += @('campaign_id','mission_id')}
        foreach($key in $keys) {
            $rows += [ordered]@{featureId='PERSIST-'+$file+'-'+$key;tree=$tree;file=$file;key=$key;source="$tree/scripts/campaign_progress.gd";symbol='_inspect/write_progress (expanded); save_store._valid (legacy)';status='inventoried; campaign semantic port remains open'}
        }
    }
}
$output = [ordered]@{baseline='bdba6e30012e1733efd5021fd8ded593f82f26e6';gameBaseline='no local .git; per-file actual SHA256';files=$files;keys=$rows;references=$references;notes=@('Settings serialized keys match across trees; runtime walk clamp differs (quest max 5 vs game max 3).','Quest offline _save_settings removes map runtime overrides; game only strips network overrides.','Logs and archive_mipmaps_r8 are diagnostics/cache, not player settings.','Godot checkpoint .pending/.backup/.backup.pending families are immutable inputs; semantic campaign migration remains open.','Weapon timing keys are actual profile IDs, not display labels.')}
$output | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Workspace 'docs/agent-runs/20261104-three-chats/unity/Persistence/source-inventory.json') -Encoding UTF8
Write-Output ("Inventory: {0} key rows, {1} source hashes, {2} references" -f $rows.Count,$files.Count,$references.Count)
