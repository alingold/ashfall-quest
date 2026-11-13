import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchDirectorR4/r5';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchDirectorR4/r5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
checks=[]
for x in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/MatchDirector/r3/changed-files.sha256.json').read_text(encoding='utf-8-sig')):
 p=R/x['path'];before=S/'before'/x['path'];actual=sha(p);saved=sha(before) if before.exists() else None
 checks.append(dict(path=x['path'],expected=x['sha256'],current=actual,preservedBefore=saved,match=actual==x['sha256'],preservedMatch=saved==x['sha256']))
(D/'historical-manifest-verification.json').write_text(json.dumps(checks,indent=2))
current=[dict(path=str(p.relative_to(S/'current-dependencies')),sha256=sha(p)) for p in (S/'current-dependencies').rglob('*.cs')]
(D/'current-snapshot.json').write_text(json.dumps(current,indent=2))
source='\n'.join('| '+p.name+' | '+sha(p)+' |' for p in (S/'candidate').glob('*.cs'))
report='''# MatchDirectorR4 / r5 implementation handoff

Status: **substantive bounded candidate; incomplete integration; needs fresh independent review. Do not promote.** No engine, runtime, visual, XR, Quest, WAN or full-game approval.

## Findings and implemented changes

H1: the r3 score and UpdateLimits commands shared a single bounded Modes ledger. A limit update exhausted capacity before committed Health damage could be ingested. Director now reserves 8192 additional scoring admissions while independently limiting successful UpdateLimits admissions to the original capacity. This is derived from the pinned HealthService's non-evicting 8192 damage-receipt bound; one HealthService per epoch is mandatory. It does not reset, evict or bypass Modes authority/replay validation. Actual Modes.RecordKnockdown still mutates the score. At configured capacity one, a limit update, actual committed death, retained replay, timer, next round and final acknowledgment all succeed. An oversized capacity that would overflow is rejected. This is a bounded composition fix, not an arbitrary unbounded journal.

Late delivery after Results is distinct: actual accepted Modes/2 cannot score outside Match. With the durable journal, Director retains a discoverable uncredited receipt, publishes the Health projection it can support, and allows lifecycle progress without deleting/acknowledging that receipt. Without the journal it fails closed with late-score-durable-journal-required. Fault explicitly reports the missing reconciliation API. Event replay does not score again. This prevents silent loss, but does NOT make late scoring complete.

H2: owned ILogicalPlayerIdentity and Director.BindConnection/CanonicalActor map the new authenticated connection to the retained logical Health actor. Binding requires a real current Session member, existing logical Health projection, unchanged team, no old active connection, no conflicting mapping and no distinct existing Health identity under the proposed connection ID. TryGet, Snapshot, LifeSequence, final Session elimination and Health binding use that map. Repeated actual Session token reconnects preserve injuries, Health LifeGeneration, roster team and life sequence without Health.Register(new actor). The host must resolve Health mutation actor/target and world pose access through the canonical identity. The deterministic test fixture supplies the trusted identity port; production authenticated provisioning is NOT implemented by pretending a Session receipt API exists.

M2: Director retains canonical roster rows from accepted Health projection and uses them only during the trusted scoring call. The killer disconnected after damage capture still receives actual Modes team credit; public disconnected roster visibility stays disconnected. Source Quest _die_r13 and separate game hurt award at the alive-to-downed/dead transition; bleedout does not award again and successful revive starts another scoring life. All original down/revive/bleedout assertions remain. CommittedAttribution itself is unchanged and still derives killer identity from actual Health.FindDamage.

G1 partial: ScoreReceiptJournal uses actual ISettingsStore.Write/Flush, composed in tests with unchanged accepted AtomicSettingsStore and PhysicalSettingsFiles. Immutable binary/base64 receipts bind session/epoch/event sequence, victim/revision/before revision/kind, life sequence, killer ID/team and victim team/life. A per-session/epoch index retains at most8192 receipts without eviction; a separate acknowledgment marker never deletes payloads. Pending discovers uncredited receipts after a real disk reopen. Conflicting retries reject; matching retries re-flush after prior failure. Actual filesystem family-lock contention rejects before scoring/publication, then retry succeeds. This is durable receipt storage, NOT an atomic damage/Health/Modes transaction or complete Director restart recovery.

M1 remains blocked: actual Sessions/3 enum has Pending/Alive/Eliminated/Spectator and no downed/revive CAS projection API. Owned ISessionHealthProjection.Project(session,epoch,connectionActor,HealthSnapshot) stages the requirement; no fake successful implementation is provided. IntegrationReadiness rejects missing identity, Session projection or durable receipt ports. Legacy constructor remains for regression compatibility and still exhibits the downed split. It must not be considered production-ready.

## Exact validation

Entry commands (bundled Python; no execution-policy changes):

    & 'C:\\Users\\aling\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe' 'C:\\Users\\aling\\Documents\\ashfall\\unity\\Tools\\Integration\\MatchDirector\\r5\\make_tests.py'
    & 'C:\\Users\\aling\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe' 'C:\\Users\\aling\\Documents\\ashfall\\unity\\Tools\\Integration\\MatchDirector\\r5\\run.py'

commands-results.json contains all14 expanded compiler/run commands, stdout/stderr and exits. Every compile invokes Framework64 v4.0.30319 csc with /nologo /langversion:5 /warn:4 /warnaserror+ /target:exe and quoted absolute Windows-backslash response-file paths. All14 exit0.

| Frozen source suite | Result |
|---|---|
| Exact r3 sealed dependency snapshots + untouched builder tests |255 assertions, exit0|
| Exact r3 sealed dependency snapshots + untouched independent diagnostics |137 assertions, four historical counterexamples reproduced, exit0|
| r5 candidate + initial pinned dependencies |255 original builder assertions, exit0|
| r5 candidate + updated independent diagnostics |137 assertions, exit0; H1/M2 expectations changed explicitly; legacy H2/M1 counterexamples retained|
| r5 additional composition tests + initial pinned dependencies |249 assertions, exit0|
| r5 candidate + separate current dependency snapshot, original builder |255 assertions, exit0|
| r5 candidate + separate current dependency snapshot, additional tests |249 assertions, exit0|

before.json records18 files = Core1 + Modes6 + Sessions2 + Health3 + Persistence3 + owned3. Tests compile the15 dependency files with the4 candidate sources. No Unity stubs, replacement shared modules or define-OFF runtime claims. Assertion totals include setup/replays, not that many independent scenarios. All255 prior checks and137 diagnostic assertions remain represented; UpdatedDiagnostics.cs changes only H1/M2 expected outcomes and explanatory output. Its journal console score=-1 is printed after BeginLoading cleared the old round; the additional test explicitly verifies score1 before turnover.

Additional tests exercise actual committed Health damage, attribution capture, repeated Session remaps while injured/downed, canonical life sequence and team, physical persistence reopen before/after acknowledgment, exact-retry/conflict handling, real family-lock failure, full limit budget plus score, clock-before-delivery uncredited retention, next round and complete acknowledgment. Existing builder tests cover actual down/revive/second down/bleedout, source score once per life, authority/clock rejection and65536-command Session saturation. The independent partial-commit diagnostic retains actual score-then-eliminate-denial/retry/death-once behavior. No simultaneous multithreaded execution or exhaustive arbitrary interleaving proof is claimed; the trusted lane is serialized.

## Provenance, drift and artifacts

Required override was read first; full orchestration/overnight/dispatch/preflight, v1/v2/v3 contracts, collision allocation, actual Core, ownership, accepted-neutral-v1/v2 and full MatchDirector-r3 review were read. Initial reviewed owned sources and small dependencies were copied before edits. CONTRACTS.md records owned contracts before implementation. freeze.py refuses to overwrite that initial snapshot. The11 exact r3 input hashes match sealed inputs and independent-review copies (r3-sealed-input-verification.json). Historical manifest verification distinguishes the now-intentionally changed owned live sources from their preserved-before bytes; historical artifacts were not edited.

Accepted Modes6/Persistence3 hashes were verified before work and remain unchanged. Session.cs and HealthService/PreparedGains changed concurrently; dependency-drift.json records exact before/after hashes. The separate current-dependencies tree and current-snapshot.json pin only the bytes actually compiled in that check. This is neutral source compatibility with a snapshot, not current whole-tree integration approval. No sibling work is declared successful. Core unchanged. Accepted v2 Inventory/ParityExpansion are not consumed or modified.

Quest baseline remains bdba6e30012e1733efd5021fd8ded593f82f26e6. Only relevant Quest _die_r13/bleedout and separate game hurt scoring symbols were reconciled, not whole-tree equivalence. No game .git assumption. World handoff semantic revision ce3b3b0771d779d93625bbe501c35c8953ab617924b4ae57b41cc1b299b5c0a3 differs from raw hash69399CF8C98DF0CE062FEFC37F6EDC8C1C0D37927A5D452F4D2266734A5E4AAD; this candidate consumes no world geometry/assets and grants no additional world approval.

## Candidate source SHA256

| Source | SHA256 |
|---|---|
'''+source+'''

The final manifest freezes all owned r5 evidence, tools, candidate and production-source bytes, excluding the manifest itself. Historical r3 tests/sources/binaries/reports remain unchanged. New writes stay in the four assigned MatchDirector roots, on C:. No package, lock, asmdef, meta, GUID, Core, bootstrap, scene, ProjectSettings, source Godot, world, skin, release or D: writes; no descendants or sibling messages.

## Required remaining gates / staged shared requests

1. Sessions owner/coordinator must supply a real authenticated canonical reconnect mapping receipt and CAS/idempotent Health-to-Session down/revive/death projection carrying session, epoch, logical actor, connection generation, Health revision/life generation and team. No SessionLife downed state can be implemented safely from the current public API. Test real spawn danger/denial and eliminated/spectator/expired reconnect, identity contention, equipment/world poses and remaps across epochs.
2. Accepted Modes needs a separately reviewed committed-event reconciliation API if late receipts must affect finalized Results, including immutable victim/killer eligibility, event key/life and exact epoch. This leaf did not change accepted Modes or invent successful post-Results score mutation. Uncredited receipts remain pending across round progression.
3. Full restart requires coordinated restore/checkpoint APIs for Health receipts/outbox, Session identity/lifecycle, Modes ledgers/totals and Director cursors/mapping. Receipt reopen alone does not provide them. Attribution capture-to-store and Health-to-consumer fanout still require a durable ownership boundary. Do not destructively drain a second Health consumer in production; tests drain only their dedicated fixtures.
4. Journal size/IO failure remains fail-closed with upstream outbox retry. No stress proof at all8192 worst-case long IDs, store document capacity, process-kill crash points or durable score-ack atomicity. Ping ledger connection-key continuity across remaps is not fully solved by the roster alias.
5. Installed central target is Unity6000.6.8f1/Input1.20.1/OpenXR1.18.0/XRManagement4.7/URP17.6/Transport6.6.0. No candidate Editor/package/API/import/render/runtime/Physics/device operation was performed. Previous coordinator probe is not this candidate's approval. Fresh independent review of these exact hashes is required; builder has not self-promoted.
'''
(D/'REPORT.md').write_text(report,encoding='utf-8')
paths=[]
for root in [S,D,R/'unity/Tools/Integration/MatchDirector/r5',R/'unity/Assets/Ashfall/Integration/MatchDirector']:
 paths.extend(p for p in root.rglob('*') if p.is_file() and p.name!='manifest.sha256.json')
manifest=[dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(set(paths))]
(D/'manifest.sha256.json').write_text(json.dumps(manifest,indent=2))
print('Sealed',len(manifest),'files; manifest SHA256',sha(D/'manifest.sha256.json'))
