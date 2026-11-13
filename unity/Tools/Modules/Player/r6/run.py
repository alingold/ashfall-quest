from pathlib import Path
import json,hashlib,subprocess
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/PlayerR6';D=R/'docs/agent-runs/20261104-three-chats/unity/PlayerR6'
old=R/'campaign-staging/20261104-three-chats/unity/reviews/PlayerIntegration-r5'
mapping=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/reviews/PlayerIntegration-r5/compile-inputs.json').read_text(encoding='utf-8-sig'))
lookup={e['snapshot']:e['original'] for e in mapping}
current=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/reviews/PlayerIntegration-r5/current-compat-inputs.json').read_text(encoding='utf-8-sig'))
currentLookup={e['snapshot']:e['source'] for e in current}
records=[];commands=[];inputs=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def capture(p):
 dest=S/'inputs'/(hashlib.sha256((str(p)+sha(p)).encode()).hexdigest()[:12]+'-'+p.name);dest.parent.mkdir(exist_ok=True)
 if not dest.exists():dest.write_bytes(p.read_bytes())
 inputs.append(dict(path=str(p),snapshot=str(dest),sha256=sha(dest)))
 return dest
def run(name,paths):
 folder=S/name;folder.mkdir(exist_ok=True);exe=folder/(name+'.exe');rsp=folder/(name+'.rsp')
 rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(capture(p))+'"' for p in paths]))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];commands.append(cmd);c=subprocess.run(cmd,capture_output=True,text=True);(folder/'compile.log').write_text(c.stdout+c.stderr);rec=dict(name=name,compile=c.returncode)
 if c.returncode==0:
  commands.append([str(exe)]);t=subprocess.run([str(exe)],capture_output=True,text=True);(folder/'run.log').write_text(t.stdout+t.stderr);rec.update(exit=t.returncode,output=t.stdout+t.stderr)
 records.append(rec);print(name,rec.get('compile'),rec.get('exit'),rec.get('output','')[-100:])
def paths(rsp,currentMode=False):
 result=[]
 for line in rsp.read_text().splitlines():
  if not line.startswith('"'):continue
  p=Path(line.strip('"'));origin=lookup.get(str(p),'')
  if currentMode:p=Path(currentLookup[str(p)]);origin=str(p)
  if '\\Assets\\Ashfall\\Modules\\Player\\' in origin:p=R/'unity/Assets/Ashfall/Modules/Player'/Path(origin.split('\\Modules\\Player\\')[-1])
  result.append(p)
 result.append(R/'unity/Assets/Ashfall/Modules/Player/PlayerSettingsPublication.cs')
 return result
for p in sorted((old/'replay/candidate').rglob('*.rsp')):run(p.stem,paths(p))
for p in sorted((old/'replay/composition').rglob('*.rsp')):run(p.stem,paths(p))
run('FreshReview',paths(old/'FreshReview.rsp'))
run('R6',list((R/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Tools/Modules/Player/r6/R6Tests.cs',R/'unity/Tools/Modules/Player/r6/PlayerR6NativeGates.cs',R/'unity/Tools/Modules/Player/r6/PlayerR6PhysicalFixture.cs'])
for name in ['Composition','SettingsComposition']:
 selected=paths(old/(name+'Current.rsp'),True)
 if name=='Composition':
  selected += [p for p in (R/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs') if p not in selected]
 run(name+'Current',selected)
(D/'results.json').write_text(json.dumps(records,indent=2));(D/'commands.json').write_text(json.dumps(commands,indent=2));(D/'compile-inputs.json').write_text(json.dumps(inputs,indent=2))
receipts=[]
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json')):
 v=json.loads(p.read_text(encoding='utf-8-sig'));receipts.append(dict(path=str(p),sha256=sha(p),keys=list(v),bytes=p.stat().st_size))
(D/'accepted-receipts.json').write_text(json.dumps(receipts,indent=2))
raise SystemExit(0 if all(e['compile']==0 and e.get('exit')==0 for e in records) else 1)
