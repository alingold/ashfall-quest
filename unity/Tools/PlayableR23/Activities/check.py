from pathlib import Path
import hashlib,json,subprocess,uuid
ROOT=Path(__file__).resolve().parents[4]
TOOLS=Path(__file__).resolve().parent
BASE=ROOT/'unity/Assets/Ashfall'
CSC=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[BASE/'Core/Contracts.cs']
for folder in ['Modules/Construction','Modules/ConstructionCandidate','Modules/Flight','Modules/Player']:
    sources+=sorted((BASE/folder).glob('*.cs'))
sources+=[BASE/'Modules/InventoryItems/BuildChargeLedger.cs',BASE/'Integration/Playable/Activities/R23ActivityState.cs']
def run(name,files,extra):
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+']+extra+['/out:'+str(TOOLS/name)]+[str(p) for p in files]
    rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"' for p in args))
    r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True)
    print(name,r.returncode,r.stdout,r.stderr)
    (TOOLS/(name+'.log')).write_text(r.stdout+r.stderr)
    assert r.returncode==0
run('mechanics.exe',sources+[TOOLS/'Mechanics.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'mechanics.exe')],capture_output=True,text=True)
print(r.stdout,r.stderr);(TOOLS/'mechanics-results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0
editor=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
run('native.dll',sources+[BASE/'Integration/Playable/Activities/R23VRActivities.cs'],['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
smoke=BASE/'Editor/Playable/R23ConstructionDamageSmoke.cs'
run('construction-damage-native-fixture.dll',sources+[BASE/'Integration/Playable/Activities/R23VRActivities.cs',smoke],['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER,UNITY_EDITOR']+['/reference:'+str(p) for p in refs])
meta=smoke.with_name(smoke.name+'.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Construction';provenance=json.loads((payload/'provenance.json').read_text());source=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
for relative,digest in provenance['inputs'].items():assert hashlib.sha256((source/relative).read_bytes()).hexdigest()==digest
assert (payload/'build_panel_user.png').read_bytes()==(source/'assets/materials/build_panel_user.png').read_bytes()
print('Source panel finish byte-identical494944B; native ray/damage/collider/rebuild fixture staged/API-compiled only, not engine-run')
