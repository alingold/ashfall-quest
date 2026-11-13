from pathlib import Path
import hashlib,json,subprocess,sys,shutil,struct
R=Path.cwd(); P=R/'campaign-staging/20261104-three-chats/unity/AudioRuntimeR2'; out=P/sys.argv[1];out.mkdir(exist_ok=False)
old=R/'campaign-staging/20261104-three-chats/unity/reviews/AudioRuntime-r1'
# Small actual dependency snapshots; no borrowed old binaries.
deps=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for folder in ['unity/Assets/Ashfall/Modules/Audio','unity/Assets/Ashfall/Modules/Persistence','unity/Assets/Ashfall/Integration/AudioSettings']:
 deps += [p for p in (R/folder).rglob('*.cs') if 'Engine' not in p.parts]
owned=list((R/'unity/Assets/Ashfall/Integration/AudioRuntime').rglob('*.cs'))
records=[]
def pin(p):
 d=out/'snapshot'/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);d.write_bytes(p.read_bytes());records.append(dict(path=str(p),snapshot=str(d),sha256=hashlib.sha256(d.read_bytes()).hexdigest()));return d
sources=[pin(p) for p in deps+owned]
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';results=[]
def gate(name,test,main=None,native=False):
 folder=out/name;folder.mkdir();exe=folder/('gate.dll' if native else 'gate.exe')
 args=['/nologo','/warnaserror+','/langversion:5','/target:'+('library' if native else 'exe'),'/out:"'+str(exe)+'"']
 if native:
  args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
  unity=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
  for ref in [unity/'NetStandard/ref/2.1.0/netstandard.dll',unity/'Managed/UnityEngine/UnityEngine.CoreModule.dll',unity/'Managed/UnityEngine/UnityEngine.AudioModule.dll']:args+=['/reference:"'+str(ref)+'"'];records.append(dict(path=str(ref),sha256=hashlib.sha256(ref.read_bytes()).hexdigest()))
 args+=['"'+str(p)+'"' for p in sources]
 if test:args+=['"'+str(pin(test))+'"']
 if name=='r2':args+=['"'+str(pin(R/'unity/Tools/Integration/AudioRuntime/ClimbFromOracleNoise.cs'))+'"']
 rsp=folder/'compile.rsp';rsp.write_text('\n'.join(args));cmd=[csc]+(['/noconfig'] if native else [])+['@'+str(rsp)]
 c=subprocess.run(cmd,capture_output=True,text=True);(folder/'compile.log').write_text(c.stdout+c.stderr);res=dict(name=name,command=cmd,compile_exit=c.returncode)
 if not native and c.returncode==0:
  command=[str(exe),str(folder/'fixtures')];
  if name in ['settings','accepted']:
   f=folder/'fixtures';f.mkdir(exist_ok=True);wave=f/'TEST-ONLY.wav';pcm=struct.pack('<32h',*([8192]*32));data=b'RIFF'+struct.pack('<I',36+len(pcm))+b'WAVEfmt '+struct.pack('<IHHIIHH',16,1,1,32000,64000,2,16)+b'data'+struct.pack('<I',len(pcm))+pcm;wave.write_bytes(data)
   manifest=f/'fixture.tsv';manifest.write_text('ASHFALL-AUDIO/1\ngeneric/wind\t0\t'+str(wave)+'\t'+hashlib.sha256(data).hexdigest()+'\ttest\taccepted:TEST-ONLY\toriginal-test\tSFX\t1\n')
   command=([str(exe),str(folder)] if name=='accepted' else command+[str(manifest)])
  t=subprocess.run(command,capture_output=True,text=True);(folder/'tests.log').write_text(t.stdout+t.stderr);res.update(test_command=command,test_exit=t.returncode,output=t.stdout+t.stderr)
 results.append(res);print(res)
for name,test in [('runtime',R/'unity/Tools/Integration/AudioRuntime/RuntimeTests.cs'),('r2',R/'unity/Tools/Integration/AudioRuntime/R2Tests.cs'),('independent',old/'Independent.cs'),('settings',old/'SettingsMutation.cs'),('accepted',R/'unity/Tools/Integration/AudioSettings/AudioR4Tests.cs')]:gate(name,test)
gate('native-api',None,native=True)
(out/'inputs.json').write_text(json.dumps(records,indent=2));(out/'results.json').write_text(json.dumps(results,indent=2))
sys.exit(any(x['compile_exit'] or x.get('test_exit',0) for x in results))
