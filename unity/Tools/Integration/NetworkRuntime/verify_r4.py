"""Freeze exact R3 closure and current candidate; compiler/neutral CPU only."""
import hashlib, json, pathlib, subprocess, sys
R = pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S = R / 'campaign-staging/20261104-three-chats/unity/NetworkDispatchR4'
D = R / 'docs/agent-runs/20261104-three-chats/unity/NetworkDispatchR4'
tag = sys.argv[1]
O = S / tag
L = D / tag
O.mkdir(parents=True, exist_ok=False)
L.mkdir(parents=True, exist_ok=False)
sha = lambda b: hashlib.sha256(b).hexdigest().upper()
old = R / 'docs/agent-runs/20261104-three-chats/unity/NetworkNativeLifecycleR3/verification03'
cmds = json.loads((old / 'commands.json').read_text(encoding='utf-8-sig'))
manifest, mapping = [], {}
for row in json.loads((old / 'compiled-sources.json').read_text(encoding='utf-8-sig')):
    p = pathlib.Path(row['snapshot']); b = p.read_bytes()
    assert sha(b) == row['sha256'].upper(), str(p)
    q = O / ('r3-' + str(len(manifest)) + '-' + p.name);q.write_bytes(b)
    mapping[str(p)] = str(q)
    manifest.append(dict(original=str(p), snapshot=str(q), sha256=sha(b), profile='exact-verification03'))
candidate = {}
for folder in ['unity/Assets/Ashfall/Integration/NetworkRuntime','unity/Tools/Integration/NetworkRuntime']:
    for p in sorted((R / folder).rglob('*.cs')):
        q = O / ('candidate-' + str(len(candidate)) + '-' + p.name);b=p.read_bytes();q.write_bytes(b)
        candidate[p.name] = str(q)
        manifest.append(dict(original=str(p),snapshot=str(q),sha256=sha(b),profile='candidate'))
# Owned coordinator/host use exact current bytes; other dependencies remain R3 pins.
for rel in ['Integration/Sessions/SessionNetworkCoordinator.cs','Integration/SessionReplication/Host.cs','Modules/Networking/Authority.cs']:
    p=R/'unity/Assets/Ashfall'/rel;q=O/('candidate-'+p.name);b=p.read_bytes();q.write_bytes(b)
    candidate[p.name]=str(q)
    manifest.append(dict(original=str(p),snapshot=str(q),sha256=sha(b),profile='candidate'))
records=[]
def run(name, args):
    p=subprocess.run(args,capture_output=True,text=True,timeout=60)
    (L/(name+'.txt')).write_text(p.stdout+p.stderr)
    records.append(dict(name=name,argv=args,command=subprocess.list2cmdline(args),exit=p.returncode))
    print(name,p.returncode,p.stdout+p.stderr)
    return p.returncode==0
def sources(args, current):
    result=[]
    for a in args:
        if not a.endswith('.cs'):continue
        name=pathlib.Path(a).name
        owned=name.startswith('owned')
        if owned:name=name.split('-',1)[1]
        replace=owned or name in ['SessionNetworkCoordinator.cs','Host.cs','Authority.cs']
        result.append(candidate.get(name,mapping[a]) if current and replace else mapping[a])
    return result
cpu=cmds[0]['argv']; csc=cpu[0]
base=[csc,'/nologo','/langversion:5','/warnaserror+','/target:exe','/define:ASHFALL_BINDING_RETIREMENT_API','/main:DispatchR4Tests']
ok=True
for current in [False,True]:
    label='current' if current else 'exact-r3'
    exe=str(O/(label+'.exe'))
    flags=['/define:R4_DIAGNOSTICS,ASHFALL_BINDING_RETIREMENT_API'] if current else []
    compiled=run(label+'-compile',base+flags+['/out:'+exe]+sources(cpu,current)+[candidate['DispatchR4Tests.cs']])
    ok=compiled and ok
    if compiled:ok=run(label+'-run',[exe]) and ok
for label, testpaths in [
    ('replication-revoked-holdouts', ['unity/Tools/Integration/SessionReplication/r5/Fixture.cs','campaign-staging/20261104-three-chats/unity/reviews/SessionReplication-r6/Holdouts.cs']),
    ('runtime-revoked-holdouts', ['campaign-staging/20261104-three-chats/unity/reviews/NetworkRuntimeReviewR2/Holdouts.cs'])]:
    tests=[]
    for rel in testpaths:
        p=R/rel;b=p.read_bytes();q=O/(label+'-'+p.name);q.write_bytes(b);tests.append(str(q))
        manifest.append(dict(original=str(p),snapshot=str(q),sha256=sha(b),profile='unchanged-prior-holdout'))
    exe=str(O/(label+'.exe'))
    args=[csc,'/nologo','/langversion:5','/warnaserror+','/target:exe','/define:ASHFALL_BINDING_RETIREMENT_API','/main:Holdouts','/out:'+exe]+sources(cpu,True)+tests
    compiled=run(label+'-compile',args);ok=compiled and ok
    if compiled:ok=run(label+'-run',[exe]) and ok
native=cmds[-1]['argv']; refs=[]
for a in native:
    if a.startswith('/reference:'):
        p=pathlib.Path(a[len('/reference:'):]);b=p.read_bytes();q=O/p.name;q.write_bytes(b)
        refs.append('/reference:'+str(q))
        manifest.append(dict(original=str(p),snapshot=str(q),sha256=sha(b),profile='installed-reference'))
args=[csc,'/nologo','/noconfig','/nostdlib+','/langversion:5','/warnaserror+','/target:library','/define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER,ASHFALL_BINDING_RETIREMENT_API','/out:'+str(O/'NativeApi.dll')]+refs+sources(native,True)
ok=run('native-api-compile',args) and ok
(L/'compiled-sources.json').write_text(json.dumps(manifest,indent=2))
(L/'commands.json').write_text(json.dumps(records,indent=2))
(L/'validation.json').write_text(json.dumps(dict(passed=ok,nativeExecution='UNRUN',globalNativeCensus='UNKNOWN',M1='OPEN'),indent=2))
raise SystemExit(0 if ok else 1)
