"""Seal bounded implementation evidence, preserving every prior attempt."""
import sys
from pathlib import Path
import current_owner as c
run=c.BASE/sys.argv[1];d=c.DOC;d.mkdir(parents=True,exist_ok=True)
cap=c.load(run/'capture.json');plan=c.load(run/'plan.json');seal=c.load(run/'seal.json')
stats={'capturedFiles':len(cap['files']),'capturedBytes':sum(x['bytes'] for x in cap['files']),'sources':len(plan['sourceEnumeration']),'units':len(plan['units']),'installedReferences':len(cap['readonlyReferences']),'summary':c.load(run/'validation02/summary.json'),'unrunToolFixtures':len([x for x in c.load(run/'analysis/fixture-coverage.json') if x['disposition'].startswith('NOT_RUN')]),'missingSourceMeta':len(c.load(run/'proposal.json')['missingSourceMeta'])}
refdrift=[{'path':r['path'],'expected':r['sha256'],'now':c.sha(c.safe(Path(r['path'])).read_bytes())} for r in cap['readonlyReferences'] if c.sha(c.safe(Path(r['path'])).read_bytes())!=r['sha256']]
c.dump(run/'installed-reference-drift.json',refdrift)
drift=c.load(run/'dependency-drift.json');stats['changedOrRemovedInputs']=len(drift['changedOrRemoved']);stats['addedAssets']=len(drift['addedAssets']);stats['referenceDrift']=len(refdrift)
c.dump(d/'summary.json',stats);c.dump(d/'trusted-identities.json',seal)
commands=[]
for p in c.walk(run):
    if p.name=='command.json':commands.append({'receipt':str(p.relative_to(c.ROOT)),'argv':c.load(c.safe(p))})
