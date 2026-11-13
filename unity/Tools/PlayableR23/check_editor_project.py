"""Compile actual last-imported Editor response files into an isolated output.

Keeps Unity's sources, references, defines, language version and analyzers. Only
compiler output paths and the Editor's reference to our runtime output change.
This does not import assets, start Unity, or modify Library compiler outputs.
"""
import argparse,hashlib,json,re,subprocess
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--response-root',required=True,type=Path)
parser.add_argument('--output-root',required=True,type=Path)
parser.add_argument('--new-runtime-source',action='append',default=[],help='Explicit new Assets source not yet imported; included only in private Runtime compile')
parser.add_argument('--new-editor-source',action='append',default=[],help='Explicit new Editor source not yet imported; included only in private Editor compile')
args=parser.parse_args()
workspace=Path(__file__).resolve().parents[3]
project=workspace/'unity'
responses=args.response_root.resolve()
output=args.output_root.resolve()
assert not output.is_relative_to(project),'Private output must be outside the Unity project'
assert not output.exists(),'Use a fresh output directory; preserve earlier compiler logs'
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
host=editor/'DotNetSdk/dotnet.exe'
compiler=editor/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
assert host.is_file() and compiler.is_file()
units=['Assembly-CSharp','Assembly-CSharp-Editor']
actual_sources={p.resolve() for p in (project/'Assets').rglob('*.cs')}
listed_sources=set();protected_outputs={}
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
for unit in units:
    for suffix in ['.rsp','.rsp2']:
        text=(responses/(unit+suffix)).read_text(encoding='utf-8-sig')
        for line in text.splitlines():
            if re.match(r'^"Assets/.*\.cs"$',line):listed_sources.add((project/line.strip('"')).resolve())
            if re.match(r'^[-/](out|refout|pdb|doc|errorlog):',line,re.I):
                value=Path(line.split(':',1)[1].strip('"'));path=(value if value.is_absolute() else project/value).resolve()
                protected_outputs[path]=digest(path)
                if line.lower().startswith('-out:'):protected_outputs[path.with_suffix('.pdb')]=digest(path.with_suffix('.pdb'))
new_runtime_sources={(project/Path(p)).resolve() for p in args.new_runtime_source}
new_editor_sources={(project/Path(p)).resolve() for p in args.new_editor_source}
new_sources=new_runtime_sources|new_editor_sources
assert all(p.is_relative_to((project/'Assets').resolve()) and p.is_file() and p.suffix=='.cs' and 'Editor' not in p.relative_to(project.resolve()).parts for p in new_runtime_sources),'New runtime sources must be actual non-Editor Assets C# files'
assert all(p.is_relative_to((project/'Assets').resolve()) and p.is_file() and p.suffix=='.cs' and 'Editor' in p.relative_to(project.resolve()).parts for p in new_editor_sources),'New Editor sources must be actual Assets/Editor C# files'
assert not (new_sources&listed_sources),'Explicit new source already present in imported response; omit corresponding --new-source argument'
assert actual_sources==listed_sources|new_sources, 'Stale response files: missing='+str(sorted(str(p) for p in actual_sources-listed_sources-new_sources))+' extra='+str(sorted(str(p) for p in listed_sources-actual_sources))
output.mkdir(parents=True)
rows=[]
for unit in units:
    inputs=[];source_count=0
    for suffix in ['.rsp','.rsp2']:
        original=responses/(unit+suffix)
        assert original.is_file(),str(original)
        raw=original.read_bytes();text=raw.decode('utf-8-sig')
        source_count+=len(re.findall(r'^"Assets/[^\n]+\.cs"\s*$',text,re.M))
        rewritten=[]
        for line in text.splitlines():
            match=re.match(r'^[-/](out|refout|pdb|doc|generatedfilesout|errorlog):',line,re.I)
            if match:
                key=match.group(1).lower()
                name={'out':unit+'.dll','refout':unit+'.ref.dll','pdb':unit+'.pdb','doc':unit+'.xml','generatedfilesout':unit+'-generated','errorlog':unit+'-errors.json'}[key]
                line='-'+key+':"'+(output/name).as_posix()+'"'
            elif unit=='Assembly-CSharp-Editor' and re.match(r'^[-/]r:',line) and 'Assembly-CSharp.ref.dll' in line:
                line='-r:"'+(output/'Assembly-CSharp.ref.dll').as_posix()+'"'
            rewritten.append(line)
        staged=output/(unit+suffix)
        if unit=='Assembly-CSharp' and suffix=='.rsp2':
            rewritten+=['"'+p.relative_to(project.resolve()).as_posix()+'"' for p in sorted(new_runtime_sources)]
            source_count+=len(new_runtime_sources)
        if unit=='Assembly-CSharp-Editor' and suffix=='.rsp2':
            rewritten+=['"'+p.relative_to(project.resolve()).as_posix()+'"' for p in sorted(new_editor_sources)]
            source_count+=len(new_editor_sources)
        staged.write_text('\n'.join(rewritten)+'\n',encoding='utf-8')
        (output/(unit+suffix+'.original')).write_bytes(raw)
        inputs.append(dict(path=str(original),sha256=hashlib.sha256(raw).hexdigest(),staged=str(staged)))
    command=[str(host),'exec',str(compiler),'/nostdlib','/noconfig']+['@'+row['staged']for row in inputs]
    run=subprocess.run(command,cwd=project,capture_output=True,text=True,errors='replace',timeout=180)
    log=output/(unit+'.log');log.write_text(run.stdout+run.stderr,encoding='utf-8')
    rows.append(dict(unit=unit,exit=run.returncode,sources=source_count,inputs=inputs,command=command,log=str(log)))
    protected=[dict(path=str(path),before=before,after=digest(path))for path,before in protected_outputs.items()]
    assert all(row['before']==row['after'] for row in protected),'Original compiler outputs changed concurrently'
    (output/'result.json').write_text(json.dumps(dict(engineExecuted=False,allAssetSourcesCovered=True,assetSourceCount=len(actual_sources),originalCompilerOutputsUnchanged=True,protectedCompilerOutputs=protected,units=rows),indent=2)+'\n')
    print(unit,'exit='+str(run.returncode),'sources='+str(source_count),flush=True)
    if run.returncode:
        print(run.stdout+run.stderr)
        raise SystemExit(run.returncode)
print('Actual Editor project source/namespace context compiles. Native behavior is unrun.')
