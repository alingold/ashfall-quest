"""CPU-only LAN lobby/session regression harness against the LIVE Assets sources.

Consolidates the latest staged LobbyCases suites (copied verbatim from
campaign-staging lan-* checkpoints, 2026-11-07) plus RegressionCases.cs.
Compiles live production sources listed in sources.txt with .NET Framework csc
(langversion 5, warnings as errors) and a simulated in-memory transport.

This does not start Unity, import assets, touch Library or create sockets.
Native UTP/Physics/Root/UI/headset behavior is NOT exercised by this check.
Run: python check_lan_current.py --output-root <new empty dir outside unity/>
"""
from pathlib import Path
import argparse, datetime, hashlib, json, subprocess, os

parser = argparse.ArgumentParser()
parser.add_argument('--output-root', required=True, type=Path)
parser.add_argument('--define', action='append', default=[], help='e.g. BEFORE_FIX: omit cases that need post-fix APIs (reproduction run)')
args = parser.parse_args()
here = Path(__file__).resolve().parent
workspace = here.parents[4]
unity = workspace / 'unity'
out = args.output_root.resolve()
assert not out.is_relative_to(unity), 'Write harness output outside the Unity project'
assert not out.exists(), 'Use a fresh output directory; preserve earlier results'
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()

live = [workspace / line.strip() for line in (here / 'sources.txt').read_text().splitlines() if line.strip()]
missing = [str(p) for p in live if not p.is_file()]
assert not missing, 'Live source missing: ' + ', '.join(missing)
legacy = here.parent  # Map/Presence/Round/Spawn suites were current in place
cases = sorted(here.glob('*Cases.cs')) + [legacy / n for n in ['MapCases.cs', 'PresenceCases.cs', 'RoundCases.cs', 'SpawnCases.cs']]
sources = sorted(set(live)) + cases
out.mkdir(parents=True)
csc = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
flags = ['/nologo', '/langversion:5', '/warn:4', '/warnaserror+', '/target:exe', '/main:LobbyCases',
         '/out:' + str(out / 'Cases.exe'), '/reference:System.dll', '/reference:System.Core.dll']
if args.define:
    flags.append('/define:' + ';'.join(args.define))
rsp = out / 'cases.rsp'
rsp.write_text('\n'.join('"' + v + '"' for v in flags + [str(p) for p in sources]))
compiled = subprocess.run([csc, '/noconfig', '@' + str(rsp)], capture_output=True, text=True)
(out / 'compile.log').write_text(compiled.stdout + compiled.stderr)
receipt = dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), compileExit=compiled.returncode,
               liveSources=len(live), caseFiles=[str(p) for p in cases], defines=args.define,
               sourcePins=[dict(path=str(p), sha256=sha(p)) for p in sources],
               engineExecuted=False, nativeTransportExecuted=False, headsetExecuted=False)
if compiled.returncode == 0:
    worker_source = out / 'CrashWorkerEntry.cs'
    worker_source.write_text('public static class CrashWorkerEntry {public static void Main(string[] args){DurableCrashWorker.Run(args);}}')
    worker = out / 'CrashWorker.exe'
    probe = subprocess.run([csc, '/nologo', '/target:exe', '/out:' + str(worker), '/reference:' + str(out / 'Cases.exe'), str(worker_source)], capture_output=True, text=True)
    (out / 'worker-compile.log').write_text(probe.stdout + probe.stderr)
    assert probe.returncode == 0, probe.stdout + probe.stderr
    environment = os.environ.copy()
    environment['ASHFALL_RECEIPT_CRASH_WORKER'] = str(worker)
    environment['ASHFALL_RECEIPT_TEST_ROOT'] = str(out / 'storage-fixtures')
    run = subprocess.run([str(out / 'Cases.exe')], capture_output=True, text=True, timeout=300, env=environment)
    (out / 'results.log').write_text(run.stdout + run.stderr)
    receipt.update(exitCode=run.returncode, output=run.stdout, error=run.stderr)
    print(run.stdout + run.stderr)
else:
    receipt.update(exitCode=None)
    print(compiled.stdout + compiled.stderr)
(out / 'result.json').write_text(json.dumps(receipt, indent=2) + '\n')
assert compiled.returncode == 0 and receipt['exitCode'] == 0
