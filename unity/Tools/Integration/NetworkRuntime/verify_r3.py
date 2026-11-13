"""Compiler + CPU only. Native execution is exclusively root-owned."""
import hashlib
import json
import pathlib
import subprocess

R = pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S = R / 'campaign-staging/20261104-three-chats/unity/NetworkNativeLifecycleR3'
D = R / 'docs/agent-runs/20261104-three-chats/unity/NetworkNativeLifecycleR3'
index = 1
while (S / ('verification%02d' % index)).exists():
    index += 1
O = S / ('verification%02d' % index)
L = D / O.name
L.mkdir()
O.mkdir()  # Never overwrite a previous result.
sha = lambda b: hashlib.sha256(b).hexdigest().upper()
inputs = json.loads((D / 'inputs.json').read_text())
deps = []
for row in inputs:
    name = row['original'].replace('\\', '/')
    if row['kind'] != 'source' or '/Integration/NetworkRuntime/' in name or '/Tools/' in name:
        continue
    if '/Modules/Sessions/' in name and not name.endswith(('/Session.cs', '/Ports.cs')):
        continue
    if '/SourceState' in name or '/Engine/' in name:
        continue  # Active source-state writer is not adopted as a member replica.
    p = R / row['snapshot']
    assert sha(p.read_bytes()) == row['sha256']
    deps.append(p)
owned = []
for folder in ['unity/Assets/Ashfall/Integration/NetworkRuntime', 'unity/Tools/Integration/NetworkRuntime']:
    for p in sorted((R / folder).rglob('*.cs')):
        q = O / ('owned' + str(len(owned)) + '-' + p.name)
        q.write_bytes(p.read_bytes())
        owned.append(q)
commands = []
csc = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def invoke(name, cmd):
    p = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
    (L / (name + '.txt')).write_text(p.stdout + p.stderr)
    commands.append(dict(argv=cmd, command=subprocess.list2cmdline(cmd), exit=p.returncode))
    print(name, p.returncode, p.stdout + p.stderr)
    return p.returncode == 0

cpu = O / 'Cpu.exe'
ok = invoke('cpu-compile', [csc, '/nologo', '/langversion:5', '/warnaserror+', '/target:exe',
    '/define:ASHFALL_BINDING_RETIREMENT_API', '/main:R2Tests', '/out:' + str(cpu)] + list(map(str, deps + owned)))
if ok:
    ok = invoke('cpu-run', [str(cpu)])
E = pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
A = R / 'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies'
references = [E/'Managed/UnityEngine/UnityEngine.CoreModule.dll', E/'NetStandard/ref/2.1.0/netstandard.dll']
references += [A/n for n in ['Unity.Networking.Transport.dll', 'Unity.Collections.dll', 'Unity.Mathematics.dll', 'Unity.Burst.dll']]
ref_records = []
for p in references:
    b = p.read_bytes();q = O / p.name;q.write_bytes(b)
    ref_records.append(dict(original=str(p), snapshot=str(q), sha256=sha(b)))
native_owned = [p for p in owned if p.name.split('-', 1)[-1] in ['NativeFixtureR3.cs', 'NativeFixtureR2.cs', 'SyntheticSessionFixture.cs', 'LoopbackEndpoint.cs', 'BindingRetirement.cs', 'Ports.cs', 'SessionChannel.cs', 'SessionClientChannel.cs', 'SessionHostLane.cs', 'SessionRuntime.cs']]
native = invoke('native-api-compile', [csc, '/nologo', '/noconfig', '/nostdlib+', '/langversion:5',
    '/warnaserror+', '/target:library', '/define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER,ASHFALL_BINDING_RETIREMENT_API',
    '/out:' + str(O / 'NativeApi.dll')] + ['/reference:' + x['snapshot'] for x in ref_records] + list(map(str, deps + native_owned)))
(L / 'compiled-sources.json').write_text(json.dumps([dict(snapshot=str(p),sha256=sha(p.read_bytes())) for p in deps+owned], indent=2))
(L / 'commands.json').write_text(json.dumps(commands, indent=2))
(L / 'references.json').write_text(json.dumps(ref_records, indent=2))
drift = []
for x in inputs:
    if x['kind'] != 'source' or '/NetworkRuntime/' in x['original'].replace('\\','/'):
        continue
    p = R / x['original']
    actual = sha(p.read_bytes()) if p.exists() else 'MISSING'
    if actual != x['sha256']:
        drift.append(dict(original=x['original'], pinned=x['sha256'], current=actual, compiled=(R/x['snapshot']) in deps))
(L / 'dependency-drift.json').write_text(json.dumps(drift, indent=2))
(L / 'validation.json').write_text(json.dumps(dict(cpu=ok, nativeApiCompile=native,
    nativeRuntime='NOT RUN', globalNativeCensus='UNKNOWN', M1='OPEN', drift=drift), indent=2))
raise SystemExit(0 if ok and native else 1)