c.dump(d/'compiler-commands.json',commands)
report='''# CurrentOwnerAssemblyR9 implementation handoff

UNREVIEWED. DO NOT APPLY. Full graph remains blocked. No self-promotion, M1/G2,
global retirement, complete recovery or production content approval is claimed.
Candidate04, Desktop sources/private project/packages and accepted-neutral-v1..v6
were not changed. No engine/import/build/native/device/UI/VR execution occurred.
Only neutral CLR fixtures executed. No delegates or sibling messages.

## Delivered implementation and fixed capture

New `unity/Tools/Integration/CurrentOwnerAssembly/` tools capture current explicit
owner sources, metadata, policies/reports, actual compiler and sole installed cache
identities; require externally held capture and plan hashes; enforce exact source
and fixture membership; compile each declared owner into a separate DLL; record
package edges, exact response files and rooted failures. Existing BuildGraph and
Closure tools are read-only. No owner sources are merged or fixed and no global
package/allreferences workaround is used. Conservative public project closure is
explicitly persisted as references, with extra-edge annotations, not hidden in csc.

`capture03` is the sole completed current source capture. Two earlier Windows long
path setup failures happened before source capture. Frozen source/tool/docs bytes
are under its snapshot; initial/final tool versions and all failed runs remain.
Installed DLLs are hash-pinned read-only, not duplicated caches. The legacy graph
omitted RecoveryContracts entirely; a once-only declaration step adds that exact
root boundary from frozen bytes. It also classifies guarded Integration sources.
Legacy graph/plan/proposal and findings are retained alongside the explicit plan.

Counts: {sources} actual sources, {units} declared units, {capturedFiles} captured
files ({capturedBytes} bytes), {installedReferences} installed/compiler reference
identities. There are {missingSourceMeta} missing source metadata preconditions.

## Actual compiler and behavioral evidence

Final validation02: CPU43 compile PASS, zero direct compiler failures,45 blocked
or deferred; installed Windows guarded53 PASS,4 direct failures,31 blocked/deferred.
Every owner is compiled freshly for its profile; there is no CPU reuse disguised
as native compilation. Editor/test-platform owners are explicitly deferred;
Android, TestRunner and native execution remain unrun.

Actual Core -> RecoveryContracts -> HealthStates -> Combat separate DLLs PASS.
InventoryItems, WeaponHandling and InventoryRecovery now compile separately: the
older R8 upward checkpoint codec cycle is absent in this capture. This does not
establish full mechanics, reservation, cold restore or gameplay adoption coverage.
The actual public CombatRules.CaptureArchive/ArchiveBindings consumer compiles,
and omitting Combat fails. No internal Health constructors or blanket friend
grant is used.

Public Health owner BoundaryTests passes108 observations, including all truncated
prefixes, invalid boolean mutations, fresh-object alias separation, byte roundtrip
and preserved sequence/state. Current HealthTests passes49 observations. Repeated
Boundary runs overlap and are not counted as new unique coverage. Two actual
separately compiled Health mutants are detected: returning null from decoded
records fails the unchanged consumer; adding an unknown HealthSnapshot field
throws the production field guard. Exact exceptions/exit codes are preserved.

Five installed public API positive/edge-omission pairs PASS as expected: Input,
OpenXR, XRManagement, Collections and Transport. Actual frozen
PlayerOpenXrLifecycleFeature passes with its explicit OpenXR edge and fails without
it. OpenXRLoader inheritance passes with Management and fails CS0012 without it.
These native API DLLs were never executed. Five production membership cases reject
source omission, duplicate membership, missing fixture, empty fixture and unknown
project edge. The guarded actual Boundary native fixture compiles and stays UNRUN.

## Rooted integration failures and required decisions

The sole graph SCC is Ashfall.Integration <-> Ashfall.Integration.CombatPublication.
CombatHealthBridge partial source is split by the inferred physical boundary.
No source merge, friend grant or asmref is applied to conceal it. Root must choose
the deliberate shared-partial assembly boundary. Conservative promoted references
spread this SCC into MatchDirector, SessionReplication/NetworkRuntime, lifecycle,
recovery and transaction consumers; blocked means untested actual API, not PASS.
R4's public Health record repair is verified directly, but this whole graph cannot
claim MatchDirector/current publication compilation while that SCC blocks it.

Four actual guarded direct failures:

- AudioRuntime.Engine NativeAudioRuntime.cs:85: CS0117 VerifiedPcm.VerifyCached.
- Player.Engine NativeRootPublication.cs and Unity player motor/driver callers:
  CS0117 internal PlayerInput.Finite across the DLL boundary.
- Networking.Engine UnityTransportAdapter.cs:32: CS0570 installed Transport6.6.0
  WithNetworkConfigParameters under required Framework C#5. This is a compiler/API
  combination failure, not evidence that Unity's own compiler fails.
- DesktopPlaytestBootstrap.cs:28 onward: CS0246 Arena/Pawn/Combat/Construction/Hud
  across the separately declared parent/child boundary. Candidate04 remains intact;
  this graph is not its private build closure and no Desktop edits are proposed.

Exact source symbols/hashes/lines are analysis/source-symbols.json; per-profile
analysis/*-rooted-failures.json includes full direct diagnostics and dependency
roots. Full source lists/references live in plan.json and every compile.rsp.
Frozen package manifests, GUID-resolved asmdef edges and consumed DLL identities
are analysis/installed-package-identities.json and declared-package-edges.json.

All {unrunToolFixtures} other captured tool C# files are enumerated as NOT_RUN in
fixture-coverage.json, including helper files; no complete owner fixture, full-game
regression, Editor/TestRunner, native authority/atomicity or hardware claim follows.
This is useful current compiler/API evidence, not a reduced feature acceptance gate.

## Proposal, identity and reproducibility

proposal.json and proposal-explicit/ contain reversible review-only asmdef payloads,
exact source/header/meta hashes, existing-before hashes/absence, root-only metadata
allocation preconditions and conditional rollback identities. No new GUIDs allocated.
Missing metadata and full graph failures prohibit applying this proposal. No apply,
rollback executor, authenticity service, source lock or TOCTOU protection exists.
Trusted external digests must be retained independently from editable artifacts.

Capture SHA256 `{capture}`.
Plan SHA256 `{plan}`.
Proposal SHA256 `{proposal}`.

Commands use the supplied absolute bundled Python with `-B`. Final compiler sources
are tool-source02 (plus separately frozen summarize/extra tools); compiler arguments
are absolute backslash paths, Framework64 csc /noconfig /langversion:5 /warn:4
/warnaserror+, with explicit BCL/Unity framework and declared project/package refs.
Exact per-invocation argv, references, hashes, logs and response files remain beside
each DLL; compiler-commands.json aggregates their receipts.

```
current_owner.py freeze capture03
declare.py capture03
current_owner.py validate capture03 --capture CAPTURE --plan PLAN --output validation02
gates.py capture03 CAPTURE PLAN gates02
extra_gates.py capture03 CAPTURE PLAN
summarize.py capture03
current_owner.py drift capture03
finish.py capture03
```

Initial validation's Boundary and initial gates failed because /noconfig suppresses
the default System.dll reference. The corrected harness explicitly supplies BCL
references; gates02 and validation02 retain the same frozen owner bytes/plan.
Failed outputs are preserved, not mislabeled source failures or overwritten.

The single final source comparison records {changedOrRemovedInputs} changed/removed
inputs and {addedAssets} newly enumerated Assets; installed reference drift is
{referenceDrift}. dependency-drift.json distinguishes later bytes from tested bytes.
No waiting, polling or refresh onto a moving dependency occurred. Only the three
assigned ownership roots were written. Root integration and fresh independent
review remain necessary; all full feature/content/authority/recovery gates remain.
'''.format(**stats,**seal)
(d/'REPORT.md').write_text(report,encoding='utf-8')
# Complete artifact manifest includes actual frozen inputs, binaries, commands,
# logs and reports. The manifest excludes itself to avoid a circular hash.
files=[]
for root in (c.BASE,c.DOC,c.ROOT/'unity/Tools/Integration/CurrentOwnerAssembly'):
    for p in c.walk(root):
        if p.name=='sha256-manifest.json':continue
        b=c.safe(p).read_bytes();files.append({'path':p.relative_to(c.ROOT).as_posix(),'bytes':len(b),'sha256':c.sha(b)})
c.dump(d/'sha256-manifest.json',files)
print(stats)
print('manifest',len(files),'SHA256',c.sha((d/'sha256-manifest.json').read_bytes()))
