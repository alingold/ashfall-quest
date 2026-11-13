# Character modeling workflow for future Sol models

## Purpose and quality priorities

Follow the procedure used by Claude Opus 5.5 to make the ASHFALL Witch Doctor, then apply the completion and validation safeguards recorded here. The user's accepted quality priorities are:

1. **Connected-looking elbows and other joints throughout posing.** A good torso does not compensate for disconnected sleeves.
2. **Carefully validated attachments.** Straps, cuffs, collars, mask pieces, equipment and decorative details must look mounted, sewn, tucked or intentionally suspended. Nothing should float accidentally.
3. **Enough actual geometry.** Preserve rounded silhouettes, shaped cloth, thickness and convincing joint volume. Do not replace the accepted character with a much lower-poly approximation merely to finish faster.

Treat these as release gates, not optional polish. Match the quality of the accepted Opus character before calling a new character complete. Extra polygons alone do not fix placement or articulation.

## Evidence and attribution

This guide was reconstructed from the original thread's Opus messages, its successive Blender scripts, reference contact sheets, and its Blender/Godot preview process. Historical thread: `2519297c-f3cd-461d-a1d6-c9230c5255a9`. Opus's main implementation is in `work/witch-doctor-r7/tools/`:

- `wd_geo.py`: lofts, curved tubes, surface frames, buckles, thickness and detail controls.
- `wd_parts.py`: shaped garment surfaces, surface-mounted accessories, rounded limbs and cuffs.
- `wd_mats.py`: layered procedural leather, felt, wool, brass, glass and emission.
- `kitpose.py`: exact lobby transforms and a desktop pose approximation.
- `build_witch_doctor.py`: model iteration, three geometry tiers, UVs, bakes and exports.
- `bake_glove.py`: glove detail derived from mesh positions, normals and real finger bones.
- `wd_render.py`, `render_kit.py`, and `godot-preview/capture.gd`: visual inspection and export round trips.

Opus authored the design, modeled the geometry, iterated against the clip, established the three-tier approach, authored the procedural materials and glove feature treatment, and introduced explicit white coverage baking. The later Sol completion fixed a thickness-generated geometry spike, export/atlas robustness, both hand UV regions, packed scene delivery and the second-glove material assignment. Those later repairs are distinguished below; do not attribute every final script line to Opus.

The completed reference kit has **62,042 PCVR triangles** and **22,444 lightweight triangles**, excluding the separately supplied hand meshes. Its source geometry is approximately **182,000 triangles** and does not ship. These are useful quality anchors for this character, not universal budgets for every platform or character.

## 1. Study the reference and the runtime before modeling

1. Extract frames across the full rotation, make contact sheets, and crop the character large enough to inspect its silhouette and equipment. Keep front, three-quarter, side and back views. Do not infer the whole design from one attractive frame.
2. Write down the defining silhouette and its secondary shapes. For this character: flat broad hat, forward beak and red lenses, mask/hood/neck relationship, shoulder capelet, belted long coat, fitted sleeves, boots, bandolier and medical backpack.
3. Inspect the existing character in the engine and read its assembly and pose code. Opus read the existing kit, `player_body.gd`, `lobby_map_091.gd`, the kit loader and the hand adapter before finalizing the model.
4. Record mesh names, local origins, axes, scale conventions, joint endpoints, eye height and glove anchors. Confirm them by importing and rendering the existing GLB.
5. Build a Blender pose utility that reproduces the runtime assembly. A model that only looks correct as one assembled sculpture can fail when its pieces are posed independently.

ASHFALL's established contract is **13 body meshes plus `AvatarHead`**. Limb meshes are unit segments stretched and rotated between real joints. Belt, boots and chest parts use their own metre-based origins. The head is authored in standing-avatar coordinates. Godot/glTF uses Y up and front −Z; these Blender tools use Z up and front +Y. Keep the conversion explicit: `(x, y, z)_Godot -> (x, -z, y)_Blender`.

**Gate:** Before creating final detail, assemble the old and new kits with the actual runtime transforms and prove that mesh names, orientation, height, boot placement and wrist locations agree.

## 2. Model a continuous-looking garment, with joints designed first

