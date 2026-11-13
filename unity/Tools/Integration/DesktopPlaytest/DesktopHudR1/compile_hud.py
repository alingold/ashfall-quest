"""Compiler only. Freeze exact sources/references; never starts Unity. Run from any cwd."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopHudR1'
CSC = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
EDITOR = Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed')

def digest(data):
    return hashlib.sha256(data).hexdigest()

def run():
    # Fresh run directories retain failed attempts rather than overwriting evidence.
    n = 1
    while (OUT / ('compile%02d' % n)).exists():
        n += 1
    output = OUT / ('compile%02d' % n)
    output.mkdir(parents=True)
    sources = [ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/DesktopPlaytestContext.cs',
               ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/Hud/DesktopHud.cs',
               Path(__file__).with_name('NativeHudFixture.cs'), Path(__file__)]
    references = [EDITOR / 'UnityEngine' / ('UnityEngine.' + name + 'Module.dll')
                  for name in ['Core', 'IMGUI', 'Physics', 'TextRendering', 'Mathematics']]
    references += [EDITOR.parent / 'NetStandard/ref/2.1.0/netstandard.dll']
    manifest = []
    frozen_sources, frozen_refs = [], []
    for i, source in enumerate(sources + references + [CSC]):
        data = source.read_bytes()
        frozen = output / ('%02d-' % i + source.name)
        frozen.write_bytes(data)
        manifest.append(dict(path=str(source), snapshot=str(frozen), bytes=len(data), sha256=digest(data)))
        if source in sources and source.suffix == '.cs':
            frozen_sources.append(frozen)
        elif source in references:
            frozen_refs.append(frozen)
    (output / 'inputs.json').write_text(json.dumps(manifest, indent=2))
    args = ['/nologo', '/nostdlib+', '/langversion:5', '/warn:4', '/warnaserror+',
            '/target:library', '/define:UNITY_6000_0_OR_NEWER', '/out:"%s"' % (output / 'DesktopHud.dll')]
    args += ['/reference:"%s"' % p for p in frozen_refs]
    args += ['"%s"' % p for p in frozen_sources]
    rsp = output / 'compile.rsp'
    rsp.write_text('\n'.join(args), encoding='utf-8')
    command = [str(CSC), '/noconfig', '@' + str(rsp)]
    result = subprocess.run(command, capture_output=True, text=True)
    (output / 'compiler.log').write_text(result.stdout + result.stderr)
    evidence = dict(command=command, returncode=result.returncode, unity='6000.6.8f1',
                    native_execution='UNRUN: root engine lane only',
                    outputs=[dict(path=str(p), sha256=digest(p.read_bytes())) for p in output.iterdir() if p.is_file()])
    (output / 'results.json').write_text(json.dumps(evidence, indent=2))
    print(json.dumps(dict(output=str(output), returncode=result.returncode, log=result.stdout + result.stderr)))
    return result.returncode

if __name__ == '__main__':
    raise SystemExit(run())
