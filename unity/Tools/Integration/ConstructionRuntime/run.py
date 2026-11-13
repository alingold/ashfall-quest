import hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeR2'
label=sys.argv[1]
if not label.isalnum(): raise SystemExit('alphanumeric fresh label required')
out=STAGE/label;out.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
before=json.loads((ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeR1/consumed-before.json').read_text(encoding='utf-8-sig'))
base=[ROOT/r['path'] for r in before]
owned=list((ROOT/'unity/Assets/Ashfall/Integration/ConstructionRuntime').rglob('*.cs'))
test=ROOT/'unity/Tools/Integration/ConstructionRuntime/RuntimeFixtures.cs'
# Snapshot every consumed candidate source before compilation.
inputs=[]
for p in base+owned+list(test.parent.glob('*.cs')):
    q=out/'sources'/p.relative_to(ROOT);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());inputs.append(q)
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
exe=out/'Runtime.exe';rsp=out/'Runtime.rsp'
rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/main:'+(sys.argv[2] if len(sys.argv)>2 else 'RuntimeFixtures')+'\n/r:System.Runtime.Serialization.dll\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in inputs))
command=[str(csc),'@'+str(rsp)]
r=subprocess.run(command,capture_output=True,text=True);(out/'compile.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr)
result={'command':command,'compileExit':r.returncode,'inputs':[{'path':str(p),'sha256':sha(p)} for p in inputs]}
if r.returncode==0:
    run=subprocess.run([str(exe)],capture_output=True,text=True);(out/'tests.log').write_text(run.stdout+run.stderr);print(run.stdout+run.stderr);result.update(testExit=run.returncode,exeSha256=sha(exe))
drift=[{'path':r['path'],'before':r['sha256'],'after':sha(ROOT/r['path']),'same':r['sha256'].lower()==sha(ROOT/r['path'])} for r in before]
(out/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
(out/'results.json').write_text(json.dumps(result,indent=2))
raise SystemExit(result.get('testExit',1) or r.returncode)