Opus modeled the limbs in **real metres first**, then normalized them into the engine's unit-segment space. This keeps a 22 mm strap, a 3 mm buckle and a 1.6 mm seam at sensible physical sizes despite different limb diameters and lengths.

For each limb:

1. Define several radius/height profile samples instead of using an unshaped cylinder.
2. Loft sufficiently dense rings along the limb. Retain extra rings where the outline rounds into an elbow, cuff, shoulder or knee.
3. Shape the ends with multiple taper/rounding rings and close the shell. Do not end both adjacent sleeves with two exposed flat rims exactly at the same joint plane.
4. Extend the hidden ends modestly beyond their nominal segment endpoints so adjacent volumes overlap when the segments rotate.
5. Put cloth folds around the joint transition and use similar materials across the connection. Avoid a large hard material boundary that makes two joined volumes look severed.
6. Normalize the finished geometry by diameter and nominal length only after the real-size cloth, straps and hardware have been built.

Concrete Opus implementation to reuse:

- `limb_surface()` evaluates a physical radius profile plus small cloth displacement.
- `limb_mesh()` lofts rings and caps the ends.
- `sleeve_fold()` adds bounded folds concentrated near the elbow region.
- `to_unit()` applies inverse diameter/length after modeling.
- Upper arms extend from about **−0.53 to +0.53** of their unit length; forearms extend from about **−0.53 to +0.515**. The nominal endpoints are −0.5/+0.5. This deliberately provides hidden overlap.
- The nominal upper arm is 0.105 m in diameter and 0.29 m long; the forearm is 0.085 m in diameter and 0.27 m long. These are this kit's authoring dimensions; the engine supplies the final joint-to-joint length.
- The upper arm and forearm have multiple end rings, not a single end ring plus a distant pointed cap.
- Forearm cuffs have an outer surface, inward return rings and lining. A cuff must have real thickness and an interior, and its opening must meet the glove.

The meshes are separately posed pieces; this is **visual continuity through overlapping volumes**, not a single welded skeletal elbow. Do not falsely claim they form one watertight deforming mesh.

### Required elbow validation

Inspect both arms from front, side, back and below in at least: lobby pose, desktop aiming pose, a relaxed pose, a near-right-angle bend, a deep bend and a reachable straight-arm pose. Use the engine's actual joint frames for final acceptance. Inspect both the outer elbow and the inner crease.

- No daylight crack between sleeves in the accepted poses.
- No detached cap, exposed hollow end or sudden radius step.
- No severe bulge through the opposite sleeve during a deep bend.
- Seams and straps follow the sleeve instead of remaining at an unrelated transform.
- Cuffs meet the hands at the actual wrist bone, including left/right differences.
- Existing first-person eye-clearance behavior remains intact.

If an elbow fails, fix profile radii, rounding, overlap or the joint frame. Do not conceal the failure by only rendering a flattering camera angle. Recheck all accepted poses after the correction.

## 3. Anchor every attachment to its supporting surface

Opus used reusable surface evaluators such as `torso_point()`, `torso_front_point()`, `cape_pt()`, `skirt_pt()` and limb surface functions. Accessories were placed from the actual garment surface and its normal, with small physical offsets.

Follow that approach:

1. Build the supporting garment first, including folds and its final shape.
2. Evaluate the attachment path on that surface. Add a measured offset for thickness, not an arbitrary world-space translation.
3. Orient buckles, buttons and cylinders in a local frame built from the surface normal and the strap direction.
4. Give straps thickness and understandable ends: beneath a flap, into a keeper, through a buckle or sewn onto cloth.
5. Mount equipment on racks, belts, straps or case walls. A vial can be separate geometry, but it must have a visible holder.
6. Keep piping and stitches following the same garment evaluator so later silhouette edits do not leave them behind.
7. Use a parallel-transport frame for curved tubes to avoid random twists in piping, straps and seams.

Opus repeatedly corrected placement after renders: removed a floating head strap, re-anchored the bib to the lower face, added hood drape, tightened flared cuffs, adjusted buckle axes and boot details, and checked the in-engine relationship between the forward head and torso. This iteration is part of the method; the first successful render is not completion.

### Required attachment review

