"""Capture playable source and payload identities; verify before building the same candidate.

Metadata is copied for inspection but Unity may extend it during first import.
No engine, lock, device or build action is performed here.
"""
import argparse,datetime,hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser();p.add_argument('directory',type=pathlib.Path);p.add_argument('--verify',action='store_true');p.add_argument('--base',type=pathlib.Path,help='Immutable prior capture: copy only changed text/metadata, retain full current identities');a=p.parse_args()
directory=a.directory.resolve();receipt=directory/'inputs.json'
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def files():
    selected=set()
    for relative in ['Assets/Ashfall/Core','Assets/Ashfall/RecoveryContracts','Assets/Ashfall/Modules','Assets/Ashfall/Integration/Playable','Assets/Ashfall/Integration/Sessions','Assets/Ashfall/Editor/Playable','Assets/StreamingAssets/PlayableR23']:
        selected.update(p for p in (root/relative).rglob('*') if p.is_file())
    # The playable round directly consumes this production owner outside Playable.
    owner=root/'Assets/Ashfall/Integration/DesktopPlaytest/Combat/LocalRifleSandbox.cs'
    assert owner.is_file(),'Production rifle owner missing'
    selected.add(owner)
    metadata=owner.with_suffix(owner.suffix+'.meta')
    if metadata.is_file():selected.add(metadata)
    return sorted(selected)
if a.verify:
    capture=json.loads(receipt.read_text());expected={r['path']:r for r in capture['content']};current={str(p.relative_to(root)).replace('\\','/'):p for p in files() if p.suffix!='.meta'}
    assert expected.keys()==current.keys(),'Candidate file membership changed since native-input capture'
    for name,path in current.items():assert path.stat().st_size==expected[name]['bytes'] and sha(path)==expected[name]['sha256'],'Candidate content changed: '+name
    print('Captured playable/owner source and payload content unchanged:',len(current),'files')
else:
    assert not directory.exists(),'Preserve prior capture';directory.mkdir(parents=True)
    base=None;old={}
    if a.base:
        base=a.base.resolve();prior=json.loads((base/'inputs.json').read_text());old={r['path']:r for r in prior['content']+prior['metadata']}
    content=[];metadata=[]
    for path in files():
        name=str(path.relative_to(root)).replace('\\','/');row=dict(path=name,bytes=path.stat().st_size,sha256=sha(path))
        (metadata if path.suffix=='.meta' else content).append(row)
        if path.suffix in ['.cs','.shader','.json','.meta'] and (name not in old or old[name]['sha256']!=row['sha256']):
            target=directory/'source'/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
    capture=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),content=content,metadata=metadata,engineRun=False,metadataMayBeExtendedByUnityImport=True)
    if base is not None:capture['sourceBase']=str(base);capture['sourceRule']='Resolve source/<path> here first, then recursively through immutable sourceBase; full content/metadata identities above always describe this capture.'
    receipt.write_text(json.dumps(capture,indent=2)+'\n');print('Captured',len(content),'content files and',len(metadata),'metadata identities at',receipt)
