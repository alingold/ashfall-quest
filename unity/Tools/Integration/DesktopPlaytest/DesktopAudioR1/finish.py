"""Seal handoff; no engine or source mutation."""
import hashlib
import json
from pathlib import Path

ROOT = Path(r'C:\Users\aling\Documents\ashfall')
REL = 'docs/agent-runs/20261104-three-chats/unity/DesktopAudioR1'
DOC = ROOT / REL
OUT = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopAudioR1/check03'
report = json.loads((OUT / 'results.json').read_text())
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
source_rows = '\n'.join('- `' + str(Path(x['path']).relative_to(ROOT)) + '` SHA256 `' + x['sha256'] + '`'
                        for x in report['inputs'] if 'path' in x)
text = '''# DesktopAudioR1 implementation handoff

Candidate only. No promotion, production audio/content approval or M1 completion. Optional synthetic Windows feedback remains unintegrated; frozen context/bootstrap/scene/project/builds and all accepted modules were left unchanged. No engine/import/build/device/UI/listening execution occurred in this leaf.

## Delivered behavior

`Ashfall.Integration.DesktopPlaytest.Audio.DesktopAudio : MonoBehaviour, IDesktopPlaytestModule` reads the real context Hits/RemainingTargets/Paused. Root may explicitly create and Initialize it after Pawn/Combat. There is no automatic bootstrap. A death (target count decrease) takes priority over a simultaneous hit. Large counter jumps produce one cue. Counter rollback/target restoration and ResetRequested silence/disarm without writing gameplay fields. No command/authority/recovery semantics are inferred from these local counters.

One owned nonlooping, nonspatial AudioSource uses Play with a selected clip, never PlayOneShot, so voices never sum inside this module. A .09s hit / .14s death gate drops all intervening events without queueing. Accepted PeakLimiter is consequently not required or copied: there is no PCM summing or parallel voice path. Other root audio/mixer sources would require a separately reviewed final mix bound if enabled later.

Two mono 48kHz generated sine-envelope clips: hit2160 samples/45ms, death5280 samples/110ms. Absolute sample bound .16; exact silent endpoints. Native PCM total7440 floats/29,760 bytes excluding engine object overhead; temporary managed generation totals another29,760 bytes per initialization. No authored/source audio assets, file IO, network, microphone, HRTF, XR, climb synthesis or content provenance claim.

Before playback, inspect real scene listeners. Reuse an enabled listener on the exact context.View. Create one owned listener there only when no enabled listener exists elsewhere and the View has no disabled external listener. Any conflicting enabled listener silences this module and disables/destroys only its owned listener. External listeners remain untouched. View replacement withdraws the owned listener. External scene writers can independently create conflicting listeners between pumps; the next pump fails silent. The module does not claim authority over foreign components.

Pause, focus loss, suspension, configuration change, reset and disable stop/clear playback and disarm stale deltas. OnEnable resets baselines; disabled Pump cannot recreate a listener. DisposeAudio is terminal/idempotent, unsubscribes both context reset and actual AudioSettings.OnAudioConfigurationChanged, disables/destroys the owned source/listener and destroys both clips. Deferred Unity destruction is expected; the native coroutine crosses frame barriers. It never destroys the root owner, camera, or borrowed listener. Listener discovery returns a scene listener array each Pump; real allocation/performance remains a root native measurement, suitable only for this bounded small sandbox candidate.

## Actual validation

Final capture: `campaign-staging/20261104-three-chats/unity/DesktopAudioR1/check03/`.

- Actual Framework64 csc C#5 `/warnaserror+` BCL executable PASS:14,909 assertions (most are per-sample checks; not14,909 gameplay scenarios). Finite/amplitude/energy/DC/continuity/determinism/sample-storage bounds, invalid cue, simultaneous death priority, throttle/no backlog, pause/reset, invalid clock/rewind/negative/overflow counters and10,000-observation burst pressure. Burst emits112 cues over10seconds, with no retained queue.
- Hit RMS .07998149, mean .0000039926, maximum adjacent delta .01841314. Death RMS .07999244, mean .0000096687, delta .01003729. These numeric facts do not establish listening quality.
- Actual installed Unity6000.6.8f1 API compile PASS for context + both production files + native coroutine, `/noconfig /nostdlib+ /langversion:5 /warnaserror+ /define:UNITY_6000_0_OR_NEWER`. Real Managed/UnityEngine DLLs plus installed NetStandard/ref/2.1.0/netstandard.dll; every reference and compiler is hashed in results.json. No stubs. Uses AudioClip.Create/SetData/GetData, AudioSource.Play/Stop, AudioSettings.OnAudioConfigurationChanged, Object.FindObjectsByType<AudioListener>(), Camera.isActiveAndEnabled and ordinary lifecycle callbacks.
- First installed compile FAILED on obsolete FindObjectsSortMode overload with warnings-as-errors. check01 preserves exact original sources, compiler diagnostic and successful CPU run. check02 passed the supported parameterless overload. check03 includes the disabled-Pump lifecycle guard, stronger native PCM/clip cleanup assertions and explicit recorded-root context comparison. Earlier evidence remains immutable.

Run from work root:

```powershell
& 'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' unity/Tools/Integration/DesktopPlaytest/DesktopAudioR1/run.py
```

Exact final compiler commands (absolute backslash response-file arguments and all actual references captured):

```powershell
& 'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe' /noconfig '@C:\\Users\\aling\\Documents\\ashfall\\campaign-staging\\20261104-three-chats\\unity\\DesktopAudioR1\\check03\\pcm-state.rsp'
& 'C:\\Users\\aling\\Documents\\ashfall\\campaign-staging\\20261104-three-chats\\unity\\DesktopAudioR1\\check03\\Checks.exe'
& 'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe' /noconfig '@C:\\Users\\aling\\Documents\\ashfall\\campaign-staging\\20261104-three-chats\\unity\\DesktopAudioR1\\check03\\installed-api.rsp'
```

## Root native gate and remaining gates

Root alone can stage the two production files plus Tools/Integration/DesktopPlaytest/DesktopAudioR1/NativeGate.cs alongside its unchanged context. Run `DesktopAudioR1.NativeGate.Run()` as a PlayMode coroutine through engine_lane.py in an isolated scene with zero preexisting listeners. Do not add it to a production scene/bootstrap automatically. It creates real source/clips/listeners and stages PCM roundtrip, hit/death scheduling, pause/focus/suspension/reset, borrowed/foreign/disabled listener policy, disable/re-enable, view replacement, both clip destruction and idempotent source cleanup checks. Native gate is compiled but UNRUN. Unity playback scheduling, deferred resource lifetime, actual static configuration callbacks, repeated native reset pressure, allocation performance, audible output, relative loudness, clicks/feel and human listening remain OPEN. Native fixture checks scheduling/ownership; it cannot approve sound quality.

The recorded desktop root context hash matches this candidate's frozen/current context. This is byte/API compatibility only. Root's existing all-five native PASS, Mono Windows build PASS and Direct3D11 standalone startup PASS exclude new Audio; no native or packaged success transfers to this candidate. No root build was relaunched or mutated. Accepted-neutral-v6/M1 OPEN unchanged. Root stack remains Input1.20.1/OpenXR1.18.0/XRManagement4.7.0/URP17.6.0/UTP6.6.0/AFR1; this module adds no package edge and only needs Unity Core/Audio APIs plus frozen context.

## Frozen input hashes

'''
(DOC / 'REPORT.md').write_text(text + source_rows + '\n\nAll installed reference/output hashes, command exits and separately labeled current/root comparisons: `check03-results.json`. Complete read policy/accepted v1..v6/phase16(293)/phase18(302)/phase19 desktop native/build/startup artifacts are frozen and hashed in check03/evidence-inputs.json. The output seal includes all three captures, preserving the failed first candidate.\n', encoding='utf-8')
roots = [ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/Audio',
         ROOT / 'unity/Tools/Integration/DesktopPlaytest/DesktopAudioR1', DOC,
         ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopAudioR1']
entries = [{'path': str(p.relative_to(ROOT)), 'bytes': p.stat().st_size, 'sha256': sha(p)}
           for folder in roots for p in sorted(folder.rglob('*')) if p.is_file() and p.name != 'MANIFEST.json']
(DOC / 'MANIFEST.json').write_text(json.dumps({'scope': 'owned output seal excluding manifest itself', 'files': entries}, indent=2))
print(json.dumps({'files': len(entries), 'report': str(DOC / 'REPORT.md'), 'rootCompatibility': report['rootCompatibility']}, indent=2))
