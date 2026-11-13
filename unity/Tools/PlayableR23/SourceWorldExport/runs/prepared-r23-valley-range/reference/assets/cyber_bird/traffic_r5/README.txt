CYBER BIRD — original avian traffic assets, R5

Selected raven taxi upgrade:
raven_taxi_argon.glb is the completed Gemini Argon variant, generated through
the authenticated antigravity / gemini-4-argon-high provider in a fresh isolated
T3 chat. 4,836 triangles, 21 merged mesh parts; 1.844m wide including details,
1.658m high, 4.7556m long. Four wheel pivots retain the same names; radius .34m.
Its sculpted fenders, canopy framing, raven nose, feather panels and rear wing
replace the simple fallback taxi's appearance. Original fallback stays below.
Generator: generate_raven_taxi_argon.py (Python standard library only).
Credits: ARGON-LICENSE.txt, ARGON-PROVENANCE.json, ARGON-INTEGRATION.json.
The adopted mesh passed 48,483 independent buffer/geometry checks plus a native
Godot import/render inspection. Generator sidecars are prefixed ARGON so they
do not overwrite fallback credits; emissive strength normalization by Codex
retains the intended cyan and amber hue. Geometry remains Argon's final output.
No original Gemini or other game assets were edited; the T3 task is completed.

raven_taxi.glb: plum/obsidian sedan, cyan shoulder trim, amber feather badges,
winged taxi roof beacon, swept tail fins. 1,920 triangles.
heron_van.glb: ivory delivery body, dark canopy, cyan heron/feather side markings,
industrial roof runners and cooling fins. 2,120 triangles.
kestrel_compact.glb: amber hatchback, magenta feather trim and canopy rails,
small raised rear wing. 1,868 triangles.

All three use metres, +Y up, forward -Z, model origin at ground plane Y=0.
Outer widths including mirrors, spokes, and neon stay within a 1.9m lane.
The four independent wheel pivots are Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR;
animate local rotation.x. Each wheel contains Rubber, Metal_Chassis, Paint_Ivory
merged surfaces. The body is merged by material, 20 mesh parts total per car.
All 11 materials are small opaque PBR materials, named consistently across cars
for runtime pooling. No textures, transparent surfaces, embedded lights, bones,
or external dependencies. Opaque dark windshields are decorative, not interiors.

These are visual traffic assets. The integrator owns road following, collision,
deterministic multiplayer placement, culling, and maximum active population.
Do not use one collider per visual mesh. Suggested bounds are in PROVENANCE.json.

Rebuild with Python 3: python generate_cars.py
The script needs only the standard library and regenerates the three GLBs,
PROVENANCE.json, and LICENSE.txt in its own asset directory deterministically.
Original generation script and original geometry are CC0-1.0; see LICENSE.txt.

Provider status: these three fallback models were authored locally before T3
orchestration access was verified. A later isolated bridge successfully sent
a prompt to Gemini 4 Argon (High) through Antigravity and read its response.
These particular models remain original Codex procedural work, not a Gemini
output. No provider keys were printed, no service was installed or restarted,
and no existing Gemini assets were changed. Original Argon player assets remain
credited separately by the parent project.
