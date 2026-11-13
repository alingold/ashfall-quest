from pathlib import Path
import hashlib,json,re
r=Path.cwd(); out=r/'campaign-staging/20261104-three-chats/unity/SaveMigration/phase03/inputs'
assert not (r/'docs/agent-runs/20261104-three-chats/unity/SaveMigration/phase03/inputs.json').exists(), 'Frozen inputs already exist; use a new version, never overwrite'
paths=['unity/Assets/Ashfall/Core/Contracts.cs','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json']
paths += ['unity/Assets/Ashfall/Modules/Persistence/'+n for n in ['Ports.cs','AtomicSettingsStore.cs','JsonTokens.cs']]
for tree in ['quest-r22/source','game']:
 for n in ['save_store.gd','campaign_progress.gd','front_menu1000.gd','net_rules1000.gd','arsenal_data.gd','map_runtime_rules_r14.gd']:
  if (r/tree/'scripts'/n).exists():paths.append(tree+'/scripts/'+n)
 paths.append(tree+'/assets/data/one_star_050.json')
records=[]
accepted=json.loads((r/paths[4]).read_text(encoding='utf-8-sig'))
for p in paths:
 b=(r/p).read_bytes(); dest=out/p;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 records.append(dict(path=p,sha256=hashlib.sha256(b).hexdigest().upper(),snapshot=str(dest.relative_to(r))))
# Namespace-local parser: copied reviewed algorithm, no access to Persistence internals.
p=r/'unity/Assets/Ashfall/Modules/SaveMigration/RawJson.cs'
p.write_text('// Derived from pinned reviewed Persistence JsonTokens; namespace-local parser, not a shared API.\n'+(r/paths[7]).read_text(encoding='utf-8-sig').replace('Ashfall.Modules.Persistence','Ashfall.Modules.SaveMigration').replace('JsonTokens','RawJson'),encoding='utf-8')
# Confirm exact accepted public dependency hashes, fail closed on drift.
for row in records:
 if '/Modules/Persistence/' in row['path']:
  assert any(x['expected']==row['sha256'] and x['actual']==row['sha256'] for x in accepted['sourceChecks'])
(r/'docs/agent-runs/20261104-three-chats/unity/SaveMigration/phase03/inputs.json').write_text(json.dumps(records,indent=2))
# Source constants preserve both rule profiles; only valid JSON numeric spelling adapted from GDScript.
b=[]
for tree,name in [('quest-r22/source','Quest'),('game','SkinLibrary')]:
 s=(r/tree/'scripts/net_rules1000.gd').read_text()
 for key in ['DEFAULTS','LIMITS','CHOICES']:
  x=re.search(r'const '+key+r' := (.*)',s).group(1)
  x=re.sub(r'(?<=[\[,]):?\.(\d)',r'0.\1',x)
  b.append('internal const string '+name+key+' = @"'+x.replace('"','""')+'";')
m=json.loads((r/'quest-r22/source/assets/data/one_star_050.json').read_text())
for name,obj in [('Explosives',m['original_tuning']),('Rates',{p['id']:p['rounds_per_second'] for p in m['profiles']})]:
 b.append('internal const string '+name+' = @"'+json.dumps(obj,separators=(',',':')).replace('"','""')+'";')
(r/'unity/Assets/Ashfall/Modules/SaveMigration/SourceSchemas.cs').write_text('namespace Ashfall.Modules.SaveMigration { internal static class SourceSchemas {\n'+'\n'.join(b)+'\n} }')
