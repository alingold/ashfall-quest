# Unity foundation contract v1 — 20261104-0020

Status: frozen initial engineering boundary; no runtime/device acceptance. Lead U alone owns shared contracts, bootstrap, ProjectSettings, Packages and meta GUID allocation. Do not select packages in a leaf. Source hashes: ../../docs/agent-runs/20261104-0020/lead-u/frozen-inputs.json (workspace-relative ledger).

## Transfer and identity

All distance is SI meters, time seconds, angles radians in neutral data. Godot right-handed +Y up / -Z forward transfers to Unity +Y up / +Z forward by reflecting Z. Point/direction: (x,y,-z); rotations must use basis conjugation S R S rather than blindly negate Euler angles. Triangle winding reverses exactly once; normals transform with inverse transpose. Preserve hierarchy and nonuniform transforms. No global double scale conversion. Map IDs, style IDs, weapon/item keys remain original strings, case-sensitive; display labels are not IDs. No fabricated GUID lock entries. Asset records require source absolute path, SHA256, source tree/revision, export version, material slots, collider role and licensing/approval state. Unapproved files are references, not distributable content.

## Player/input and authority

Root is locomotion/capsule authority; tracked head and hand poses are presentation/input children and cannot move collision independently. Tracking loss disarms interactions; pause releases gameplay actions while preserving tracking. Actions: Move(Vector2), Turn(Vector2), Jump, Sprint, GripLeft/Right, TriggerLeft/Right, Reload, Menu, Ping. Desktop adapter and XR adapter feed the same frame with monotonic sequence/tick; adapters do not author damage. Height, speed, comfort and handedness are settings, preserving source values through an explicit versioned migration. Do not substitute generic movement for accepted climb/glide/mantle rules.

## Rules and network

Simulation receives tick, actor ID and command ID. Host/server validates damage, pickups, building, revive and pad launches; client intent is never an authoritative state mutation. Commands are idempotent within a bounded session window, rejected across session/authority epochs. Interaction requires line of sight to the intended semantic owner, range, layers and valid alive state; do not globally bypass occlusion. Pure data/core contains no UnityEngine dependency. Interfaces use immutable requests/results, explicit rejection and finite SI vectors. First protocol is ASHFALL-UNITY/1; reject incompatible major/schema/content versions in handshake before spawn. Godot interoperability is unsupported until an actual bridge is implemented/tested. Networking package choice remains pending official verification; local simulation is not network proof.

## Data and persistence

Map schema 1 includes map ID, source hash, transformed spawn poses with mode/team policy, indoor/capsule policy, pads with orientation and stable IDs, semantic fixtures and referenced assets. Keep archives mounted/read-only, no bulk map copy. Save/config schema 1 preserves original keys and unknown values in migration sidecars; never overwrite Godot saves. Writes use temporary file plus atomic replacement and maintain prior backup; malformed/newer schemas fail explicitly. Migration coverage remains open until actual source keys are reconciled.

## Assemblies, ownership and gates

Core: Assets/Ashfall/Core (Lead U), no engine dependency. Runtime: Assets/Ashfall/Runtime (Lead U bootstrap). Toolchain leaf: Tools/ToolchainProbe only plus its toolchain evidence. Inventory leaf: campaign-staging/20261104/unity-import/inventory and lead-u/inventory only. Future Player/World/Combat leaves require fresh named allocations and this contract hash. Windows/Quest rendering/input/OpenXR versions will be selected from official evidence after toolchain investigation; no invented lock or unverified manifest.

M0 inventory/architecture/toolchain gate stays open until reconciled. M1 requires runnable desktop AND Quest slice evidence prescribed by campaign; neutral tests do not qualify. M2 systems, M3 content and M4 replacement remain open. Preserve every baseline/release.
