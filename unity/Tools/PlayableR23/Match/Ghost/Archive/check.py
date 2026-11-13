from pathlib import Path
import hashlib,json,math,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5];LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable';STAGE=HERE/'Staged';S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();payloadPath=HERE/'Payload/maps-post-stage.json';payloadPath=payloadPath if payloadPath.exists()else HERE/'Payload/maps.json';payload=json.loads(payloadPath.read_text());launch={r['id']:r for r in json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch/launch-maps.json').read_text())['rows']}
pins={r['path']:r['sha256']for r in json.loads((S.parent/'SOURCE-MANIFEST.json').read_text())['files']}
for p,h in payload['sourceInputs'].items():assert sha(S/p)==h==pins[p]
sem=json.loads((S/'assets/data/map_semantics_r14.json').read_text())['maps'];L=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
def values(p):return[p['x'],p['y'],p['z']]
def cs(v):return repr(float(v))+'f'
def vec(p):return 'new Float3('+','.join(cs(x)for x in values(p))+')'
rows=payload['rows'];assert len(rows)==105;extra=[];hazards=panels=floors=0
for r in rows:
 mpath=L/r['id'][8:]/'converted/manifest.json';m=json.loads(mpath.read_text());a=launch[r['id']]
 assert sha(mpath)==r['manifestSha256']==a['manifestSha256']
 for key in ['sourceSha256','cacheFingerprint','adapterSha256']:assert r[key]==a[key]
 for key,raw in [('boundsMin','bounds_min'),('boundsMax','bounds_max'),('solidMin','solid_min'),('solidMax','solid_max'),('center','center')]:assert values(r[key])==m[raw]
 assert [values(p)for p in r['recorded']]==m['recorded_spawns']
 assert [values(p)for p in r['deathmatch']]==[p['center']for p in sem[r['id'][8:]].get('deathmatch',[])]
 originalWarmup=[p['center']for p in sem[r['id'][8:]].get('warmup',[])];assert [values(p)for p in r['warmup'][:len(originalWarmup)]]==originalWarmup
 hazards+=len(r['hazards']);panels+=len(r['panels']);floors+=len(r['spawnFloors'])
 for h in r['hazards']:
  assert len(h['planes'])>=4
  for p in h['planes']:assert abs(math.sqrt(p['x']**2+p['y']**2+p['z']**2)-1)<1e-6
 extra.append('Bounds("'+r['id']+'",'+','.join(vec(r[k])for k in ['boundsMin','boundsMax','solidMin','solidMax','center'])+');')
