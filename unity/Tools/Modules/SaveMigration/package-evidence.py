from pathlib import Path
import hashlib,json,shutil
r=Path.cwd();e=r/'docs/agent-runs/20261104-three-chats/unity/SaveMigration/phase03';o=r/'campaign-staging/20261104-three-chats/unity/SaveMigration/phase03'
rows=json.loads((e/'inputs.json').read_text());drift=[]
for x in rows:
 actual=hashlib.sha256((r/x['path']).read_bytes()).hexdigest().upper()
 drift.append(dict(path=x['path'],expected=x['sha256'],actual=actual,match=actual==x['sha256']))
(e/'input-drift.json').write_text(json.dumps(drift,indent=2))
for folder in ['unity/Assets/Ashfall/Modules/SaveMigration','unity/Tools/Modules/SaveMigration']:
 for p in (r/folder).glob('*'):
  if p.is_file():
   target=o/'candidate'/folder/p.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
maprows=[
('save_store.gd','_inspect, read_save, write_save','SemanticMigration.Read/Family, accepted AtomicSettingsStore','all four source slots scanned; source never written; Unity File.Replace is accepted store behavior'),
('campaign_progress.gd','_inspect, _read_family, read_progress','SemanticMigration.Campaign','same campaign ID and exact missions, expanded-first, legacy chapter mapping, full-family identity protection'),
('front_menu1000.gd','_save_preset1000, _load_preset1000','SemanticMigration.Lobby','unversioned dictionary imported through dedicated rule validation'),
('net_rules1000.gd','DEFAULTS, LIMITS, CHOICES, validate','SourceSchemas + SemanticMigration.Lobby','both default profiles retained; explicit IMapAvailability for archive/community'),
('arsenal_data.gd','tuning, apply','SemanticMigration.Tuning','original_tuning and profile IDs/rates; clamp .05..60/240 and .2..40; matadors factor after clamp'),
('map_runtime_rules_r14.gd','_same, personal_settings','PersonalSettingsPolicy.ForSave/ForSourceSave','Quest offline only; game file absent, deliberately no offline stripping'),
('net_bridge1000.gd','personal_settings','PersonalSettingsPolicy.ForSourceSave','both trees save cached seven personal rule keys during live sessions'),
('main.gd','_save_settings, _load_progress, _save_progress','coordinator call sites remain unbound','source chooses expanded opt-in and personal policy; no shared runtime edits')]
(e/'source-symbol-map.json').write_text(json.dumps([dict(source=a,symbols=b,candidate=c,behavior=d) for a,b,c,d in maprows],indent=2))
print('Drift:',[x['path'] for x in drift if not x['match']])
for name in ['one_star_050.json','arsenal_data.gd','save_store.gd','campaign_progress.gd']:
 paths=[p for p in rows if p['path'].endswith('/'+name)]
 print(name,[(p['path'],p['sha256']) for p in paths])
