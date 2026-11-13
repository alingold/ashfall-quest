"""Freeze bytes, compile real installed APIs, execute BCL-only PCM/state checks. No engine."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(r'C:\Users\aling\Documents\ashfall')
BASE = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopAudioR1'
DOC = ROOT / 'docs/agent-runs/20261104-three-chats/unity/DesktopAudioR1'
TOOLS = ROOT / 'unity/Tools/Integration/DesktopPlaytest/DesktopAudioR1'
ASSETS = ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest'
CSC = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
EDITOR = Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    BASE.mkdir(parents=True, exist_ok=True)
    DOC.mkdir(parents=True, exist_ok=True)
    n = 1
    while (BASE / ('check%02d' % n)).exists():
        n += 1
    out = BASE / ('check%02d' % n)
    out.mkdir()
    sources = [ASSETS / 'DesktopPlaytestContext.cs', ASSETS / 'Audio/DesktopCue.cs',
               ASSETS / 'Audio/DesktopAudio.cs', TOOLS / 'Checks.cs', TOOLS / 'NativeGate.cs',
               Path(__file__).resolve()]
    inputs = []
    for src in sources:
        dst = out / src.name
        shutil.copyfile(src, dst)
        inputs.append({'path': str(src), 'snapshot': str(dst), 'sha256': sha(dst)})
    # Read complete policy/index/acceptance artifacts; retain exact small metadata inputs.
    run = ROOT / 'docs/agent-runs/20261104-three-chats/unity'
    metadata = list((ROOT / 'unity/Documentation').glob('*.md'))
    metadata += [ROOT / ('docs/' + name) for name in ['TWO-CHAT-20-AGENT-OVERRIDE.md',
        'HILL-CLIMB-ORCHESTRATION.md', 'OVERNIGHT-AGENT-PLAN-2026-11-03.md', 'AGENT-DISPATCH-BRIEFS-2026-11-03.md']]
    metadata += [ROOT / 'docs/agent-runs/20261104-0020/preflight.json', run / 'GAMEPLAY-CHECKPOINT-v1.md']
    metadata += [run / ('accepted-neutral-v%d.json' % i) for i in range(1, 7)]
    metadata += [run / p for p in ['phase16/source-index.json', 'phase16/dispatch-plan.json',
        'phase18/source-index.json', 'phase18/root-manifest.json', 'phase18/ROOT-REPORT.md',
        'phase19/ROOT-REPORT.md', 'phase19/dispatch-plan.json']]
    metadata += list((run / 'desktop-playtest/r1').glob('*'))
    stage = json.loads((run / 'desktop-playtest/r1/stage.json').read_text())
    root_context = next(x for x in stage['sources'] if x['source'].endswith('DesktopPlaytestContext.cs'))
    captured_context = ROOT / root_context['source']
    metadata.append(captured_context)
    root_compatibility = {'recordedContextHash': root_context['sha256'],
        'capturedContextHash': sha(captured_context), 'currentContextHash': sha(ASSETS / 'DesktopPlaytestContext.cs'),
        'matchesRecorded': sha(captured_context).lower() == root_context['sha256'].lower(),
        'currentMatchesCaptured': sha(ASSETS / 'DesktopPlaytestContext.cs') == sha(captured_context),
        'scope': 'context byte compatibility only; existing root native/build/startup gates exclude new Audio module'}
    evidence = out / 'evidence'
    evidence.mkdir()
    records = []
    for i, src in enumerate(metadata):
        if not src.is_file():
            continue
        data = src.read_bytes()
        # Keep logs/manifests only, never copy a build/cache.
        if len(data) > 2000000:
            records.append({'path': str(src), 'sha256': sha(src), 'bytes': len(data), 'copied': False})
            continue
        dst = evidence / ('%03d-' % i + src.name)
        dst.write_bytes(data)
        if src.suffix == '.json':
            json.loads(data.decode('utf-8-sig'))
        else:
            data.decode('utf-8-sig')
        records.append({'path': str(src), 'snapshot': str(dst), 'sha256': sha(dst), 'bytes': len(data)})
    (out / 'evidence-inputs.json').write_text(json.dumps(records, indent=2))
    refs = sorted((EDITOR / 'Managed/UnityEngine').glob('*.dll'))
    refs.append(EDITOR / 'NetStandard/ref/2.1.0/netstandard.dll')
    inputs += [{'reference': str(p), 'sha256': sha(p)} for p in refs + [CSC]]
    results = []
    def compile_one(label, args):
        rsp = out / (label + '.rsp')
        rsp.write_text('\n'.join('"' + x + '"' for x in args), encoding='utf-8')
        command = [str(CSC), '/noconfig', '@' + str(rsp)]
        done = subprocess.run(command, capture_output=True, text=True)
        (out / (label + '.log')).write_text(done.stdout + done.stderr)
        results.append({'name': label, 'command': command, 'exitCode': done.returncode})
        print(label, done.returncode, done.stdout, done.stderr)
        return done.returncode
    common = ['/nologo', '/langversion:5', '/warnaserror+']
    cpu = compile_one('pcm-state', common + ['/target:exe', '/out:' + str(out / 'Checks.exe'),
        str(out / 'DesktopCue.cs'), str(out / 'Checks.cs')])
    if cpu == 0:
        command = [str(out / 'Checks.exe')]
        done = subprocess.run(command, capture_output=True, text=True)
        (out / 'pcm-state-run.log').write_text(done.stdout + done.stderr)
        results.append({'name': 'pcm-state-run', 'command': command, 'exitCode': done.returncode})
        print(done.stdout, done.stderr)
    native = compile_one('installed-api', common + ['/nostdlib+', '/target:library',
        '/define:UNITY_6000_0_OR_NEWER', '/out:' + str(out / 'DesktopAudio.Native.dll')]
        + ['/reference:' + str(p) for p in refs]
        + [str(out / name) for name in ['DesktopPlaytestContext.cs', 'DesktopCue.cs', 'DesktopAudio.cs', 'NativeGate.cs']])
    report = {'scope': 'compiler/BCL tests only; native fixture NOT RUN; listening NOT RUN',
        'editor': '6000.6.8f1', 'inputs': inputs, 'results': results, 'rootCompatibility': root_compatibility,
        'currentCompatibility': [{'path': str(p), 'matchesFrozen': sha(p) == sha(out / p.name)} for p in sources],
        'outputs': [{'path': str(p), 'sha256': sha(p)} for p in out.rglob('*') if p.is_file()]}
    target = out / 'results.json'
    target.write_text(json.dumps(report, indent=2))
    shutil.copyfile(target, DOC / ('check%02d-results.json' % n))
    print('receipt', target)
    return max(r['exitCode'] for r in results)

if __name__ == '__main__':
    raise SystemExit(main())
