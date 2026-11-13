"""Direct compiler commands only; never invokes historical wrappers or engines."""
import hashlib, json, pathlib, subprocess, sys, shutil
ROOT=pathlib.Path(__file__).resolve().parents[4]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/Audio/r2'
EVIDENCE=ROOT/'docs/agent-runs/20261104-three-chats/unity/Audio/evidence/r2'
SNAP=STAGE.parent/'revisions/r1-before-r2'
CSC=pathlib.Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
PROD=ROOT/'unity/Assets/Ashfall/Modules/Audio'
TOOLS=ROOT/'unity/Tools/Modules/Audio'
REVIEW=ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Audio-r1'
commands=[]
def run(name,cmd):
    commands.append(subprocess.list2cmdline([str(x) for x in cmd]))
    result=subprocess.run([str(x) for x in cmd],cwd=ROOT,capture_output=True,text=True)
    (EVIDENCE/(name+'.txt')).write_text(result.stdout+result.stderr,encoding='utf-8')
    print(name,result.returncode,result.stdout[-600:],result.stderr[-600:])
    if result.returncode:raise RuntimeError(name)
def compile(name,production,test):
    sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted(production.glob('*.cs'))+[test]
    target=STAGE/(name+'.exe')
    run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(target)]+sources)
    return target
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def main():
    if (EVIDENCE/'candidate-manifest.json').exists():
        raise RuntimeError('r2 is frozen; use fresh independent review outputs, never overwrite candidate binaries')
    STAGE.mkdir(parents=True,exist_ok=True);EVIDENCE.mkdir(parents=True,exist_ok=True)
    before=STAGE/'before';before.mkdir(exist_ok=True)
    old=compile('BeforeAudioTests',SNAP/'production',SNAP/'tools/AudioTests.cs')
    run('before-baseline',[old,before/'fixtures'])
    reviewer=compile('BeforeReviewTests',SNAP/'production',REVIEW/'ReviewTests.cs')
    run('before-review-findings',[reviewer,before])
    current=compile('AudioTests',PROD,TOOLS/'AudioTests.cs')
    run('baseline',[current,STAGE/'fixtures'])
    # Preserve every independent assertion; invert only the three defect expectations.
    s=(REVIEW/'ReviewTests.cs').read_text(encoding='utf-8-sig')
    s=s.replace('Require(accepted,"FourCC repro changed")','Require(!accepted,"Malformed five-byte FourCC accepted")')
    start=s.index('AudioAsset a=Cat(')
    end=s.index('fields[2]=Path.GetFullPath',start)
    s=s[:start]+'''bool rejected=false;try{Cat(string.Join("\\t",fields)+"\\n");}catch(InvalidDataException){rejected=true;}Require(rejected,"Root-relative catalog path accepted");Console.WriteLine("PASS root-relative path rejected");
 '''+s[end:]
    s=s.replace('Require(lost,"action alias repro changed")','Require(!lost,"Action subject incorrectly aliased")').replace('FINDING literal action subject uzi incorrectly aliased and lost=True (synthetic future route)','PASS literal action subject uzi preserved')
    fixed=STAGE/'ReviewerRegressions.cs';fixed.write_text(s,encoding='utf-8')
    recheck=compile('ReviewerRegressions',PROD,fixed);run('reviewer-regressions',[recheck,STAGE])
    extra=compile('R2Tests',PROD,TOOLS/'R2Tests.cs');run('r2-regressions',[extra,STAGE])
    old_inventory=json.loads((STAGE.parent/'source-audio-inventory.json').read_text())
    paths={r['source']:r['sha256'] for t in old_inventory['trees'] for r in t['assets'] if 'sha256' in r}
    assert len(paths)==365
    assert all(digest(pathlib.Path(p))==h for p,h in paths.items())
    wave_list=STAGE/'source-wavs.txt';wave_list.write_text('\n'.join(sorted(paths))+'\n')
    run('source-wave-check',[current,'--wave-list',wave_list])
    run('source-inventory',[sys.executable,'-B',TOOLS/'export_audio.py','--quest',ROOT/'quest-r22/source','--game',ROOT/'game','--output',STAGE/'source-audio-inventory.json'])
    # Original importer assertions, with temporary output constrained to r2.
    s=(TOOLS/'test_import_tools.py').read_text().replace("parent=pathlib.Path(__file__).resolve().parents[4]/'campaign-staging/20261104-three-chats/unity/Audio'",'parent=pathlib.Path('+repr(str(STAGE))+')')
    imp=STAGE/'import_regressions.py';imp.write_text('import sys\nsys.path.insert(0,'+repr(str(TOOLS))+')\n'+s)
    run('import-tests',[sys.executable,'-B',imp])
    consumed=[]
    for path in [ROOT/'unity/Assets/Ashfall/Core/Contracts.cs',ROOT/'unity/Documentation/CONTRACTS-v1.md',ROOT/'unity/Documentation/CONTRACTS-v2.md',REVIEW/'ReviewTests.cs']+[pathlib.Path(t['root'])/p for t in old_inventory['trees'] for p in t['files']]+[ROOT/'quest-r22/source/scripts/net_bridge1000.gd',ROOT/'game/scripts/net_bridge1000.gd']:
        consumed.append(dict(path=str(path),sha256=digest(path)))
    (EVIDENCE/'consumed-sources.json').write_text(json.dumps(consumed,indent=2))
    missing=[dict(root=t['root'],missing=[r for r in t['assets'] if 'error' in r]) for t in json.loads((STAGE/'source-audio-inventory.json').read_text())['trees']]
    (EVIDENCE/'source-validation.json').write_text(json.dumps(dict(unique_wavs=365,hash_mismatches=0,missing=missing),indent=2))
    frozen=STAGE/'production';shutil.copytree(PROD,frozen,dirs_exist_ok=True)
    (EVIDENCE/'commands.txt').write_text('\n'.join(commands)+'\n')
if __name__=='__main__':
    main()
