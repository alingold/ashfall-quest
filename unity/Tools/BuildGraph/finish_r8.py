"""Seal final compiler-only evidence after explicit verification completes."""
import json
import sys
from collections import Counter
from pathlib import Path
import build_graph as b
import package_graph_r8 as r


def main():
    run=r.BASE/sys.argv[1]; verification=run/'verification-final'
    tests=json.loads((verification/'tests.json').read_bytes())
    gates=json.loads((verification/'actual-package-gates.json').read_bytes())
    closure=json.loads((verification/'public-api-closure.json').read_bytes())
    assert all(v==0 for v in tests.values()), tests
    for key,value in gates.items():
        expected = 0 if key.endswith('declared') or key=='actualFeature-missingManagement' else 1
        assert value==expected,(key,value,expected)
    assert closure=={'A':0,'B':0,'C-1':1,'C-2':0},closure
    definitions={json.loads(p.read_bytes())['name']:json.loads(p.read_bytes()) for p in (run/'proposed').rglob('*.asmdef')}
    reference_errors=[]; reference_drift=[]; checked=set(); stats={}
    for profile in r.PROFILES:
        values=json.loads((run/profile/'results.json').read_bytes())
        reused=json.loads((run/profile/'reused-neutral.json').read_bytes())
        stats[profile]={'pass':sum(type(v)==int and v==0 for v in values.values()),'directFail':sum(type(v)==int and v!=0 for v in values.values()),'blockedExcludedDeferred':sum(type(v)!=int for v in values.values()),'reusedNeutral':len(reused)}
        for p in (run/profile).glob('*/references.json'):
            definition=definitions[p.parent.name]
            for item in json.loads(p.read_bytes()):
                ref=Path(item['path'])
                if 'ScriptAssemblies' in ref.parts or ref.is_relative_to(run):
                    if ref.stem not in definition['references']: reference_errors.append(str(p)+':'+str(ref))
                if definition['noEngineReferences'] and ref.name.startswith(('UnityEngine','UnityEditor')): reference_errors.append('noEngine:'+str(p))
                if profile not in ('editor','tests') and ref.name.startswith('UnityEditor'): reference_errors.append('player-editor-leak:'+str(p))
                if item['path'] not in checked:
                    checked.add(item['path'])
                    if not ref.exists() or b.digest(ref.read_bytes())!=item['sha256']:reference_drift.append(item['path'])
    assert not reference_errors,reference_errors
    b.dump(r.DOC/'reference-access-audit.json',{'errors':reference_errors,'externalOrArtifactDrift':reference_drift,'distinctReferenceFilesChecked':len(checked)})
    b.dump(r.DOC/'compiler-summary.json',stats)
    plan=run/'proposal.json'; inventory=run/'input-inventory.json'
    identity={'proposalSha256':b.digest(plan.read_bytes()),'inventorySha256':b.digest(inventory.read_bytes())}
    b.dump(r.DOC/'trusted-identities.json',identity)
    b.dump(r.DOC/'toolchain-identities.json', [{'path':str(p),'sha256':b.digest(p.read_bytes())} for p in (b.CSC,Path(sys.executable))])
    physical=json.loads((run/'physical-preconditions.json').read_bytes())
    drift=json.loads((run/'drift.json').read_bytes())
    package_errors=json.loads((run/'package-errors.json').read_bytes())
    report='''# BuildGraph R8 implementation handoff

**UNREVIEWED, DO NOT APPLY. M1 remains OPEN.** Required package edges are now
explicit in the staged proposals and compiler references follow those proposals.
No live asmdef/meta/package/project/accepted source was edited. All work is in
the assigned three roots; build_graph.py's prior bytes are in before-tools.

Final candidate: `campaign-staging/20261104-three-chats/unity/BuildGraphR8/final06`.
Pinned phase15 source bytes and physical metadata were captured once by
candidate02, then replayed. Phase15 deliberately omits the active Session writer;
no writer polling or messaging occurred. Its absence is a real integration
blocker. Candidate01's long-path setup failure and stopped preliminary compiler
attempts remain separate, unpromoted evidence. Final05 exposed a framework-glob
error: UnityEditor modules were included from the UnityEngine directory, and
Editor compilation duplicated the aggregate facade. Final06 restricts player
frameworks to UnityEngine DLLs and Editor frameworks to modular Editor DLLs.
Its reference audit checks every player invocation for any Editor DLL.

Player.Engine explicitly references Unity.InputSystem and Unity.XR.OpenXR.
Networking.Engine and NetworkRuntime.Engine explicitly reference Unity.Collections
and Unity.Networking.Transport. Namespace-based guarded discovery also supports
Unity.XR.Management, Mathematics and Burst when consumed. Actual OpenXRFeature
compiles without Management; OpenXRLoader public inheritance fails CS0012 without
Management and passes with it. Consequently no false mandatory Player Management
edge is asserted. Package identity audit reports zero errors for proposed edges
and verifies installed Input1.20.1/OpenXR1.18.0/XRManagement4.7.0/Transport6.6.0/
URP17.6.0. The Unity6000.6.8f1 stack and AFR1 are unchanged.

43 regression tests PASS (36 retained plus 7 new). Both retained device-name and
sealed-inventory mutants fail by assertions. Five actual installed package API
gates pass with declared edges and fail individually when that edge is removed.
CollectionHelper establishes the Collections dependency; NativeArray alone is
provided by built-in Unity references, and NativeList separately hit C#5 CS0648.
The actual pinned PlayerOpenXrLifecycleFeature source passes with explicit OpenXR
and its actual Core/Player DLLs, fails without OpenXR, and passes without Management.
The real three-DLL exposed-return-type fixture passes A/B, fails C with B only
(CS0012), and passes C with explicit A/B. No Unity stubs or generated DLL execution.

Every compiler invocation uses absolute Framework64 csc, C#5, warnings-as-errors,
explicit proposed project/package references and permitted system/Unity framework
references. noEngineReferences assemblies receive no UnityEngine/UnityEditor
DLLs. No package dependency closure is appended globally. Project transitive
closure is deliberately promoted into concrete asmdef references; edge-reasons
identifies conservative additions for review. Reused directive-free neutral CPU
DLLs are explicitly recorded and are not fresh native compilation evidence.

CPU/Windows/Android/Editor/test membership and exact sources are in membership.json.
Editor/test define/platform checks do not prove a Unity Test Runner result or
platform execution. Existing guard-ON MapNative/Editor findings remain in
original-validation.json. Unresolved catalog references outside proposed edges
(including samples/tests/built-in assemblies) remain package-errors.json; this
does not approve the entire installed package graph.

Actual blockers remain: InventoryItems/WeaponHandling reference upper-layer
InventoryRecovery checkpoint codecs and form cycles; parent namespace lexical
inference can add conservative edges, so source-symbols and original graph are
retained for root review. MatchDirector uses inaccessible/mismatched Health
constructors. NetworkRuntime lacks intentionally omitted active SessionReplication
and coordinator sources. Player.Engine still accesses internal PlayerInput.Finite.
Networking.Engine records CS0570 for WithNetworkConfigParameters under required
C#5. No failure is converted to PASS or source-merged fallback. Root's public
TryReadReserve(out int,out long)/SharesReserveWith boundary and six separate-DLL
assertions are captured as root phase15 evidence; the new whole graph's codec
cycle prevents claiming InventoryTransfers/current whole-tree acceptance.

R7 CombatPublication membership and Health/Combat named friend grants are retained
as concrete root-only proposal operations. They expose all internals to those
assemblies and still require policy review. New file/directory meta allocation,
physical source additions, current drift, approved production content, central
import/build/runtime, rendered/audio/Quest/VR/WAN evidence and independent review
remain open. No engine/import/device operation was performed.

The proposal records exact before hashes or absence, payload hashes and inverse
remove-if-hash/restore-if-hash metadata with preserved before bytes. preflight
authenticates externally held whole-plan and inventory hashes and checks paths,
links, source metadata, payload and inverse identity. It is read-only: no apply,
lock, rollback execution or TOCTOU guarantee. Current live preflight correctly
rejects drift. Never authorize by a digest read only from the editable plan.

Commands (bundled Python path is fixed in the dispatch):

```
python -B unity/Tools/BuildGraph/package_graph_r8.py final06 candidate02
python -B unity/Tools/BuildGraph/audit_r8.py final06
python -B unity/Tools/BuildGraph/verify_r8.py final06 verification-final
python -B unity/Tools/BuildGraph/finish_r8.py final06
```

Per-DLL compile.rsp/command.json/references.json/compiler.log record exact sources,
flags, references, SHA256s and diagnostics. Verification commands.json records
retained suites, and audit/mutant-*/command.json records mutant commands. Exact
source symbols/lines/hashes are in audit/source-symbols.json. Tool-source preserves
the compiler at proposal generation; final06-source records final verification tools.
Changed-file and complete evidence SHA manifests are sealed separately below.
'''
    report+='\nCompiler counts (pass includes disclosed neutral reuse):\n\n|Profile|Pass|Direct fail|Blocked/excluded/deferred|CPU reused|\n|---|---:|---:|---:|---:|\n'
    for profile,s in stats.items():report+='|%s|%d|%d|%d|%d|\n'%(profile,s['pass'],s['directFail'],s['blockedExcludedDeferred'],s['reusedNeutral'])
    report+='\nCurrent source drift records: %d; missing source meta preconditions: %d; whole-cache unresolved diagnostics: %d.\n'%(len(drift),len(physical['missingSourceMeta']),len(package_errors))
    report+='\nProposal SHA256: `%s`.\n\nInventory SHA256: `%s`.\n'%(identity['proposalSha256'],identity['inventorySha256'])
    (r.DOC/'REPORT.md').write_text(report,encoding='utf-8')
    final_source=r.DOC/'final06-source';final_source.mkdir()
    changed=[]
    for name in ('build_graph.py','package_graph_r8.py','verify_r8.py','test_package_graph_r8.py','audit_r8.py','finish_r8.py','README_R8.md'):
        source=b.ROOT/'unity/Tools/BuildGraph'/name; dest=final_source/name;dest.write_bytes(source.read_bytes())
        changed.append({'path':source.relative_to(b.ROOT).as_posix(),'sha256':b.digest(source.read_bytes()),'snapshot':dest.relative_to(b.ROOT).as_posix()})
    b.dump(r.DOC/'changed-file-sha256.json',changed)
    manifest=[{'path':p.relative_to(b.ROOT).as_posix(),'sha256':b.digest(p.read_bytes()),'bytes':p.stat().st_size} for p in sorted(run.rglob('*')) if p.is_file()]
    b.dump(r.DOC/'evidence-sha256.json',manifest)
    seal={p.name:b.digest(p.read_bytes()) for p in (r.DOC/'REPORT.md',r.DOC/'changed-file-sha256.json',r.DOC/'evidence-sha256.json',r.DOC/'trusted-identities.json',r.DOC/'toolchain-identities.json')}
    b.dump(r.DOC/'seal.json',seal)
    print(json.dumps({'stats':stats,'identity':identity,'tests':tests,'gates':gates,'closure':closure,'drift':len(drift)},indent=2))


if __name__=='__main__':main()
