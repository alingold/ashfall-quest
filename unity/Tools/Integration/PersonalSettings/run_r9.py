from pathlib import Path
import json,subprocess,hashlib,datetime
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/PresentationLiveBindingsR9';E=R/'docs/agent-runs/20261104-three-chats/unity/PresentationLiveBindingsR9'
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
deps=json.loads((E/'dependencies.json').read_text());S=S/('run-'+datetime.datetime.now().strftime('%H%M%S%f'));S.mkdir();mods=['Player','HealthStates','Modes','Sessions','Persistence','Audio']
sources=[Path(x['snapshot']) for x in deps if '/Core/' in x['original'] or any('/Modules/'+m+'/' in x['original'] for m in mods)]
owned=[p for d in ['Modules/Presentation','Integration/PersonalSettings'] for p in (R/'unity/Assets/Ashfall'/d).rglob('*.cs')]
owned += [R/'unity/Assets/Ashfall/Modules/Player'/n for n in ['Locomotion.cs','PlayerPorts.cs','PlayerSettingsPublication.cs']]
replaced={str(p.relative_to(R)).replace('\\','/') for p in owned}
sources=[Path(x['snapshot']) for x in deps if x['original'].replace('\\','/') not in replaced and ('/Core/' in x['original'] or any('/Modules/'+m+'/' in x['original'] for m in mods))]+owned
inputs=[]
def pin(p):
 q=S/'candidate'/(str(len(inputs))+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());inputs.append(dict(original=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));return q
sources=[pin(p) for p in sources]
tests=[pin(R/'unity/Tools/Integration/PersonalSettings'/n) for n in ['PresentationR5Tests.cs','PresentationR6Tests.cs','PresentationR9Tests.cs']]
tests+=[pin(R/'campaign-staging/20261104-three-chats/unity/reviews/PresentationResetReviewR8/IndependentR8Final.cs')]
rows=[]
def run(name,main,src,extra=[]):
 out=S/(name+'.exe');rsp=S/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/reference:"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll"','/reference:"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll"','/target:exe','/main:'+main,'/out:"'+str(out)+'"']+extra+['"'+str(p)+'"' for p in src]))
 cmd=[CSC,'/noconfig','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);row=dict(name=name,command=cmd,compileExit=p.returncode,diagnostics=p.stdout+p.stderr)
 if p.returncode==0:
  cmd=[str(out),str(S/('data-'+name))];q=subprocess.run(cmd,capture_output=True,text=True);row.update(runCommand=cmd,runExit=q.returncode,output=q.stdout+q.stderr)
 rows.append(row);print(name,row['compileExit'],row.get('runExit'),row.get('output',row['diagnostics'])[:3000])
run('R9','PresentationR9Tests',sources+tests)
run('R8Retained','PresentationR7Tests',sources+tests)
run('R5Retained','R5Tests',sources+tests)
run('R6UpdatedPortExpectation','R6Tests',sources+tests)
motor=next(p for p in sources if p.name.endswith('-Locomotion.cs'))
for name,old,new in [('DurationMutant','float capacity=StaminaMaximum,recovery=RecoverySeconds;','float capacity=6,recovery=RecoverySeconds;'),('RecoveryMutant','dt * capacity/recovery','dt * capacity/4')]:
 text=motor.read_text();assert old in text
 mutant=S/(name+'.cs');mutant.write_text(text.replace(old,new))
 run(name,'PresentationR9Tests',[mutant if p==motor else p for p in sources]+tests)
# Compiler-only guarded native closure; PlayerDriver/native bridge deliberately
# excluded: full Input/XR lifecycle composition belongs to root.
native=[Path(x['snapshot']) for x in inputs if '\\Engine\\' not in x['original'] and '/Engine/' not in x['original'] and not x['original'].endswith('IndependentR8Final.cs') and '\\Tools\\' not in x['original']]
native += [Path(x['snapshot']) for x in inputs if x['original'].endswith('PresentationAdapter.cs') or x['original'].endswith('NativeTargetFeedbackView.cs')]
native += [pin(R/'unity/Tools/Modules/Presentation'/n) for n in ['PresentationNativeR6Gates.cs','PresentationNativeR7Gates.cs','PresentationNativeR9Gates.cs']]
unity=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
refs=[unity/'NetStandard/ref/2.1.0/netstandard.dll']+list((unity/'Managed/UnityEngine').glob('UnityEngine*.dll'))
rsp=S/'Native.rsp';out=S/'Native.dll'
rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR','/out:"'+str(out)+'"']+['/r:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in native]))
cmd=[CSC,'/noconfig','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);rows.append(dict(name='NativeAPIOnly',command=cmd,compileExit=p.returncode,diagnostics=p.stdout+p.stderr));print('NativeAPIOnly',p.returncode,p.stdout+p.stderr)
(E/'native-references.json').write_text(json.dumps([dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in refs],indent=2))
(E/'results.json').write_text(json.dumps(rows,indent=2));(E/'candidate-inputs.json').write_text(json.dumps(inputs,indent=2))
