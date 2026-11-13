"""Immutable, source-pinned neutral C#5 candidate runner. No engine launch."""
import hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
stage=ROOT/'campaign-staging/20261104-three-chats/unity/Construction/r3'
label=sys.argv[1]
if not label.isalnum(): raise SystemExit('new alphanumeric label required')
out=stage/label
out.mkdir(exist_ok=False)
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
sample_path=stage/'inputs/paintball-small-surface.json'
if not sample_path.exists():
    source=ROOT/'campaign-staging/20261104-three-chats/unity/World/battlefield-paintball-gods-pitdevil.json'
    raw=source.read_bytes();data=json.loads(raw);surface=data['surfaces'][0]
    sample={'parentPath':str(source),'parentSha256':hashlib.sha256(raw).hexdigest(),'mapId':data['id'],'sourcePath':data['sourcePath'],'sourceSha256':data['sourceSha256'],'sourceRevision':data['sourceRevision'],'approval':data['approval'],'surface':{k:surface[k] for k in ['id','semanticOwner','colliderRole','matrix','vertices','indices']}}
    sample_path.write_text(json.dumps(sample,indent=2))
sample=json.loads(sample_path.read_text());surface=sample['surface']
if surface['matrix']!=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]:raise SystemExit('unsupported sample transform')
v=surface['vertices'];indices=surface['indices'];lines=[]
def point(i):return 'new Float3('+','.join(format(float(x),'.9g')+'f' for x in [v[3*i],v[3*i+1],-v[3*i+2]])+')'
for i in range(0,len(indices),3):
    lines.append('new SourceTriangle("'+surface['id']+'/tri/'+str(i//3)+'","'+surface['semanticOwner']+'","'+sample['sourceSha256']+'",SurfaceRole.Structure,8,'+','.join(point(indices[j]) for j in [i,i+2,i+1])+')')
sample_cs=out/'SampleData.cs'
sample_cs.write_text('using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.ConstructionGeometry;static class SampleData { public static List<SourceTriangle> Triangles(){return new List<SourceTriangle>{'+',\n'.join(lines)+'};}}')
core=stage/'inputs/source/Contracts.cs'
if digest(core)!=digest(ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'):raise SystemExit('Core drift from consumed snapshot')
module=ROOT/'unity/Assets/Ashfall/Modules/Construction'
integration=ROOT/'unity/Assets/Ashfall/Integration/ConstructionGeometry'
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
receipts=[]
tests=[('Builder',ROOT/'unity/Tools/Modules/Construction/ConstructionTests.cs'),('R1Regressions',ROOT/'unity/Tools/Modules/Construction/R1Regressions.cs'),('R2',ROOT/'unity/Tools/Modules/Construction/R2Tests.cs'),('Geometry',ROOT/'unity/Tools/Integration/ConstructionGeometry/GeometryTests.cs')]
for name,test in tests:
    sources=[core]+sorted(module.glob('*.cs'))+[test]
    if name=='Geometry':sources+=sorted(integration.glob('*.cs'))+[sample_cs]
    exe=out/(name+'.exe');rsp=out/(name+'.rsp')
    rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources))
    command=[str(csc),'@'+str(rsp)]
    compiled=subprocess.run(command,capture_output=True,text=True)
    (out/(name+'-compile.txt')).write_text(compiled.stdout+compiled.stderr)
    r={'name':name,'command':command,'compileExit':compiled.returncode,'inputs':[{'path':str(p),'sha256':digest(p)} for p in sources]}
    if not compiled.returncode:
        ran=subprocess.run([str(exe)],capture_output=True,text=True);(out/(name+'-results.txt')).write_text(ran.stdout+ran.stderr)
        r.update(testExit=ran.returncode,exeSha256=digest(exe),summary=ran.stdout.splitlines()[-1] if ran.stdout else ran.stderr)
    receipts.append(r);print(name,r.get('summary',compiled.stdout),flush=True)
(out/'receipts.json').write_text(json.dumps(receipts,indent=2))
if any(r['compileExit'] or r.get('testExit',1) for r in receipts):raise SystemExit(1)
