# Source-convex collision candidate r4 (unreviewed)

`SourceHeadConvex.Instance` represents the convex hull of the 168 samples in
`player._make_goggle_head_shape`: seven rings, elevation ring*pi/12, 24 segments,
12 cm lateral radius and 12 cm authored eye-to-top height. The bottom is the eye
plane. Z is reflected once. Duplicate pole samples are preserved as source data;
the consuming convex hull backend may weld them without altering the hull.
`CopyVertices` returns a defensive copy; `Support` queries that finite hull.

`ConvexClimbCollision(IConvexClimbScene, IConvexHeadSource, CollisionLayers, float floorNormalY)`
implements the existing `IClimbCollision` consumed by `ClimbController` and
`MantlePlanner`. `Solve` also exposes availability, completion, displacement and
cast count. Use `MantleTolerance` (.001 m) for mantle/preflight and
`SupportTolerance` (.015 m) for source support completion; the support caller still
owns support-hit/rise/drop eligibility and final root resolution.

The central backend must bind the actual locomotion collision shape and actor
exclusion. `_sync_head_shape` moves the shape center to the camera position but
does not copy camera rotation. `ConvexHeadPose` therefore carries the collider's
world basis rotation and an already transformed world offset from the head,
never HMD pitch as an implicit collider rotation. Rotations must be converted by
basis conjugation when transferring from Godot. No code here writes a tracked
pose, collider, locomotion root, mask, scene, or project setting.

Backend obligations: exact convex continuous cast, true-penetration query that
distinguishes touching contact margins, actor-hierarchy exclusion, explicit masks,
trigger exclusion and fail-closed saturation/unavailability. `Revision` changes
whenever geometry, identity or filtering changes. Read the live pose on each
solve; changes during the solve invalidate it. `Recovery` permits numerical
separating contact resolution only; every retry must still test the entire
remaining motion against all solids. It never licenses skipping all upward
normals, increasing an overlap tolerance or teleporting through a floor.

`_resolve_support` and `_step_mantle` allow one initial cast plus four recovery
casts, only for positive-Y motion against a walkable floor normal, stopping when
the rest is below .0001 m. Preflight runs the same collision semantics without
scene mutation. Walls/ceilings and initial true penetration remain blocking. A
later sweep rechecks live geometry after preflight and returns safe partial
motion; the existing controller owns cancellation and root displacement.

The old `ClimbCollisionAdapter` / `UnityClimbCollision` remains an experimental
box backend. Its bounded retry fix does not approve the box or establish parity.
No Unity convex adapter was invented: the exact Unity6000.3.27f2 convex sweep,
penetration/contact-margin behavior and actor/mask binding require centrally
serialized API/engine evidence. Define-OFF compilation is neutral evidence only.
