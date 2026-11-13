"""Direct C#5 compile/run; no PowerShell policy changes or engine operations."""
from pathlib import Path
import json, subprocess, sys
root = Path(__file__).resolve().parents[4]
out = root / 'campaign-staging/20261104-three-chats/unity/Persistence/r2'
evidence = root / 'docs/agent-runs/20261104-three-chats/unity/Persistence/r2'
compiler = Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
common = [root / 'unity/Assets/Ashfall/Core/Contracts.cs'] + sorted((root / 'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))
receipts = []
for name in ['PersistenceTests', 'ReviewRegressionR2', 'PersistenceR2Tests']:
    exe = out / (name + '.exe')
    command = [str(compiler), '/nologo', '/langversion:5', '/warnaserror+', '/target:exe', '/out:' + str(exe)] + [str(p) for p in common] + [str(root / ('unity/Tools/Modules/Persistence/' + name + '.cs'))]
    for phase, args in [('compile', command), ('tests', [str(exe), str(out)])]:
        result = subprocess.run(args, cwd=str(root), capture_output=True, text=True)
        log = evidence / (name + '-' + phase + '.log')
        log.write_text(result.stdout + result.stderr, encoding='utf-8')
        receipts.append({'suite': name, 'phase': phase, 'argv': args, 'exitCode': result.returncode, 'log': str(log.relative_to(root)).replace('\\', '/')})
        print(name, phase, 'exit', result.returncode)
        if result.returncode:
            print(result.stdout + result.stderr)
            (evidence / 'commands-results.json').write_text(json.dumps(receipts, indent=2))
            sys.exit(result.returncode)
(evidence / 'commands-results.json').write_text(json.dumps(receipts, indent=2))
(evidence / 'COMMANDS.txt').write_text('\n'.join(subprocess.list2cmdline(r['argv']) + '\nexit=' + str(r['exitCode']) for r in receipts))
