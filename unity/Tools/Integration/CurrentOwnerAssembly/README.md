# Current owner assembly diagnostics R9

Compiler-only tooling; no engine, apply, GUID allocation, default assembly merge,
friend grants or project/package edits. All artifacts stay in the assigned roots.

Use the fixed bundled Python with `-B`. `current_owner.py freeze NAME` captures
actual source, tools, metadata, policies, package metadata, compiler and installed
reference identities once. `declare.py NAME` runs exactly once over that snapshot
to add the explicit root RecoveryContracts unit omitted by legacy BuildGraph and
classify guarded Integration sources. Preserve the resulting `seal.json` values
externally. The legacy plan/proposal/findings remain for comparison.

`current_owner.py validate NAME --capture EXPECTED --plan EXPECTED --output OUT`
authenticates bytes and exact source/fixture membership before compiling separate
DLLs. Every project/package compiler reference must be explicitly enumerated in
`plan.json`. Public-signature transitive project edges are conservatively promoted
into the plan, not appended invisibly by the compiler. BCL and Unity framework
references are distinct from package references. CPU does not prove guard-ON APIs.

`gates.py NAME CAPTURE PLAN OUT` compiles real owner DLLs, public external boundary
consumers, a decoder mutant, five installed package positive/omission pairs and
membership omission cases. `extra_gates.py NAME CAPTURE PLAN` exercises a compiled
unknown-owner-field mutant, the actual OpenXR feature and public inheritance
Management dependency. Native/API artifacts are never executed. Neutral CLR
fixtures alone execute. `summarize.py NAME` emits source symbols, package edges,
cycles and rooted failures from `validation02`, plus every unexecuted tool fixture.

`current_owner.py drift NAME` records one later observation, never updates the
frozen inputs. No waiting, polling, sibling communication or accepted graph claim.
The proposal is reviewable data with identities and inverse dispositions; root
must reconcile missing metadata, boundaries, failures and independent review.
There is no application/rollback implementation or TOCTOU protection.

Preserved setup failures: capture01 encountered a Windows long traversal path;
capture02 encountered a long snapshot destination. Neither reached owner-source
capture. capture03 is the sole completed source capture. Initial gates omitted
System.dll under /noconfig and failed compilation; gates02 explicitly supplies
BCL references. Initial validation's Boundary fixture had that same harness error;
validation02 passes it. Historical outputs remain untouched.
