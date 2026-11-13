from pathlib import Path
import hashlib,json,re,subprocess
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;R=T.parents[5];d=json.loads((T/'source-recipe.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for row in d['sourceFiles']:
 p=Path(row['path']);p=p if p.is_absolute()else R/p
 assert p.stat().st_size==row['bytes']and sha(p)==row['sha256'],p
source=(R/d['sourceFiles'][0]['path']).read_text();assert d['sourceScriptSha256']=='abb06da6c542a3e04ac8f0baf497ec988028b5eb28eef1fc0ea0e6df2459f448';assert 'viewport.size=Vector2i(960,960)'in source and'camera.fov=38;camera.near=.02'in source
assert [(s['id'],s['sourceIndex'])for s in d['styles']]==[('prowler',81),('stacy_throwback',73),('anna_tabby',25),('tox',42),('haze_nuclear',36),('sir_box_a_lot',60),('pj_golden',103)]
count=0;S=np.diag([1.,1.,-1.]);up=np.array([0.,1.,0.]);unit=lambda p:p/np.linalg.norm(p);points=[(0,1.68,0),(.2,1.2,-.17),(-.26,.97,-.04),(.26,.97,-.04),(-.13,.08,-.03),(.13,.08,-.03)]
for style in d['styles']:
 for shot in style['shots']:
  name=shot['name'];position=shot['godotPosition'];target=shot['godotTarget'];expected='["'+name+'",Vector3('+','.join(str(v)for v in position)+'),Vector3('+','.join(str(v)for v in target)+')]'
  # Parse the executed literal instead of relying on document prose.
  match=re.search(r'\["'+name+r'",Vector3\(([^)]+)\),Vector3\(([^)]+)\)\]',source);assert match
  assert np.array_equal(position,[float(x)for x in match[1].split(',')])and np.array_equal(target,[float(x)for x in match[2].split(',')])
  if shot['reference']:
   p=R/shot['reference'];assert sha(p)==shot['referenceSha256']and Image.open(p).size==(960,960);assert style['id']in['prowler','stacy_throwback']and shot['classification']=='matched-source-camera'
  else:assert name=='front'and shot['classification']=='source-labelled-probe'
  p=np.array(position);f=unit(np.array(target)-p);right=unit(np.cross(f,up));camera_up=unit(np.cross(right,f));uf=S@f;ur=unit(np.cross(up,uf));uu=unit(np.cross(uf,ur));assert np.allclose(ur,S@right)and np.allclose(uu,S@camera_up)
  for point in points:
   v=np.array(point)-p;uv=S@v;a=np.array([right@v,camera_up@v])/(f@v*np.tan(np.deg2rad(38)/2));b=np.array([ur@uv,uu@uv])/(uf@uv*np.tan(np.deg2rad(38)/2));assert np.allclose(a,b,atol=1e-12);count+=1
# Exact default Godot YXZ directional rotation reflected once, not negated twice.
a=np.deg2rad(35);Rx=lambda x:np.array([[1,0,0],[0,np.cos(x),-np.sin(x)],[0,np.sin(x),np.cos(x)]]);Ry=lambda y:np.array([[np.cos(y),0,np.sin(y)],[0,1,0],[-np.sin(y),0,np.cos(y)]]);assert np.allclose(S@(Ry(-a)@Rx(-a))@S,Ry(a)@Rx(a),atol=1e-12)
assert sum(len(s['shots'])for s in d['styles'])==11
print('PASS14 source file pins/six genuine960 PNGs/executed camera literals/seven stable live style IDs;'+str(count)+' projected canonical body points and reflected source sun; no engine/captures.')
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');args=[]
for row in (R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.BuildAndSmoke.rsp').read_text(encoding='utf-8-sig').splitlines():
 a=row.strip().strip('"')
 if a.startswith('/out:'):a='/out:'+str(T/'MatchedCapture.BuildAndSmoke.dll')
 args.append(a)
args=[a for a in args if not a.lower().endswith('.cs')or Path(a).name!='R24MatchedAppearanceCapture.cs'];host=R/'unity/Assets/Ashfall/Editor/Playable/R24AppearanceSmoke.cs'
if host.exists()and not any(Path(a).name==host.name for a in args):args.append(str(host))
args.append(str(T/'R24MatchedAppearanceCapture.cs'));rsp=T/'api.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));q=subprocess.run([str(E/'NetCoreRuntime/dotnet.exe'),str(E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'api.log').write_text(q.stdout+q.stderr);print(q.stdout+q.stderr);assert q.returncode==0;print('PASS staged actual BrowseAppearance/InspectPresentation capture full installed BuildAndSmoke API compile; native UNRUN.')
