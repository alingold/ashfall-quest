import pathlib,json,hashlib,subprocess,struct
R=pathlib.Path(__file__).resolve().parents[4]
P=R/'campaign-staging/20261104-three-chats/unity/AudioRuntimeR1'
E=R/'docs/agent-runs/20261104-three-chats/unity/AudioRuntimeR1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
# Only small relevant read-only symbols, no audio assets copied.
extra=['quest-r22/source/scripts/reference_audio_099.gd','game/scripts/reference_audio_099.gd']
rows=json.loads((P/'before.json').read_text(encoding='utf-8-sig'))
for rel in extra:
    src=R/rel;dst=P/'inputs'/rel;dst.parent.mkdir(parents=True,exist_ok=True)
    if not dst.exists():dst.write_bytes(src.read_bytes())
    rows.append(dict(path=rel,sha256=sha(dst)))
drift=[dict(path=x['path'],expected=x['sha256'],actual=sha(R/x['path'])) for x in rows if sha(R/x['path'])!=x['sha256']]
(E/'dependency-drift.json').write_text(json.dumps(dict(records=rows,drift=drift,scope='small consumed snapshots only; no whole-tree equivalence'),indent=2))
# Reproduce frozen accepted Audio-r4 state-machine gates on our captured dependency bytes.
out=P/'accepted-r4-reproduction';out.mkdir(exist_ok=False)
test=R/'unity/Tools/Integration/AudioSettings/AudioR4Tests.cs';dest=out/'AudioR4Tests.cs';dest.write_bytes(test.read_bytes())
fixture=out/'fixtures';fixture.mkdir()
pcm=struct.pack('<32h',*([8192]*32));wav=b'RIFF'+struct.pack('<I',36+len(pcm))+b'WAVEfmt '+struct.pack('<IHHIIHH',16,1,1,32000,64000,2,16)+b'data'+struct.pack('<I',len(pcm))+pcm
wave=fixture/'TEST-ONLY.wav';wave.write_bytes(wav)
(fixture/'fixture.tsv').write_text('ASHFALL-AUDIO/1\ngeneric/wind\t0\t'+str(wave)+'\t'+sha(wave)+'\ttest\taccepted:TEST-ONLY\toriginal-test\tSFX\t1\n')
sources=[p for p in (P/'inputs').rglob('*.cs') if 'Engine' not in p.parts]+[dest]
exe=out/'AcceptedR4.exe';rsp=out/'compile.rsp'
rsp.write_text('\n'.join(['/nologo','/warnaserror+','/langversion:5','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
c=subprocess.run(cmd,capture_output=True,text=True);(out/'compile.log').write_text(c.stdout+c.stderr)
assert c.returncode==0,c.stdout+c.stderr
t=subprocess.run([str(exe),str(out)],capture_output=True,text=True);(out/'tests.log').write_text(t.stdout+t.stderr)
(out/'result.json').write_text(json.dumps(dict(compile_exit=c.returncode,test_exit=t.returncode,commands=[cmd,[str(exe),str(out)]],scope='Frozen accepted neutral source reproduction; not native approval'),indent=2))
print(t.stdout+t.stderr);assert t.returncode==0
# Verify matching accepted hash records for each consumed source wherever present.
ledgers=[R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v'+str(n)+'.json') for n in [1,2,3]]
entries=[]
def visit(v):
    if isinstance(v,dict):
        if 'path' in v and ('expected' in v or 'sha256' in v):entries.append(v)
        for x in v.values():visit(x)
    elif isinstance(v,list):
        for x in v:visit(x)
for path in ledgers:visit(json.loads(path.read_text(encoding='utf-8-sig')))
matches=[]
for row in rows:
    target=(R/row['path']).resolve()
    for entry in entries:
        path=pathlib.Path(entry['path']);path=path if path.is_absolute() else R/path
        if path.resolve()==target:
            h=entry.get('expected',entry.get('sha256','')).upper();matches.append(dict(path=str(target),expected=h,actual=row['sha256'],match=h==row['sha256']))
(E/'accepted-consumed-checks.json').write_text(json.dumps(matches,indent=2))
assert not drift
assert all(x['match'] for x in matches)
# Exact immutable handoff and six patch identities, without interpreting as content approval.
world=R/'docs/agent-runs/20261104-three-chats/world/accepted-handoff-v2.json'
context=dict(path=str(world),sha256=sha(world),content=json.loads(world.read_text(encoding='utf-8-sig')))
(E/'world-context.json').write_text(json.dumps(context,indent=2))
# Final output identity inventory excludes itself. Keep source bytes in a sealed packet.
files=[]
for folder in [R/'unity/Assets/Ashfall/Integration/AudioRuntime',R/'unity/Tools/Integration/AudioRuntime']:
    for src in folder.rglob('*'):
        if src.is_file():
            dst=P/'candidate'/src.relative_to(R);dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(src.read_bytes())
            files.append(dict(path=str(src),snapshot=str(dst),sha256=sha(src)))
manifest=dict(status='unreviewed builder candidate; no promotion',target='6000.6.8f1 / central v3',files=files,dependency_drift=drift,accepted_consumed_records=len(matches),tests='run03:2210 PASS; accepted-r4-reproduction see tests.log',native='SOURCE ONLY; no native compile/playback/quality approval')
(E/'candidate-manifest.json').write_text(json.dumps(manifest,indent=2))
print('sealed files',len(files),'accepted hash records',len(matches),'dependency drift',len(drift),'world raw',context['sha256'])
