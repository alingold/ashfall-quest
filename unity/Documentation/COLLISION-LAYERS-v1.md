# Central Unity collision allocation v1

Reserved layer indices for the migration: 8 WorldStructure, 9 WorldInteraction, 10 PlayerActor, 11 WorldDecoration. Coordinator alone applies TagManager/physics project settings; adapters validate this allocation rather than silently assuming default layer 0. This reserves names/indices, not a claim that project settings or collision matrix have been imported.

Structural floors/walls/ceilings retain physical collision and legitimate interaction occlusion. Intended interactions resolve to stable semantic owners, require range/line-of-sight and use authored trigger volumes. Decorative foliage may omit collision; real structures cannot be blanket-disabled. Queries exclude the requesting actor by explicit actor identity and never treat all actors/structures as decoration. Unknown semantic classification or unbound masks reject gameplay activation.

Pilot exports remain quarantined and cannot enter the production Build API without a versioned immutable approval receipt. Trigger meshes must not be automatically convexified into invented gameplay volumes; require an approved authored collider representation or fail with a concrete unsupported record. Keep source geometry available for further conversion.
