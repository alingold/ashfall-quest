"""Compiler-only frozen-source qualification. Never launches Unity."""
import hashlib
import json
import pathlib
import shutil
import subprocess

ROOT = pathlib.Path(r'C:\Users\aling\Documents\ashfall')
BASE = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopArenaR1'
DOC = ROOT / 'docs/agent-runs/20261104-three-chats/unity/DesktopArenaR1'
EDITOR = pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
CSC = pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    BASE.mkdir(parents=True, exist_ok=True)
    DOC.mkdir(parents=True, exist_ok=True)
    number = 1
    while (BASE / ('compile%02d' % number)).exists():
        number += 1
    out = BASE / ('compile%02d' % number)
    out.mkdir()
    sources = [ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/DesktopPlaytestContext.cs',
               ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/Arena/DesktopArena.cs',
               ROOT / 'unity/Tools/Integration/DesktopPlaytest/DesktopArenaR1/DesktopArenaNativeFixture.cs']
    frozen = []
    records = []
    for source in sources + [pathlib.Path(__file__).resolve()]:
        target = out / source.name
        shutil.copyfile(source, target)
        records.append({'path': str(source), 'snapshot': str(target), 'sha256': sha(target)})
        if source.suffix == '.cs':
            frozen.append(target)
    refs = sorted((EDITOR / 'Managed/UnityEngine').glob('*.dll'))
    refs.append(EDITOR / 'NetStandard/ref/2.1.0/netstandard.dll')
    records += [{'reference': str(p), 'sha256': sha(p)} for p in refs + [CSC]]
    args = ['/nologo', '/nostdlib+', '/langversion:5', '/warnaserror+', '/target:library',
            '/define:UNITY_6000_0_OR_NEWER', '/out:' + str(out / 'DesktopArena.NativeApi.dll')]
    args += ['/reference:' + str(p) for p in refs] + [str(p) for p in frozen]
    rsp = out / 'native-api.rsp'
    rsp.write_text('\n'.join('"' + arg + '"' for arg in args), encoding='utf-8')
    cmd = [str(CSC), '/noconfig', '@' + str(rsp)]
    result = subprocess.run(cmd, capture_output=True, text=True)
    (out / 'native-api.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    report = {'command': cmd, 'exitCode': result.returncode, 'inputs': records,
              'scope': 'C#5 warnings-as-errors installed Unity 6000.6.8f1 API compilation only; native fixture NOT RUN',
              'currentCompatibility': [{'path': str(p), 'matchesFrozen': sha(p) == sha(q)} for p, q in zip(sources, frozen)]}
    report['outputs'] = [{'path': str(p), 'sha256': sha(p)} for p in out.iterdir() if p.is_file()]
    (out / 'results.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    shutil.copyfile(out / 'results.json', DOC / ('compile%02d-results.json' % number))
    print(json.dumps({'exitCode': result.returncode, 'out': str(out), 'diagnostics': result.stdout + result.stderr}))
    return result.returncode


if __name__ == '__main__':
    raise SystemExit(main())
