from pathlib import Path
import subprocess
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
A=ROOT/'unity/Assets/Ashfall'
files=sorted(set([ROOT/x for x in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines() if x]+list((A/'Integration/Playable/HandsWeapons').glob('*.cs'))))+[HERE/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in files]
rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+x+'"' for x in args))
r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True)
(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True)
(HERE/'cases.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
# Exact cleanup scope: never retire/reset a native owner; dormant cache records stay
# claimable. Only legacy starter-home identities are withdrawn at match loadout.
text=(A/'Integration/Playable/HandsWeapons/R23HandsWeapons.MatchHealth.cs').read_text()
cleanup=text.split('foreach(var id in starterWorldAtHome){',1)[1].split('starterWorldAtHome.Clear();',1)[0]
assert 'droppedWeapons.Remove(id)' in cleanup and 'ground.Visual.gameObject.SetActive(false)' in cleanup
assert 'Retire' not in cleanup and 'Reset' not in cleanup and 'SetWorldWeaponVisible(id,false)' not in text
print('Legacy starter-only withdrawal/source guard PASS; world acquisition/PhysX native confirmation remains pending.')
