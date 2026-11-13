"""Run with bundled Python; never invokes Unity, UPM or an engine."""
import hashlib,json,pathlib,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[4]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/ProjectilesR1/phase05'
EVIDENCE=ROOT/'docs/agent-runs/20261104-three-chats/unity/ProjectilesR1/phase05'
run=EVIDENCE/(sys.argv[1] if len(sys.argv)>1 else 'run01')
run.mkdir(exist_ok=False)
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
before=STAGE/'before'
records=json.loads((EVIDENCE/'inputs.json').read_text(encoding='utf-8-sig'))
for record in records:
    assert sha(before/record['path']).upper()==record['sha256'],record['path']
# Golden fixture values derive from the sealed real source JSON, not the candidate.
quest=before/'quest-r22/source'
data=json.loads((quest/'assets/data/one_star_050.json').read_text(encoding='utf-8-sig'))
rarity=json.loads((quest/'assets/data/weapon_rarity_r6.json').read_text(encoding='utf-8-sig'))
assert [(p['index'],p['id']) for p in data['profiles'] if p['id'] in ('frag','smaw','zone')]==[(26,'frag'),(27,'smaw'),(28,'zone')]
assert rarity['weapons']['smaw']['body']==[60,84,109,134,160]
t=data['original_tuning'];assert t['frag_fuse_seconds']==3.5 and t['zone_tick_seconds']==.5 and t['zone_activation_delay']==2.7
fixture={'catalog':{'26':'frag','27':'smaw','28':'zone'},'tuning':t,'smaw_damage':rarity['weapons']['smaw']['body'],'zone_ticks':[{'at':i*.5,'radius':15*(1-i/20),'power':5} for i in range(1,21)]}
(run/'golden-source-fixtures.json').write_text(json.dumps(fixture,indent=2))
reconcile=[]
for rel in ('scripts/net_projectile_proxy1000.gd','scripts/explosive_projectile.gd','scripts/net_bridge1000.gd','scripts/net_session1000.gd','scripts/explosive_weapon.gd','assets/data/one_star_050.json','assets/data/weapon_rarity_r6.json'):
    a=before/'game'/rel;b=quest/rel
    row={'relative':rel,'game_sha256':sha(a),'quest_sha256':sha(b),'whole_file_equal':a.read_bytes()==b.read_bytes()}
    if rel.endswith('net_bridge1000.gd'):
        def explosion(p): return p.read_text(encoding='utf-8-sig').split('func explosion(',1)[1].split('\nfunc ',1)[0]
        row['consumed_explosion_symbol_equal']=explosion(a)==explosion(b)
    reconcile.append(row)
(run/'source-reconciliation.json').write_text(json.dumps(reconcile,indent=2))
sources=[before/'unity/Assets/Ashfall/Core/Contracts.cs',before/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
sources+=sorted((before/'unity/Assets/Ashfall/Modules/Combat').glob('*.cs'))
sources+=sorted((before/'unity/Assets/Ashfall/Modules/HealthStates').glob('*.cs'))
candidate=STAGE/'sealed-candidate'
if candidate.exists():
    for row in json.loads((EVIDENCE/'candidate-sources.json').read_text(encoding='utf-8-sig')):
        assert sha(candidate/row['path']).upper()==row['sha256'],row['path']
else: candidate=ROOT
sources+=sorted((candidate/'unity/Assets/Ashfall/Modules/Projectiles').glob('*.cs'))
sources+=[candidate/'unity/Tools/Modules/Projectiles/ProjectileTests.cs']
out=STAGE/run.name;out.mkdir(exist_ok=False)
csc=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
exe=out/'ProjectileTests.exe'
args=[str(csc),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+[str(p) for p in sources]
(run/'compile-command.txt').write_text(' '.join('"'+a+'"' for a in args))
result=subprocess.run(args,capture_output=True,text=True)
(run/'compile.log').write_text(result.stdout+result.stderr)
if result.returncode: print(result.stdout+result.stderr);sys.exit(result.returncode)
result=subprocess.run([str(exe)],capture_output=True,text=True)
(run/'tests.log').write_text(result.stdout+result.stderr)
drift=[{'path':r['path'],'sealed_sha256':r['sha256'],'current_sha256':sha(ROOT/r['path']),'changed':sha(ROOT/r['path']).upper()!=r['sha256']} for r in records]
(run/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
(run/'consumed-sources.json').write_text(json.dumps([{'path':str(p),'sha256':sha(p)} for p in sources],indent=2))
print(result.stdout+result.stderr);sys.exit(result.returncode)
