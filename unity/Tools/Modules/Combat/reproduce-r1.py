from pathlib import Path
import subprocess,json,hashlib,shutil
root=Path(r'C:/Users/aling/Documents/ashfall')
stage=root/'campaign-staging/20261104-three-chats/unity/Combat'
e=root/'docs/agent-runs/20261104-three-chats/unity/Combat/evidence/r2'
snap=stage/'revisions/r1-before-r2'
src=root/'docs/agent-runs/20261104-three-chats/unity/Combat/candidate-manifest.json'
if src.exists():
    dst=snap/'r1-original-candidate-manifest.json'
    if not dst.exists():shutil.copyfile(src,dst);dst.chmod(0o444)
before=stage/'r2/before'
before.mkdir(exist_ok=True)
csc=r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
prod=[root/'unity/Assets/Ashfall/Core/Contracts.cs']+list((snap/'unity/Assets/Ashfall/Modules/Combat').glob('*.cs'))
records=[]
for name,test,expected in [('R1Builder',snap/'unity/Tools/Modules/Combat/CombatTests.cs',0),('R1Adversarial',root/'campaign-staging/20261104-three-chats/unity/reviews/Combat-r1/Adversarial.cs',1)]:
    args=[csc,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(before/(name+'.exe'))]+list(map(str,prod+[test]))
    result=subprocess.run(args,capture_output=True,text=True)
    records.append(dict(args=args,exit=result.returncode,stdout=result.stdout,stderr=result.stderr))
    assert result.returncode==0,records[-1]
    result=subprocess.run([str(before/(name+'.exe'))],capture_output=True,text=True)
    (e/(name+'-results.txt')).write_text(result.stdout+result.stderr)
    records.append(dict(args=[str(before/(name+'.exe'))],exit=result.returncode,expectedExit=expected))
    assert result.returncode==expected,records[-1]
(e/'before-commands.json').write_text(json.dumps(records,indent=2))
print(records[-1])
