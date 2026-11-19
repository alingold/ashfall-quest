#!/usr/bin/env python3
"""FLIGHT-DIVE r12: compile+run the CPU flight suites on Linux with Unity's bundled Mono csc (no editor, no license).
Usage: python3 -I Tools/Modules/Flight/R26RunFlightSuites.py <new out dir outside unity/> [--only a,b] [--overlay DIR]
--overlay DIR replaces repo-relative sources with copies under DIR (used for before/after runs).
FLIGHT2 overlays update drag/descent oracles and intentional forward-stick priority.
Run R26RunRigDiveTests.py separately for the production input/activity path."""
import sys, os, re, subprocess, glob
from pathlib import Path
REPO = Path.home()/'ashfall/unity'
MONO = Path.home()/'Unity/Hub/Editor/6000.6.8f1/Editor/Data/MonoBleedingEdge'
CSC = [str(MONO/'bin/mono'), str(MONO/'lib/mono/4.5/csc.exe')]
out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
overlay = Path(sys.argv[sys.argv.index('--overlay')+1]) if '--overlay' in sys.argv else None
only = sys.argv[sys.argv.index('--only')+1].split(',') if '--only' in sys.argv else None
def src(rel):
    rel = str(rel)
    if overlay and (overlay/rel).exists(): return str(overlay/rel)
    return str(REPO/rel)
def g(pattern): return sorted(str(Path(p).relative_to(REPO)) for p in glob.glob(str(REPO/pattern)))
def rsp(path):
    files = []
    for line in Path(path).read_text().splitlines():
        line = line.strip().strip('"')
        if line.startswith('/') or not line.endswith('.cs'): continue
        rel = re.sub(r'^.*?\\ashfall\\unity\\', '', line).replace('\\', '/')
        files.append(rel)
    return files
core = ['Assets/Ashfall/Core/Contracts.cs']
flight = [p for p in g('Assets/Ashfall/Modules/Flight/*.cs')]
player = g('Assets/Ashfall/Modules/Player/*.cs')
motor = [p for p in g('Assets/Ashfall/Integration/FlightMotor/*.cs') if 'ReservedCapsuleRoute' not in p]
suites = {
  'flight-original': core+flight+['Tools/Modules/Flight/FlightTests.cs'],
  'flight-r2': core+flight+['Tools/Modules/Flight/R2Tests.cs'],
  'motor-r3': core+flight+player+motor+['Tools/Integration/FlightMotor/R3Tests.cs'],
  'motor-r5': core+flight+player+motor+['Tools/Integration/FlightMotor/r5/R5Tests.cs'],
  'motor-r6': core+flight+player+motor+['Tools/Integration/FlightMotor/r6/R6Tests.cs'],
  'prepared-pad': core+flight+player+motor+['Tools/Integration/PreparedPadOwner/PreparedPadTests.cs'],
  'motionfix': rsp(REPO/'Tools/PlayableR23/MotionFix/mechanics.rsp'),
  'activities': rsp(REPO/'Tools/PlayableR23/Activities/mechanics.exe.rsp'),
}
# New R26 suite, picked up once it exists.
if (REPO/'Tools/Modules/Flight/R26FreefallTests.cs').exists() or (overlay and (overlay/'Tools/Modules/Flight/R26FreefallTests.cs').exists()):
    suites['r26-freefall'] = core+flight+['Tools/Modules/Flight/R26FreefallTests.cs']
suites['r26-metrics'] = core+flight+['Tools/Modules/Flight/R26FlightMetrics.cs']
summary = []
for name, files in suites.items():
    if only and name not in only: continue
    # rsp lists may predate new flight files: add any missing Modules/Flight sources.
    if any('Modules/Flight/' in f for f in files):
        for f in flight:
            if f not in files: files = files + [f]
    files = [f for f in dict.fromkeys(files) if os.path.exists(src(f))]
    exe = out/(name+'.exe')
    args = CSC+['-nologo','-langversion:5','-warnaserror+','-target:exe','-r:System.dll','-r:System.Core.dll','-out:'+str(exe)]+[src(f) for f in files]
    c = subprocess.run(args, capture_output=True, text=True)
    (out/(name+'.compile.txt')).write_text(c.stdout+c.stderr)
    if c.returncode != 0:
        summary.append((name,'COMPILE-FAIL',(c.stdout+c.stderr).strip().splitlines()[-1][:200])); continue
    r = subprocess.run([str(MONO/'bin/mono'), str(exe)], capture_output=True, text=True, timeout=600)
    (out/(name+'.run.txt')).write_text(r.stdout+r.stderr)
    lines = [l for l in (r.stdout+r.stderr).splitlines() if l.strip() and 'abort_threads' not in l]
    fail = next((l for l in lines if 'Exception' in l), None)
    summary.append((name, 'PASS' if r.returncode == 0 else 'FAIL', (fail or (lines[-1] if lines else ''))[:200]))
for s in summary: print('%-16s %-13s %s' % s)
raise SystemExit(0 if all(s[1]=='PASS' for s in summary) else 1)
