"""Fresh C#5 neutral evidence only. No engines or historical output wrappers."""
import hashlib,json,pathlib,subprocess,sys,shutil
ROOT=pathlib.Path(__file__).resolve().parents[5]
S=ROOT/'campaign-staging/20261104-three-chats/unity/Audio/r3'
E=ROOT/'docs/agent-runs/20261104-three-chats/unity/Audio/r3'
P=ROOT/'unity/Assets/Ashfall/Modules/Audio'
T=ROOT/'unity/Tools/Modules/Audio'
CSC=pathlib.Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
commands=[]
def run(name,args):
    args=list(map(str,args));commands.append(' '.join('"'+a+'"' for a in args))
    r=subprocess.run(args,cwd=ROOT,capture_output=True,text=True)
    (E/(name+'.txt')).write_text(r.stdout+r.stderr,encoding='utf-8')
    (E/'commands.txt').write_text('\n'.join(commands)+'\n')
    print(name,r.returncode,r.stdout[-500:],r.stderr[-500:],flush=True)
    if r.returncode:raise RuntimeError(name)
def compile(name,test,integration=False):
    sources=[S/'inputs/core/Contracts.cs']+sorted(P.glob('*.cs'))
    if integration:sources+=sorted((S/'inputs/persistence').glob('*.cs'))+sorted((ROOT/'unity/Assets/Ashfall/Integration/AudioSettings').glob('*.cs'))
    exe=S/(name+'.exe');run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+sources+[test]);return exe
def main():
    if (E/'candidate-manifest.json').exists():raise RuntimeError('Frozen candidate: use a new owned evidence directory')
    accepted=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
    inputs=[]
    for row in accepted['sourceChecks']:
        if row['name']=='Persistence':
            p=pathlib.Path(row['path']);assert sha(p)==row['expected']==sha(S/'inputs/persistence'/p.name)
            inputs.append(dict(path=str(p),sha256=sha(p),snapshot=str(S/'inputs/persistence'/p.name)))
    for rel in ['unity/Assets/Ashfall/Core/Contracts.cs','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json','docs/agent-runs/20261104-three-chats/unity/reviews/Audio-r2/REPORT.md']:
        p=ROOT/rel;inputs.append(dict(path=str(p),sha256=sha(p)))
    assert sha(ROOT/'unity/Assets/Ashfall/Core/Contracts.cs')==sha(S/'inputs/core/Contracts.cs')
    for tree in ['quest-r22/source','game']:
        for name in ['weapon.gd','menu.gd','audio_bank.gd','net_bridge1000.gd','ambience.gd','reference_audio_099.gd']:
            p=ROOT/tree/'scripts'/name
            if p.exists():
                dest=S/'inputs/source'/tree/'scripts'/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
                inputs.append(dict(path=str(p),sha256=sha(p),snapshot=str(dest)))
    (E/'inputs.json').write_text(json.dumps(dict(revision='bdba6e30012e1733efd5021fd8ded593f82f26e6',game_revision='unversioned differing skin runtime',inputs=inputs),indent=2))
    baseline=compile('AudioTests',T/'AudioTests.cs');run('baseline',[baseline,S/'fixtures'])
    r2=compile('R2Tests',T/'R2Tests.cs');run('r2-regressions',[r2,S])
    # Retain every independent corrected r1 regression from the frozen r2 packet.
    r1=compile('RetainedR1',S.parent/'r2/ReviewerRegressions.cs');run('retained-r1',[r1,S])
    run('source-inventory',[sys.executable,'-B',T/'export_audio.py','--quest',ROOT/'quest-r22/source','--game',ROOT/'game','--output',S/'source-audio-inventory.json'])
    inventory=json.loads((S/'source-audio-inventory.json').read_text(encoding='utf-8-sig'))
    paths={a['source']:a['sha256'] for t in inventory['trees'] for a in t['assets'] if 'sha256' in a}
    assert len(paths)==365
    wavs=S/'source-wavs.txt';wavs.write_text('\n'.join(sorted(paths))+'\n')
    run('real-decode',[baseline,'--wave-list',wavs])
    r3=compile('AudioR3Tests',ROOT/'unity/Tools/Integration/AudioSettings/AudioR3Tests.cs',True);run('r3-behavior',[r3,S,wavs])
    assert all(sha(pathlib.Path(p))==h for p,h in paths.items())
    assert all(sha(pathlib.Path(i['path']))==i['sha256'] for i in inputs)
    (E/'source-validation.json').write_text(json.dumps(dict(unique_wavs=len(paths),hash_drift=0,missing=[dict(root=t['root'],rows=[a for a in t['assets'] if 'error' in a]) for t in inventory['trees']]),indent=2))
    shutil.copytree(P,S/'candidate/audio',dirs_exist_ok=True)
    shutil.copytree(ROOT/'unity/Assets/Ashfall/Integration/AudioSettings',S/'candidate/integration',dirs_exist_ok=True)
    print('Evidence ready; independent review required')
if __name__=='__main__':main()

