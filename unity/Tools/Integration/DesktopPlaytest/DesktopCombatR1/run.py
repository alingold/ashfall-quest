import pathlib, json, hashlib, subprocess, sys, shutil
ROOT=pathlib.Path(__file__).resolve().parents[5]
TOOLS=pathlib.Path(__file__).resolve().parent
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/DesktopCombatR1'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/DesktopCombatR1'
CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    label=sys.argv[1];out=STAGE/label;out.mkdir(parents=True,exist_ok=False);DOC.mkdir(parents=True,exist_ok=True)
    # Capture policies and exact immutable root index validation separately from current closure.
    reads=[]
    docs=[ROOT/'docs'/n for n in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md']]
    base=ROOT/'docs/agent-runs/20261104-three-chats/unity'
    docs+=list((ROOT/'unity/Documentation').glob('*.md'))+[ROOT/'docs/agent-runs/20261104-0020/preflight.json',base/'GAMEPLAY-CHECKPOINT-v1.md']+list(base.glob('accepted-neutral-v*.json'))
    docs += [base/'phase16'/n for n in ['source-index.json','dispatch-plan.json']]+[base/'phase18'/n for n in ['source-index.json','root-manifest.json','ROOT-REPORT.md','dispatch-plan.json']]
    docs += [base/n/'REPORT.md' for n in ['InventoryCheckpointR2','WeaponPreparedFireR1','SourceLaunchProjectilesR4']]
    for p in docs:
        content=p.read_bytes().decode('utf-8-sig',errors='replace');reads.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p),'bytes':p.stat().st_size})
        if p.suffix=='.json':json.loads(content)
    pins=[]
    for phase in ['phase16','phase18']:
        index=json.loads((base/phase/'source-index.json').read_text(encoding='utf-8-sig'))
        for entry in index:
            p=ROOT/entry['snapshot'];pins.append({'phase':phase,'path':entry.get('path',entry.get('original')),'matches':sha(p).lower()==entry['sha256'].lower()})
    assert all(p['matches'] for p in pins)
    (out/'policy-reads.json').write_text(json.dumps(reads,indent=2));(out/'root-pins.json').write_text(json.dumps(pins,indent=2))
    sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']
    for folder in ['RecoveryContracts','Modules/WeaponHandling','Modules/HealthStates','Modules/Combat']:
        sources+=sorted((ROOT/'unity/Assets/Ashfall'/folder).glob('*.cs'))
    sources+=sorted((ROOT/'unity/Assets/Ashfall/Integration/DesktopPlaytest/Combat').glob('*.cs'))
    sources+=[ROOT/'unity/Assets/Ashfall/Integration/DesktopPlaytest/DesktopPlaytestContext.cs']+sorted(TOOLS.glob('*.cs'))+[pathlib.Path(__file__).resolve()]
    inputs=[];frozen=[]
    for i,p in enumerate(sources):
        dest=out/'sources'/('%03d-'%i+p.name);dest.parent.mkdir(exist_ok=True);shutil.copyfile(p,dest)
        inputs.append({'path':str(p.relative_to(ROOT)),'snapshot':str(dest.relative_to(ROOT)),'sha256':sha(dest)});frozen.append(dest)
    (out/'inputs.json').write_text(json.dumps(inputs,indent=2))
    results=[]
    def run(name,cmd):
        r=subprocess.run([str(x) for x in cmd],capture_output=True,text=True);results.append({'name':name,'command':[str(x) for x in cmd],'exit':r.returncode,'output':r.stdout+r.stderr});(out/'results.json').write_text(json.dumps(results,indent=2));print(name,r.returncode,r.stdout,r.stderr);assert r.returncode==0
    cs=[p for p in frozen if p.suffix=='.cs' and not p.name.endswith('NativeGate.cs')]
    def compile(name,args,files):
        rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+']+([] if '/nostdlib+' in args else ['/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll'])+args+['"'+str(p)+'"' for p in files]));run(name,[CSC,'/noconfig','@'+str(rsp)])
    # Deliberate external consumer graph: no internal access hidden by a merged test.
    built=[]
    groups=[('Core','/Core/'),('Recovery','/RecoveryContracts/'),('Weapon','/Modules/WeaponHandling/'),('Health','/Modules/HealthStates/'),('Combat','/Modules/Combat/'),('DesktopCombat','/DesktopPlaytest/Combat/')]
    for name,part in groups:
        files=[frozen[i] for i,x in enumerate(inputs) if part in x['path'].replace('\\','/')]
        if name=='Combat':files=[p for p in files if not p.name.endswith('CheckpointState.cs')]
        compile(name,['/target:library','/out:"'+str(out/(name+'.dll'))+'"']+['/reference:"'+str(p)+'"' for p in built],files)
        built.append(out/(name+'.dll'))
    compile('behavior',['/target:exe','/out:"'+str(out/'behavior.exe')+'"']+['/reference:"'+str(p)+'"' for p in built],[p for p in cs if p.name.endswith('BehaviorTests.cs')]);run('behavior-run',[out/'behavior.exe'])
    # Native API closure uses installed netstandard profile throughout; no Unity stubs.
    compile('neutral',['/target:library','/out:"'+str(out/'neutral.dll')+'"'],[p for p in cs if not p.name.endswith('BehaviorTests.cs')])
    managed=pathlib.Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed')
    refs=list((managed/'UnityEngine').glob('UnityEngine.*.dll'))+list((managed/'UnityEditor').glob('UnityEditor.*.dll'))
    refs += list((managed/'UnityEngine').glob('UnityEditor.*.dll'))
    refs += list((managed/'UnityEngine').glob('UnityEngine.dll'))
    # Unity's netstandard profile prevents framework/engine type-identity mixing.
    profile=managed.parent/'NetStandard/ref/2.1.0'
    refs+=list(profile.glob('*.dll'))
    inputdll=ROOT/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies/Unity.InputSystem.dll'
    refs+=[inputdll]
    args=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER','/target:library','/out:"'+str(out/'native.dll')+'"']+['/reference:"'+str(p)+'"' for p in refs]
    native=[p for p in frozen if p.suffix=='.cs' and not p.name.endswith('BehaviorTests.cs')]
    compile('native',args,native)
    (out/'references.json').write_text(json.dumps([{'path':str(p),'sha256':sha(p)} for p in refs],indent=2))
    (out/'current-comparison.json').write_text(json.dumps([{'path':x['path'],'matches':sha(ROOT/x['path'])==x['sha256']} for x in inputs],indent=2))
    root_index=json.loads((base/'phase18/source-index.json').read_text(encoding='utf-8-sig'))
    root_by_path={x['path'].replace('\\','/'):x for x in root_index}
    (out/'root-compatibility.json').write_text(json.dumps([{'path':x['path'],'phase18_present':x['path'].replace('\\','/') in root_by_path,'phase18_matches':root_by_path.get(x['path'].replace('\\','/'),{}).get('sha256','').lower()==x['sha256']} for x in inputs],indent=2))
    (out/'SHA256.json').write_text(json.dumps([{'path':str(p.relative_to(ROOT)),'sha256':sha(p)} for p in out.rglob('*') if p.is_file()],indent=2))
if __name__=='__main__':main()




