R8 stages root-only assembly/package proposals; it never applies live files.

Use the bundled Python with `-B`. Fresh output names are direct children of
`campaign-staging/20261104-three-chats/unity/BuildGraphR8`.

    python -B unity/Tools/BuildGraph/package_graph_r8.py fresh-name
    python -B unity/Tools/BuildGraph/verify_r8.py fresh-name
    python -B unity/Tools/BuildGraph/audit_r8.py fresh-name

An optional second argument to package_graph_r8.py names an existing R8 input
capture to replay. It copies small pinned source/metadata only, never packages,
assets, caches or new live source bytes. The submitted result is `final06`,
replayed from `candidate02`. Candidate01 hit Windows long paths; extended-path
metadata reads repair that. Candidate02/03/04 compile runs were stopped when
replaced; their partial diagnostics remain. Final05's framework glob included
Editor modules in player flags and duplicated the Editor aggregate facade.
Final06 corrects both allowlists and audits every player compiler invocation.
`verification-final` under final06 is authoritative.

Package identities and actual GUID/name references come from installed metadata;
version checks are explicit. Unresolved references elsewhere in package cache
(including samples/tests and built-in assemblies) remain catalog diagnostics.
They do not become guessed package GUIDs. Actual compiler failures remain failures.

`consumed` evaluates conditional source per CPU/Windows/Android/Editor/test
profiles, then identifies bounded namespace dependencies. This is lexical
inference, not a complete C# semantic model. Native compilation is stronger
evidence. Define/platform membership is recorded; test membership is not a test
runner result. Known project public dependency closure is explicitly promoted
into asmdef references so the compiler never supplies undeclared project edges.
Every such promotion is listed for central review; it can over-reference.

Compiler references contain only explicit proposal project/package references
and system/built-in Unity framework DLLs permitted by noEngineReferences and the
profile. Package asmdef closure is indexed for identity review, never injected
into compiler flags. Directive-free successful neutral CPU DLLs may be reused;
reused-neutral.json records that exact artifact and hash. It claims no fresh
native compile. Every actual compile retains sources, response file, command,
diagnostic and reference hashes. No Unity stub, engine or generated DLL execution.

OpenXRFeature does not require XR Management in the actual negative gate.
OpenXRLoader does require that public base dependency. The Player proposal
therefore declares InputSystem/OpenXR; mapping supports Management where truly
used. Collections uses CollectionHelper in the package-only negative gate:
NativeArray is supplied by Unity core, while NativeList is unsupported by C#5.

`preflight` authenticates both whole-plan and inventory bytes using externally
retained SHA256 values; verifies target-before, payload and inverse bytes; and
rejects source/metadata drift and links. It is read-only, with no lock, apply,
rollback execution or TOCTOU guarantee. Input and payload identities must be
retained outside writable staging. Physical additions and missing meta are
separate blockers. New metadata/GUID allocation remains central work.

The R7 explicit CombatPublication asmref and narrow named friend-assembly policy
are retained as root-only proposals. Friend grants expose all internals to those
assemblies and need policy review. Checkpoint layering cycles, inaccessible APIs,
active Session writer omissions, current drift, import/runtime/device and M1
gates are not repaired by expanded compiler flags or by this proposal.
