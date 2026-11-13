from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12';S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12'
latest=json.loads((D/'latest.json').read_text());run=R/latest['run'];pins=json.loads((D/(run.name+'-pins.json')).read_text());lines=[]
for n in [1,2,3,4,5,6]:
 p=S/'before/docs/agent-runs/20261104-three-chats/unity'/('accepted-neutral-v%d.json'%n);v=json.loads(p.read_text(encoding='utf-8-sig'));lines.append(dict(version=n,status=v.get('status'),M1=v.get('M1'),accepted=[{k:e[k] for k in ['name','scope','remaining'] if k in e} for e in v.get('accepted',[])]))
(D/'acceptance-scopes.json').write_text(json.dumps(lines,indent=2))
# Verify accepted Modes without changing a byte.
v=json.loads((S/'before/docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'));checks=[]
for row in v['sourceChecks']:
 if row['name']=='Modes':
  p=Path(row['path']);actual=hashlib.sha256(p.read_bytes()).hexdigest();checks.append(dict(path=str(p),expected=row['expected'],actual=actual,match=actual.lower()==row['expected'].lower()))
assert all(x['match'] for x in checks);(D/'accepted-modes.json').write_text(json.dumps(checks,indent=2))
sourcepins=[p for p in pins if p['source'].endswith('.cs') and ('/Modules/' in p['source'] or '/MatchDirector/' in p['source'] or '/CombatPublication/' in p['source'])]
(D/'API-REQUESTS.md').write_text('''# Root ports still required — not implemented or approved

G2 OPEN; M1 OPEN. `PartialDirectorPing/1` is historical/quarantined only.

1. `HealthService.CreateCheckpoint`, `CombatRules.CaptureArchive`, `HealthPublicationLane.SuccessfulCheckpoint`, `Session.CaptureCheckpoint`, and `RoundController.CaptureCheckpoint` do not export one typed common logical-time/source/epoch/sequence/ACK descriptor proving all captured bytes belong to one checkpoint instant. DirectorPingArchive checks only actual stored Session/Round/Director/Ping time headers; it cannot infer missing Health/Combat/publication proof from their supplied clocks. Request owner-private verified metadata over exact original bytes, then a root-authenticated common receipt with digest/freshness. A caller `time`, boolean, callback or recomputed self-digest is insufficient. Relevant pinned sources/hashes are in final candidate pins and source-port-pins.json.
2. `ReceiptFamilyArchive.Capture` captures original primary/backup under the real cooperative lock; `ScoreReceiptJournal.CaptureArchive` captures exact in-memory payload/index/ACK state, including failed writes. There is no public typed proof that this physical family and logical journal belong to the same producer generation. They must remain separate retained artifacts in this partial envelope until root binds physical source identity plus producer checkpoint digest/generation/sequence/ACK to them. Matching contents alone cannot prove provenance.
3. `Director.CreateRecoveryPingOwner` is an explicit new private-owner lifecycle; it does not export the accepted private `Director.pings` or switch `Director.Ping`/`VisiblePings`. No migration of an already-running accepted PingService is possible through its current public API. A deliberate reviewed root lifecycle selection is needed before this owner can route gameplay.
4. `RestorePartialPingArchive` constructs fresh Session/Round/Director/Ping objects, but Health/publication and gate/identity/world/reconnect factories remain caller-supplied quarantine dependencies. Root must provide authenticated connection remapping, physical/world state, registered actual participants, validated-token disposition, durable adoption and old-epoch tombstones. Existing `HealthProducerDrain.Retirement` and global producer retirement refusal are unchanged. No new recovery object exposes Director/Session/Round or a score replay/adoption method.

These requests identify missing evidence and owner interfaces; no fake signatures are reported as implemented. Same-binding mixed snapshots with an attacker-recomputed digest require authenticated outer receipt rejection at root, not a local self-authentication claim.
''')
(D/'source-port-pins.json').write_text(json.dumps(sourcepins,indent=2))
report='''# MatchPingRecoveryR12 — partial owner composition

Implemented candidate; no self-promotion. DirectorArchiveReviewR11 availability/full coverage gates remain unresolved. G2 OPEN, M1 OPEN; independent review required. No engine/import/build/native/device/UI/VR execution, children, sibling messages, installs or shared contract/accepted owner edits.

## Concrete code

- `DirectorPingArchive.cs:14`: `Director.CreateRecoveryPingOwner(double cooldown,int journalCapacity)` creates a separately versioned RecoverablePingService bound privately to the actual Director roster/gate/session/epoch/map/team. `RecoveryPingOwner.Place` uses actual Director.Time and actual owner cooldown/replay. Accepted Director.Ping and VisiblePings still use the original service, unchanged. This explicit new lifecycle is not automatic migration or production routing.
- `RecoveryPingOwner.CapturePartialArchive(CheckpointBinding,byte[] physicalReceiptFamily)` captures DirectorHistorical/1, actual private-owner Ping codec and original validated receipt primary/backup bytes in bounded `PartialDirectorPing/1`. Binding covers build/catalog/session/owner/epoch/life/connection/sequence. `MatchPingHeaders.Check` checks known exact codec identities and actual stored Director/Session/Round/Ping times for equality; NaN/infinity/negative, missing/unverifiable equality and unknown codecs reject. No caller clock assertion substitutes for these available fields. This proves only partial owner-time equality, not complete coherent bundle time.
- `Director.RestorePartialPingArchive(...)` validates trusted expected outer digest/binding, unpacks strict bounded blobs and trailing-data check, constructs a fresh actual Session from embedded owner bytes, reconstructs actual historical Director/Round/producer and binds actual new Ping to that reconstructed private Director. Existing private owner decoders preserve typed records, committed score eligibility, historical life, aliases, pending producer, journal/ACK state and replay/cooldown. No Join/Register/Spawn/Grant/replay-as-restore, live owner mutation, store writes or adoption.
- `DirectorArchive.cs:20` adds only internal `QuarantinedArchive.RestorePing` to bind the private roster without publishing the Director. The original historical format and public quarantine/readiness/global refusal remain unchanged. `QuarantinedPingArchive` exposes read-only scores/history/receipt counts/visible records plus an explicitly quarantined Ping probe. It never exposes actual Director/Session/Round/producer references or score-resumption/adoption capability.
- `Director.RequireCompleteRecovery()` always refuses with concrete absent owner logical-time/physical-source proof. This is not a replacement for root global retirement or an implemented complete bundle.

## Executed evidence

Historical baseline uses exact R11 pinned sources (combined historical reproduction only): ARCHIVE PASS88 plus two COLD PASS8 subprocesses; original R9 PASS608; external public API INDEPENDENT PASS120. This preserves archive/physical cuts, remap and global refusal observations without relabeling them as new cold adoption.

Final candidate RUN: RUNNAME. Actual C#5 `/warn:4 /warnaserror+` compiler-only graph: Core, RecoveryContracts, Modes, ModesRecovery, HealthStates, Combat, Persistence, Sessions, MatchRounds, CombatPublication (including actual CombatHealthBridge partial closure), MatchDirector and MatchRecovery are twelve separate production DLLs, then external Tests.exe. Every compile exits0. No merged-DLL workaround, fake APIs, stubs, friend assembly or native execution. The once-frozen current Combat/Health boundary now compiles; historical CS0122 remains in historical review, not silently erased. This is a declared CPU closure, not central Unity asmdef/project approval.

Final pair suite PASS13423 (counts include setup and each truncation; not unique scenarios), two fresh subprocesses PASS9 each. It exercises death before score and score before physical ACK write success; two repeated fresh reconstructions each; original Alive historical versus latest Dead Health; pending eligibility/producer, exact credited totals0/1, original physical pending receipt; private Ping visible mark, original command replay refusal, retained cooldown, later retry only on quarantined Ping; no duplicate credited score/death on original live retry. Every truncated envelope prefix rejects; wrong build/epoch/life/connection/sequence, trailing data, nonfinite/negative/forged partial time and divergent owner clocks reject. 8192 actual commands fill private Ping history, 8193rd rejects without evicting earliest replay; capture/reconstruct preserves saturation (image156399 bytes). Oversized family input rejects. External historical public API fixture also passes120 against the current separate owner DLLs, including original conflicting receipt and connection-remap observations.

Four separate compiled owner-DLL mutants compile0/run1: lost producer pending; latest Health substituted for historical life; removed exact owner-time equality; removed Ping cooldown. Original candidate DLLs stay in their own directory; mutation directories retain exact changed source, response files, DLLs and failure outputs. Earlier failed harness runs remain preserved: missing direct Combat reference, C# nested-private accessibility, and first lost-producer mutant SURVIVED because the initial fixture directly delivered instead of retaining producer ownership. The final fixture uses actual DrainProducer at the cut and kills it. No failure is hidden or counted as a production pass.

## Reproduce / pins

From workspace root:

    & 'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' unity/Tools/Integration/MatchPingRecovery/baseline.py
    & 'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' unity/Tools/Integration/MatchPingRecovery/validate.py

`validate.py` makes a fresh timestamped candidate, reuses frozen lower-owner bytes, freezes current owned candidate/tests/tools before compiling. `freeze.py` intentionally refuses a second freeze; do not refresh moving dependencies. Baseline runner preserves historical source copies. `inputs.json` pins original current sources and policies; final candidate `*-pins.json` identifies exact tested bytes. `*-commands.json` records every absolute compiler argv, stdout/stderr/exit; response files hold absolute backslash source/reference/output arguments. `SHA256-manifest.json` binds original/current inputs, scripts, fixtures, binaries and output files; compiler identity is in compiler.json. Source port pins/API requests are separate. Accepted Modes six exact hashes still match accepted-neutral-v1; v1..v6 remain unchanged.

## Remaining gates — not reduced for promotion

Full authenticated common Health/Combat/publication/Session/Match/Ping logical time and physical source proof are unavailable through current ports; `API-REQUESTS.md` identifies exact symbols. We retain the artifacts separately and reject complete recovery. Full durable-publication cold composition, whole-bundle physical/logical cut adoption, cold score resumption, 65536 Session history maximum and every prepared participant disposition remain unverified. New partial tests do not certify full cold remap/adoption; historical R9/current external remap observations retain their narrower scope. Same-binding mixed owner snapshots need root-authenticated expected whole digest; local self-hash cannot detect fabricated provenance. Complete rollback/retirement/authentication cannot be created by this leaf.

The final graph uses frozen CombatPublication inputs. Later external Ports.cs and ProducerRouting.cs drift is recorded separately in later-drift.json and is NOT qualified by this run. Owned DirectorArchive delta is separately identified. No waiting or substitution of moving dependencies. Original before bytes and all prior binaries/evidence are preserved. Root PC candidate04/Desktop/project/packages remain untouched; native/platform/physical controls/feel, real maps/audio/skins, WAN, G2 and M1 remain OPEN. Full 9EACH/noHIGH review gates are not satisfied by this partial implementation.
'''.replace('RUNNAME',run.name)
(D/'REPORT.md').write_text(report)
csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');(D/'compiler.json').write_text(json.dumps(dict(path=str(csc),sha256=hashlib.sha256(csc.read_bytes()).hexdigest()),indent=2))
print(run.name)

