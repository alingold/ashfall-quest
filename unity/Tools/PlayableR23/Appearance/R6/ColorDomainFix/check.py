from pathlib import Path
import hashlib,json,struct,subprocess
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;R=T.parents[5];P=R/'unity/Assets/StreamingAssets/PlayableR23/Appearance';S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();before=json.loads((T/'payload-before.json').read_text());after={p.name:sha(p)for p in P.iterdir()if p.is_file()};assert before==after,'Preserve every existing115 payload/meta byte'
d=json.loads((P/'stacy_throwback.json').read_text());m=d['materials'][d['gloveMaterialIndex']];pixels=np.array(Image.open(P/m['baseColor']).convert('RGB'));assert np.all(pixels==[201,140,115]);skin=np.array(d['materials'][2]['color'][:3]);encoded=np.where(skin<=.0031308,skin*12.92,1.055*skin**(1/2.4)-.055);assert np.array_equal(np.rint(encoded*255),[201,140,115]);assert d['materials'][2]['baseColor']==''and m['color']==[1,1,1,1]
for id in ['prowler','stacy_throwback']:
 path=S/'assets/models/custom_skins'/id/('player_kit_'+id+'_light.glb')
 with path.open('rb')as f:
  f.seek(12);length=struct.unpack('<I',f.read(4))[0];f.read(4);g=json.loads(f.read(length))
 assert all('COLOR_0'not in p['attributes']for mesh in g['meshes']for p in mesh['primitives']),'No missing source vertex-color contribution for demonstrated pair'
 actual=json.loads((P/(id+'.json')).read_text());assert all(a['color']==b.get('pbrMetallicRoughness',{}).get('baseColorFactor',[1,1,1,1])for a,b in zip(actual['materials'],g['materials']))
print('PASS exact source body factors, genuine constant64px glove albedo equality, no vertex-color source for pair;'+str(len(before))+' files across115 styles/meta byte-unchanged.')
helper=R/'unity/Assets/Ashfall/Integration/Playable/Appearance/R23AppearanceColorDomain.cs';exe=T/'Cases.exe';q=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe),str(helper),str(T/'Cases.cs')],capture_output=True,text=True);print(q.stdout+q.stderr);assert q.returncode==0;q=subprocess.run([str(exe)],capture_output=True,text=True);print(q.stdout+q.stderr);assert q.returncode==0
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');args=[]
for row in (R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.BuildAndSmoke.rsp').read_text(encoding='utf-8-sig').splitlines():
 a=row.strip().strip('"')
 if a.startswith('/out:'):a='/out:'+str(T/'ColorDomainFix.BuildAndSmoke.dll')
 args.append(a)
if not any(Path(a).name==helper.name for a in args):args.append(str(helper))
rsp=T/'api.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));q=subprocess.run([str(E/'NetCoreRuntime/dotnet.exe'),str(E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'api.log').write_text(q.stdout+q.stderr);print(q.stdout+q.stderr);assert q.returncode==0;print('PASS actual updated Appearance runtime+installed BuildAndSmoke API; native render UNRUN.')