Make a support list for visible assemblies: mask -> head/hood; hood -> neck/collar; collar/cape -> torso; straps -> garment; buckle -> strap; backpack -> shoulder/belt supports; vial -> holder; cuff -> wrist; shin -> boot.

For each, inspect close-ups from several sides and at least one runtime pose. State how it is supported. Reject unexplained gaps, an accessory visible from below with no mounting, and coincident surfaces that flicker. Distinguish intentional clearance inside a rack from an accidental floating strap.

Do not use “one Blender object” as proof that nothing floats: joined meshes may contain many disconnected islands. Conversely, separate mesh islands are valid for buttons, lenses and hardware when they visibly contact a support. Use visual review plus measured nearest-surface checks where useful; an AABB overlap alone does not prove contact.

## 4. Preserve a generous, purposeful geometry budget

Build the silhouette and articulation with enough geometry before spending time on textures. Prioritize round elbows, shoulder and knee transitions, hat brim/crown, mask/beak curvature, boot toe/sole, cloth folds and garment thickness.

Opus's first detailed v2 meshes reached approximately 123k PCVR / 44.5k lightweight triangles. It then introduced three separately generated tiers rather than accepting blanket decimation:

| Tier | Implementation | Purpose |
| --- | --- | --- |
| Source | `Q=1.25`, detailed hardware and stitched geometry | High-quality baking and editable source; never shipped |
| PCVR | `Q=0.8`, silhouette and substantial structural detail retained | Accepted full character; 62,042 triangles |
| Lightweight | `Q=0.38`, lower profile/curve sampling with minimum segment counts | Same design and connected joints; 22,444 triangles |

The shared `S(n, minimum)` function scales tessellation while retaining minimum ring counts. Detail switches omit tiny seam beads and hardware where the bake can reproduce them. The structural cloth shape and joint closure remain modeled.

**Do not silently reduce the PCVR character to the older low-poly candidates' budgets.** Keep the accepted density unless profiling establishes a requirement to lower it. If optimization is necessary, compare silhouette and elbow close-ups before and after, record actual triangles per part, and preserve the higher-quality source. Normal maps cannot restore a faceted silhouette or close a physical gap.

## 5. Iterate geometry in a fast model stage

Keep `model` and `full` stages separate. The model stage should rebuild quickly with procedural preview materials and render:

- Front, three-quarter, exact side and back full-body views.
- Close-ups of head/neck, both elbows, cuffs, knees/boots and equipment mounts.
- Both runtime geometry tiers, not only the detailed source.

Use a neutral floor, readable key/fill/rim lighting and a repeatable camera. Dark leather must still be visible. Keep the entire hat and soles in frame for full-body reviews. Save the images, inspect them, list concrete defects, fix the geometry and render again.

Opus made multiple such rounds before baking: mask width and bib shape, hood drape, coat length and flare, cuff fit, leather roughness, piping appearance and detail density. It toned thread down when large stitch beads read as ropes/zippers. Retain that willingness to revise.

**Gate:** Do not run the long bake until silhouette, connections, attachment support and tier geometry have passed this review.

## 6. Use layered materials and bake from the detailed source

Opus's materials combine base-color variation, larger-scale wear, leather grain/scuffs, cavity darkening, appropriate roughness, stitched thread, upward/low-surface ash, polished/tarnished brass, lens gradients and glowing vial liquid. Use distinct roughness for felt, wool, leather, brass and glass; do not rely on color alone.

The source exposes explicit base color, roughness, metal, emission and AO outputs for emission-based baking, plus a BSDF for the tangent normal pass. Per-vertex attributes place seam dashes and lens gradients consistently.

For the accepted pipeline:

1. Generate independent UV atlases for PCVR and lightweight meshes.
2. Equalize texel scale, then weight important areas (head 1.8, chest 1.35, jacket/boots 1.15 by area in this kit).
3. Bake **each runtime tier from the full-detail source**. Do not assume a lightweight mesh can use a different topology's UV atlas.
4. Bake at 4096, then coverage-weight downsample to 2048 and pad island edges.
5. Use an explicit white-emission UV coverage bake. Bake alpha is not a trustworthy coverage mask.
6. Pack ORM as R=AO, G=roughness, B=metallic. Treat normals/ORM as non-color; albedo/emission use color management.
7. Embed all runtime body textures into each GLB and pack both texture sets into the editable Blender scene.

