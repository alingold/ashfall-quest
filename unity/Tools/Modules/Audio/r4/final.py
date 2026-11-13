import pathlib,json,hashlib,subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/Audio/r4';E=R/'docs/agent-runs/20261104-three-chats/unity/Audio/r4'
commands=[]
def run(name,args):
 args=list(map(str,args));commands.append(' '.join('"'+a+'"' for a in args));p=subprocess.run(args,cwd=R,capture_output=True,text=True);(E/(name+'.txt')).write_text(p.stdout+p.stderr);print(name,p.returncode,(p.stdout+p.stderr)[-1300:]);assert p.returncode==0
D=S/'final-state-machine';D.mkdir(exist_ok=False)
run('final-fixture',[S/'fixture.exe',D/'fixtures'])
sources=[S/'inputs/unity/Assets/Ashfall/Core/Contracts.cs']+sorted((R/'unity/Assets/Ashfall/Modules/Audio').rglob('*.cs'))+sorted((S/'inputs/unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))+sorted((R/'unity/Assets/Ashfall/Integration/AudioSettings').glob('*.cs'))+[R/'unity/Tools/Integration/AudioSettings/AudioR4Tests.cs']
exe=S/'FinalStateMachine.exe'
run('final-compile',[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+sources)
run('final-state-machine',[exe,D])
(E/'commands-final.txt').write_text('\n'.join(commands))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
rows=json.loads((E/'inputs.json').read_text());assert all(sha(pathlib.Path(x['path']))==x['sha256']==sha(pathlib.Path(x['snapshot'])) for x in rows)
inv=json.loads((R/'campaign-staging/20261104-three-chats/unity/Audio/r3/source-audio-inventory.json').read_text(encoding='utf-8-sig'))
wavs={a['source']:a['sha256'] for t in inv['trees'] for a in t['assets'] if 'sha256' in a};drift=[p for p,h in wavs.items() if sha(pathlib.Path(p))!=h.upper()];assert not drift
(E/'final-identity.json').write_text(json.dumps(dict(consumed_dependencies=len(rows),external_dependency_drift=[],original_wavs=len(wavs),wav_drift=drift),indent=2))
