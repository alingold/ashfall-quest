from pathlib import Path
import hashlib,json,struct,subprocess
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent;BASE=ROOT/'unity/Assets/Ashfall';SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/JumpPads'
data=json.loads((OUT/'pads.json').read_text());provenance=json.loads((OUT/'provenance.json').read_text());sem=json.loads((SRC/'assets/data/map_semantics_r14.json').read_text())['maps']
for relative,digest in provenance['inputs'].items():assert hashlib.sha256((SRC/relative).read_bytes()).hexdigest()==digest
for m in data['maps']:
    if m['mapId'] in ['range','valley']:
        source=SRC/('scripts/sandbox_map_091.gd' if m['mapId']=='range' else 'scripts/world.gd');assert not m['pads'] and m['sourceSha256']==hashlib.sha256(source.read_bytes()).hexdigest();continue
    source=sem[m['mapId'].removeprefix('archive:')];assert m['sourceSha256']==source['source_sha256'] and len(m['pads'])==len(source['jump'])
    for i,(a,b) in enumerate(zip(m['pads'],source['jump'])):
        center=list(b['center']);normal=list(b['normal']);center[2]*=-1;normal[2]*=-1
        assert a['sourceIndex']==i and a['center']==center and a['normal']==normal and a['radius']==b['radius'] and a['speed']==b.get('speed',45)
def vector(a):
    a=list(a);a[2]*=-1
    return 'Mechanics.V('+','.join('0x%08Xu'%struct.unpack('<I',struct.pack('<f',v))[0] for v in a)+')'
cases=json.loads((TOOLS/'Reference/pad_contact_cases_r24.json').read_text())
lines=['using Ashfall.Integration.Playable.JumpPads;','static class SourceFixtures {','public static readonly Mechanics.Fixture[] Cases=new Mechanics.Fixture[]{']
for c in cases:
    lines.append('new Mechanics.Fixture{Label='+json.dumps(c['label'])+',Start='+vector(c['start'])+',End='+vector(c['end'])+',Center='+vector(c['center'])+',Normal='+vector(c['normal'])+',Radius='+repr(c['radius'])+',Expected='+str(c['expected']).lower()+'},')
lines+=['};','public static readonly R23SourcePad[] Alpha=new R23SourcePad[]{']
for i,p in enumerate(sem['alfa-aglae-souza']['jump']):lines.append('new R23SourcePad("alpha/'+str(i)+'",'+vector(p['center'])+','+vector(p['normal'])+','+repr(p['radius'])+','+repr(p['speed'])+'f,'+str(p.get('tilted',False)).lower()+'),')
lines+=['};','}'];fixture=TOOLS/'SourceFixtures.cs';fixture.write_text('\n'.join(lines))
sources=[BASE/'Core/Contracts.cs']+list((BASE/'Modules/Flight').glob('*.cs'))+[BASE/'Integration/Playable/JumpPads/R23PadContacts.cs'];csc=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def compile(name,files,extra):
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extra+[str(p) for p in files];rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+x+'"' for x in args));r=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);print(name,r.returncode,r.stdout,r.stderr);assert r.returncode==0
compile('mechanics.exe',sources+[TOOLS/'Mechanics.cs',fixture],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'mechanics.exe')],capture_output=True,text=True);(TOOLS/'mechanics-results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
editor=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
compile('JumpPads.Native.dll',sources+[BASE/'Integration/Playable/JumpPads/R23JumpPads.cs'],['/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