Do not spend the entire budget on texture decoration while leaving elbow connections or mounts unfinished.

## 7. Validate the actual exported assets in the actual runtime

Opus round-tripped the exported GLB through Blender, then built a Godot harness using the game's real `lobby_map_091`, `player_body` and kit loader. It checked a lineup, head/turntable close-ups, a runtime desktop pose and a first-person downward view. Continue with that workflow.

Required checks:

- Import the exported GLB again and render it; a procedural source preview is not sufficient.
- Verify exactly the required mesh names, finite coordinates and canonical transforms. Godot suffixes such as `.001` can break a name-based kit loader.
- Compare counts and bounds against the source report and preserve the intended triangle budget.
- Verify albedo, normals, ORM, emission, color space and texture dimensions after engine import.
- Reassemble with real engine transforms. Check profile, elbows, cuffs, boots, head/neck and backpack supports again.
- Check both hands separately. The existing hands use different UV regions; seeing a good left glove does not prove the right glove is textured.
- Exercise open/closed finger poses on both existing rigs and confirm the material reaches both lobby gloves, even if Godot renames a duplicate sibling.
- Check the selected character locally, in a lobby/avatar preview and through the remote-head/body path. Preserve tracking, wrist alignment and camera collision authority.
- Keep old/new profile captures when changing head position; translate the render mesh, not the tracked camera or collision shape.
- Import and export an executable after integration, then check the packaged runtime. Do not call a loose drop-in folder “integrated into the game.”

Record what was actually inspected. Source checks, simulated poses, native desktop rendering, loopback networking and headset acceptance are different evidence. Do not claim headset validation without a headset test.

## 8. Completion safeguards learned while finishing this kit

These include later Sol fixes and should remain in future implementations:

- **Thickness spikes:** even-offset Solidify on near-reversing torn-hem normals produced a vertex roughly 25 m away. Use bounded/normal thickness, inspect bounds before UVs, and reject outlier vertices. The spike had squeezed the useful PCVR atlas to about 2% coverage; corrected coverage was about 46%.
- **Degenerate geometry:** remove zero-area faces and collapsed edges before UV equalization. Inspect unusually large islands rather than only changing pack margins.
- **Atlas compositing:** use explicit per-object coverage and compositing for shared target images; validate the combined mask. Do not infer success from a file being written.
- **Absolute file paths:** normalize paths before Blender load/save operations. Confirm the expected output files changed, rather than reading an old export from another directory.
- **LOD name collisions:** park the posed originals' names while exporting canonical copies. Preserve exact body labels in both LODs.
- **Both glove regions:** bake both hands and merge their UV coverage; handle the small intentionally shared regions and renormalize averaged normals.
- **Material assignment:** recognize both glove instances by a stable interface/identity, not only a sibling name that Godot may auto-rename.
- **Real editable scene:** save the posed high/detail/low collections, camera, lights and packed textures in a usable `.blend`, not just an unlinked library of datablocks.
- **Meaningful process failures:** use Blender `--python-exit-code 1`, inspect exit codes, and distinguish stderr deprecation warnings from actual exceptions.

## Final acceptance checklist for a Sol character

- [ ] Reference rotation and current game character studied.
- [ ] Runtime piece origins, axes and transforms documented and reproduced.
- [ ] Both elbows look connected in all agreed poses and camera directions.
- [ ] Shoulders, cuffs, knees, boots and head/neck transitions checked.
- [ ] Every visible accessory has a believable support; no accidental floating pieces.
- [ ] Silhouette and joints retain the accepted level of tessellation.
- [ ] Source, PCVR and lightweight triangle counts recorded separately.
- [ ] Model-stage defects fixed before long bakes.
- [ ] Independent LOD UVs/bakes, reliable coverage and padded maps verified.
- [ ] Both hands textured and articulated correctly.
- [ ] Export round trip and real-engine material/pose checks passed.
- [ ] Integrated executable checked, with evidence saved.
- [ ] Editable source, scripts, provenance, screenshots and a concise handoff delivered.

If any defining quality gate is unchecked, continue work or state the remaining limitation explicitly. Do not declare the character complete simply because it loads.
