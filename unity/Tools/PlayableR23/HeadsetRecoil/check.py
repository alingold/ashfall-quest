from pathlib import Path
import json,subprocess,hashlib,re
T=Path(__file__).resolve().parent;R=T.parents[3];P=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';S=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
ids=['mk18','akm','spr','magnum','cx4','px4','m1014','ump','p90','mp5','mp9','1911','tec9','rfb','fal','s85']
# Expected source contact is data-derived; source script overrides below are explicit original branches.
source_player=(S/'scripts/player.gd').read_text();source_fit=(S/'scripts/playtest_fit_088.gd').read_text();source_download=(S/'scripts/downloaded_firearm_model.gd').read_text()
assert 'center=Vector3(0,-.065,-.13)'in source_player and 'center=Vector3(0,-.045,-.065)'in source_player and 'id=="tec9":w.set_meta("primary_grip_084",Vector3(0,-.057,-.025))'in source_download
assert 'if id=="mp5":offset.y=-.0635'in source_fit and 'offset+=Vector3(0,.0762,-.0762)'in source_fit and 'return offset/weapon.model.global_basis.get_scale().x'in source_fit
physical={'px4':[0,0,-.0381],'magnum':[0,0,-.05715],'m1014':[0,.0381,-.0381],'p90':[0,0,-.0381],'mp5':[0,-.0635,0],'1911':[0,-.0254,0],'s85':[0,.0762,-.0508],'mp9':[0,0,.01905],'mk18':[0,-.0127,-.0127],'akm':[0,0,-.0127]}
def v(a):return 'new Float3('+','.join(format(float(x),'.9g')+'f'for x in a)+')'
rows=[]
for name in ids:
 point=[0,-.065,-.13]if name=='px4'else[0,-.045,-.065]if name=='magnum'else[0,-.057,.025]
 path=S/'assets/models'/('download_s85_m1_099'if name=='s85'else'download_'+name)/'asset-provenance.json'
 if path.exists():point=json.loads(path.read_text()).get('primary_grip',point)
 if name=='tec9':point=[0,-.057,-.025]
 point=[point[0],point[1],-point[2]]
 rows.append('new Row("'+name+'",'+v(point)+','+v(physical.get(name,[0,0,0]))+','+('35f*(float)System.Math.PI/180'if name=='s85'else'0')+')')
(T/'ExpectedGripRows.cs').write_text('using Ashfall.Core;static class ExpectedGripRows {public sealed class Row{public string Id;public Float3 Primary,Offset;public float Pitch;public Row(string i,Float3 p,Float3 o,float r){Id=i;Primary=p;Offset=o;Pitch=r;}}public static readonly Row[] Rows={'+','.join(rows)+'};}\n')
sources=sorted(set([R/p for p in (R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list(P.glob('*.cs'))))
CSC='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
def compile(name,files,options):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(T/name)]+options+[str(p)for p in files];rsp=T/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"'for a in args));result=subprocess.run([CSC,'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/(name+'.log')).write_text(result.stdout+result.stderr);assert result.returncode==0,result.stdout+result.stderr
base=['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']
compile('Cases.exe',sources+[T/'Cases.cs',T/'ExpectedGripRows.cs'],base)
r=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);(T/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((E/'Managed/UnityEngine').glob('*.dll'))+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))
for defines,name in [('UNITY_6000_0_OR_NEWER','Hands.Runtime.dll'),('UNITY_6000_0_OR_NEWER;UNITY_EDITOR','Hands.Editor.dll')]:compile(name,sources,['/nostdlib+','/target:library','/define:'+defines]+['/reference:'+str(p)for p in refs]);print('PASS installed Unity API',name)
for label,files,extra in [('Instances222',[R/'unity/Tools/PlayableR23/InstanceCopies/Cases.cs'],[]),('Rifle97',[R/'unity/Tools/PlayableR23/HandsWeapons'/n for n in ['Mechanics.cs','SourceMechanics.cs','BothHandsAndGains.cs','SprMechanics.cs']],[]),('Smg71',[R/'unity/Tools/PlayableR23/Smg4/Cases.cs'],[]),('Shotgun43',[R/'unity/Tools/PlayableR23/ArsenalGameplay/Cases.cs'],[]),('Catalog100',[R/'unity/Tools/PlayableR23/Catalog5/Cases.cs'],[])]:
 compile(label+'.exe',sorted(set(sources+files)),base+extra);r=subprocess.run([str(T/(label+'.exe'))],capture_output=True,text=True);(T/(label+'-results.txt')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr;print('PASS retained',label)
# Production bindings: accepted pose precedes next kick, no neutral muzzle firing path.
hands=(P/'R23HandsWeapons.cs').read_text();pose=(P/'R23HandsWeapons.DisplayedPose.cs').read_text();scope=(P/'R23SprScope.cs').read_text();shader=(P/'Resources/R23SprScope.shader').read_text();gun=(P/'R23GunFeedback.cs').read_text()
assert 'GripPoint(int index){return Vector3.zero;}'in hands and 'Vector3 origin=From(acceptedPose.Origin),direction=From(acceptedPose.Forward)'in hands
assert hands.index('var acceptedPose=displayedShot;')<hands.index('ActiveFeedback.AcceptedShot(afterState.Shots')
assert '[BeforeRenderOrder(1100)]'in pose and '[BeforeRenderOrder(1200)]'in scope and 'Application.onBeforeRender-=RefreshLateTrackedPresentation'in hands
assert 'views[i].cullingMask=1|(1<<19)'in scope and 'target=sharedAim()'in scope and 'flash.gameObject.layer=18'in gun
assert 'UNITY_UV_STARTS_AT_TOP'in shader and '_OpticalLeft_TexelSize'in shader
inputs=['scripts/weapon.gd','scripts/player.gd','scripts/playtest_fit_088.gd','scripts/downloaded_firearm_model.gd','scripts/spr.gd','scripts/spr_scope.gd','scripts/reticle_bounce_071.gd']
(T/'source-inputs.json').write_text(json.dumps({'source':str(S),'sha256':{n:hashlib.sha256((S/n).read_bytes()).hexdigest()for n in inputs},'engine':False,'device':False,'gpu':False},indent=2)+'\n');print('PASS exact production displayed-pose / source-fit / scope-layer bindings; no engine/GPU/device')
