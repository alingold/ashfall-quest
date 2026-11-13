BuildGraphR7 is an unreviewed coordinator proposal tool. It never applies shared project files.

Target metadata: Unity6000.6.8f1, Input1.20.1, OpenXR1.18.0, XRManagement4.7.0,
URP17.6.0, Transport6.6.0. Package resolution, Unity API/import/runtime and device
acceptance remain unverified for this candidate. The central Editor probe only
proved its isolated import/mesh/Physics scenario.

Run with bundled Python and -B, using fresh direct children under
campaign-staging/20261104-three-chats/unity/BuildGraphR7:

    python -B unity/Tools/BuildGraph/test_build_graph.py retained-new
    python -B unity/Tools/BuildGraph/test_r4.py adversarial-new
    python -B unity/Tools/BuildGraph/test_r6.py r6-new
    python -B unity/Tools/BuildGraph/build_graph.py <absolute-fresh-output>

The original 15 cases remain; the existing source fixture additionally asserts
that invalid staging is rejected before writes. test_r4.py covers prewrite case
and path checks, existing output preservation, forward/name/GUID asmrefs, nearest
parent boundaries, nested overrides, dangling/duplicate/cyclic membership,
Editor/player and platform reference constraints, and plan tamper/drift checks.

Existing asmdefs are indexed before references are resolved. Existing definitions,
platform restrictions, source memberships and meta GUIDs are preserved, never
restaged as replacements. Unknown external asmrefs fail closed. Package assembly
allowlist names are explicit; no synthetic package GUIDs/locks. Case-sensitive C#
symbols remain intact; destination and membership paths use casefold checks on
all hosts. stage() preflights all targets before any mutation, rejects existing
outputs and never overwrites them. Invalid factory graphs retain evidence but
emit no assembly proposals and invoke no neutral compiler.

apply-plan.json contains input hashes, absent-target preconditions, staged
payload hashes and remove-if-hash rollback metadata, plus existing membership.
Schema3 binds stack and Unity scope. validate_plan(plan, live_root, staged)
regenerates actual membership and expected target/payload bytes without writing. The
coordinator must use its shared lane and revalidate immediately before any apply;
this is not an atomic/durable apply or rollback implementation. Source GUIDs in
guid-plan.json remain separate proposals for coordinator allocation.

Neutral closure and separate DLL compilation preserve accepted Toolchain/2,
Framework64 csc C#5 /warn:4 /warnaserror+ /noconfig and profile-compatible explicit
references. No System.Web extension, Unity stubs or alternate runner. All Engine
assemblies are excluded; Editor compile has Unity defines OFF. Neither proves
Unity API/import. net472 IDE projects are inspection aids, not Quest builds.

Replay accepts verified historical BuildGraph/phase03, BuildGraphR4/r5,
BuildGraph-r3/r5 review and owned R6 snapshots with --from-snapshot. The sealed
inventory, including metadata, is replayed exactly; paths, links, additions and
hashes are validated before copying. drift.json
separately reports changed/added live inputs; exit1 preserves any compile,
validation or drift failure and is never current-integration approval. Assembly
inference is lexical/conservative; actual neutral DLL compiles are stronger
boundary evidence, and actual central engine probes remain required.

PUBLIC_API_EDGES adds only the r5 compiler-evidenced RuntimeLoop dependencies
Modes/Combat/InventoryItems and CombatPublication dependency Combat. It is a
bounded explicit table, not general semantic inference. Unsupported platform IDs
fail closed; the five-platform validation domain is explicit. Dot components,
Windows reserved names, ADS, trailing dots/spaces and backslash aliases reject.
Inferred identities never absorb existing assemblies without directory/asmref
ownership. Editor-only conditional references may still conservatively block a
proposal; neutral diagnostic compilation does not clear that finding.

evidence_r6.py independently recompiles captured historical graphs and the exact
166-input replay, then diagnoses all current neutral nodes on pinned source bytes.
It may compile diagnostically despite graph findings, but never stages such a
graph. Its fresh output names are intentionally single-use.

Owned data contracts: ports.py. Main APIs: generate, validate, validate_asmdefs,
stage, proposal_plan, validate_plan, factory, ToolchainSnapshotCompiler.compile.
Trusted local coordinator tooling; no hostile filesystem sandbox is claimed.
Fresh independent review is required; builder cannot promote.

R7 authorization boundary: proposal_plan seals the original ordered preconditions
in staged/input-inventory.json. validate_plan always compares the supplied list
against that separate record, preserving historical metadata inventories exactly.
For coordinator authorization call validate_authorized_plan(plan, live_root,
staged, trusted_inventory_sha256) with the uppercase SHA256 retained outside the
editable staged plan. It rejects absent identities, deletion, inventory changes,
missing seals and byte drift. validate_plan without an external identity remains a
read-only diagnostic for a trusted staging directory; editing both that directory
and its unpinned inventory is outside that diagnostic's trust boundary. No apply,
filesystem immutability, locking or TOCTOU guarantee is implemented.

Paths reject ASCII and superscript 1/2/3 COM/LPT names, extensions and case in every
component; CONIN$/CONOUT$ are conservatively reserved too. This is a restricted
portable language, not an exact emulation of every Windows pathname API.
Malformed asmdef/asmref JSON and reference field types reject without crashing.

Separate compiler references now include the full transitive declared DLL graph,
so indirectly exposed signature types can load. This does not fabricate direct
asmdef references or perform semantic API inference. compile_r7.py records each
failure and dependency/cycle blocker, never falling back to one source assembly.
proposal_r7.py stages a ROOT-ONLY unreviewed central membership/friend-access
proposal with exact source hashes and absent-target/remove-if-hash rollback.
It preserves other boundaries and adds every concrete inferred asmdef so parent
membership cannot silently absorb descendants. New non-asmdef meta allocation,
independent review and central Unity import remain mandatory before any apply.
native_r7.py is compiler-only with real installed module/package/facade references,
UNITY_6000_0_OR_NEWER and C#5 warnings-as-errors. It executes no engine or output DLL.
Historical failed compiler setup attempts remain in distinct output trees.