code='''using System;using System.Collections;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.World;
class ArchiveCases {
 static int count,magnum;static void N(bool b,string why){count++;if(!b)throw new Exception(why);}
 static R24ArchiveRecoveryInfo Info(string id,Float3 low,Float3 high,Float3 solidLow,Float3 solidHigh,Float3 center,Float3[] recorded=null,Float3[] native=null,Float3[] warmup=null){return new R24ArchiveRecoveryInfo(id,"s","m","c","a",low,high,solidLow,solidHigh,center,recorded??new Float3[0],native??new Float3[0],warmup??new Float3[0],new R24ArchiveBox[0],new R24ArchiveBox[0],new R24ArchiveHazard[0]);}
 static void Bounds(string id,Float3 lo,Float3 hi,Float3 sl,Float3 sh,Float3 center){var data=Info(id,lo,hi,sl,sh,center);float x=(lo.X+hi.X)*.5f,z=(lo.Z+hi.Z)*.5f;N(data.Outside(new Float3(x,sl.Y-25.1f,z),float.NegativeInfinity,false),id+" strict low");N(!data.Outside(new Float3(x,sl.Y-25,z),float.NegativeInfinity,false),id+" low boundary");N(data.Outside(new Float3(x,sh.Y+100.1f,z),float.NegativeInfinity,false),id+" high");N(!data.Outside(new Float3(x,sh.Y+100,z),float.NegativeInfinity,false),id+" high boundary");N(data.Outside(new Float3(hi.X+10.1f,sl.Y,z),float.NegativeInfinity,false)==(id!="archive:alfa-aglae-souza"),id+" exact Alpha XZ exception");N(!data.Outside(new Float3(x,sh.Y+110,z),sh.Y+120,false),id+" actual launch ceiling");N(!data.Outside(new Float3(x,sl.Y-100,z),float.NegativeInfinity,true),id+" actual launch authorized/staging exemption");if(R24ArchiveMapPolicy.MagnumOnly(id))magnum++;}
 sealed class World:IR24ArchiveRecoveryWorld{public bool NoRoom;public bool Floor(Float3 from,float depth,out Float3 p){float level=from.Y>=40?40:0;p=new Float3(from.X,level,from.Z);return from.Y>=level&&from.Y-depth<=level;}public bool Outside(Float3 p){return false;}public bool Room(Float3 p,bool initial){return !NoRoom;}}
 static void Run(R24ArchiveSpawnPlan p){var w=p.Build();int guard=0;while(w.MoveNext())N(++guard<1000,"finite coroutine plan");}
 static void Main(){
 SOURCE_BOUNDS
 N(magnum==2,"exact two real source Magnum maps");N(!R24ArchiveMapPolicy.MagnumOnly(null)&&!R24ArchiveMapPolicy.MagnumOnly("archive:MAGNUM-chaosvr")&&!R24ArchiveMapPolicy.MagnumOnly("magnum-chaosvr"),"no invented Magnum aliases");
 var lo=new Float3(-10,-10,-10);var hi=new Float3(10,100,10);var sl=new Float3(-10,0,-10);var sh=new Float3(10,0,10);var c=new Float3();var recorded=new[]{new Float3(-2,.2f,0),new Float3(2,.2f,0),new Float3(0,40.2f,0)};
 var w=new World();var info=Info("archive:magnum-chaosvr",lo,hi,sl,sh,c,recorded);var plan=new R24ArchiveSpawnPlan(info,w);Run(plan);N(plan.Ready&&plan.Failure==null&&plan.Spawns.Length==2&&plan.Loot.Length==361,"source recorded validation/19x19 loot/high lobby separation positive branch");N(Math.Abs(plan.SourceWarmup.Y-40.12f)<.0001&&Math.Abs(plan.MagnumFallLevel+19.88f)<.0001,"source lobby and mean-spawn minus20 fall level");N(Math.Abs(plan.MagnumCenter.Y-2.5f)<.0001,"source mean center floor+2.5");
 info=Info("archive:magnum-3-chaosvr",lo,hi,sl,sh,c,recorded,new[]{new Float3(3,2,0)},new[]{new Float3(-3,42,0)});plan=new R24ArchiveSpawnPlan(info,w);Run(plan);N(plan.Ready&&plan.Spawns.Length==1&&plan.SourceSpawn.X==3,"actual source native deathmatch override stage");N(plan.SourceWarmup.X==-3&&Math.Abs(plan.SourceWarmup.Y-40.12f)<.0001,"source native warmup floor stage");
 info=Info("archive:classic-bigbox-vr",lo,hi,sl,sh,c);plan=new R24ArchiveSpawnPlan(info,w);Run(plan);N(plan.Ready&&plan.Spawns.Length==16,"source empty recorded grid fallback reaches original16 cap");var points=plan.Spawns;for(int a=0;a<points.Length;a++)for(int b=a+1;b<points.Length;b++){float x=points[a].X-points[b].X,z=points[a].Z-points[b].Z;N(x*x+z*z>=4,"source fallback minimum2m uniqueness");}
 w.NoRoom=true;plan=new R24ArchiveSpawnPlan(info,w);Run(plan);N(!plan.Ready&&plan.Failure!=null,"no fake platform/default spawn when all native room checks reject");
 var box=new R24ArchiveBox(new Float3(-1,0,-1),new Float3(2,2,2));N(box.Contains(new Float3(),0)&&!box.Contains(new Float3(1,0,0),0),"source AABB max boundary excluded");N(box.Intersects(new Float3(.9f,.1f,.9f),new Float3(.3f,1,.3f))&&!box.Intersects(new Float3(1,0,0),new Float3(1,1,1)),"source panel AABB strict overlap");
 Console.WriteLine(count+" actual105-source outside/Alpha/launch/Magnum policy and source spawn-planner branch cases PASS (planner floor oracle is a unit control, native map qualification separate)");}
}'''.replace('SOURCE_BOUNDS','\n'.join(extra))
(HERE/'ArchiveCases.cs').write_text(code)
exe=HERE/'ArchiveCases.exe';files=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs',STAGE/'Match/R24ArchiveRecoveryState.cs',STAGE/'World/R24ArchiveMapPolicy.cs',HERE/'ArchiveCases.cs']
p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/out:'+str(exe)]+[str(p)for p in files],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(exe)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
for f in ['R23PlayableLoop.ArchiveGhostRecovery.cs','R23PlayableLoop.GhostRecovery.cs']:
 text=(STAGE/f).read_text()
 for banned in ['ResetRig(','.Spawn(','.ResetRound(','.ConfigureLocalMatchHealth(']:assert banned not in text,banned
assert 'PAYLOAD_SHA256'not in(STAGE/'Match/R24ArchiveRecoveryCatalog.cs').read_text()
result=dict(sourceMaps=len(rows),sourcePanelHeadroomBounds=panels,sourceConvexHazards=hazards,sourceSpawnFloors=floors,payloadBytes=payloadPath.stat().st_size,output=p.stdout.strip(),nativeExecuted=False)
(HERE/'checks.json').write_text(json.dumps(result,indent=2)+'\n');print('PASS all105 exact manifest/index/native-hint bindings and authentic hazard/panel metadata; no source/geometry/health mutations.')
