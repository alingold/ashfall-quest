All115 actual outfits are now selectable through the unchanged AvailableStyles /
SelectStyle / InitializePreview APIs, stable SourceIndex0–114. This adds the
remaining73 R5 outfits and30 new frozen-R6 outfits to the prior12. Every entry
has its own actual source-derived manifest/mesh and material bindings; none
aliases another outfit as a stand-in. Root's115 source cards can bind directly.

R5 inputs use the prescribed d4fb…SOURCE-CONTENT manifest and exact R5 registry /
draft preimages at D:/ashfall-r6-staging/world/outfits-v001/r5-preimages. R6 uses
only D:/ashfall-r6-staging/frozen-source-r6-v001/source, manifest SHA
b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111,
content23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520.
R6 keys/labels/configs0–84 were checked equal to verified R5. Mutable quest-r24
inputs are accepted only when their bytes match R5; otherwise exact preimages
are required. Receipts retain every consumed path/hash and location.

All new textures cap at1024 with exact content deduplication. Forty-five older
custom kits genuinely lack LIGHT: the source-prescribed PCVR fallback is used
without decimation. The later R5 drafts and all thirty new R6 outfits use actual
LIGHT. New publication added524,252,280bytes including metas, while all360 files
and metas of the existing12 outfits remained byte-identical. Staging is bounded
to2GiB; actual extra art is about527MiB before cross-stage deduplication.

Concrete corrections: baked mapped glTF metallic/roughness factors (installed
URP17.6 ignores _Metallic when a map is enabled); original emissive-strength
extensions on newly converted outfits; Haze Nuclear's source specular factor on
body/gloves through an isolated URP shader; exact chest-follow-jacket transform;
the three Anna gallery leg frames. Outstanding requests are explicitly aborted
on replacement. The previous committed asset remains until the new one is ready.
InspectPresentation now includes actual committed SourceIndex. Current+pending
ownership remains bounded; no unbounded outfit/glove cache was introduced.

Checks passed: all73+30 source meshes, complete positions/normals/UV/indices,
material IDs/pixels/factors/extensions, all14 canonical parts, glove material
selection, source hashes,1024 caps, all115 live file/registry bindings,24 specular
energy cases and full installed Runtime/EditorRuntime/BuildAndSmoke compilation.
The custom shader source/math is checked; native shader compilation/rendering
is still unrun. Original12 material payloads deliberately remain exact, including
legacy emission metadata; eight older seed outfits retain28 textures at2048.
No complete rendering/lighting parity or headset performance claim is made.

Inspected genuine Godot Prowler LIGHT front/wrist captures in
campaign-staging/20261104-three-chats/world/morning-r5/r5/art-fixes-outfits-v001,
and fresh Unity floor-20261105-102203/unity-outfits.png plus the older appearance
menu. Fresh Unity shows the real Camo body/podium with the source-card grid.
Different styles, lobby lighting and VR-board desktop projection do not provide
a matched body comparison. No avatar scale/art was distorted to fit that probe.

Finite next capture request: Prowler, Stacy Throwback, Anna Tabby, Tox,
Haze Nuclear, Sir Box-a-lot and PJ Golden, using real BrowseAppearance/Preview
binding and receipt SourceIndex/ProfileId checks. For a genuine Prowler/Stacy
comparison, original executed-capture_outfits.gd uses960x960, FOV38, near.02,
front camera Godot(0,1.1,-3.7)→(0,1,0), wrist(0,1.05,-1.9)→(0,1,0),
head(0,1.65,-1.05)→(0,1.62,0). Reflect camera/target Z once for Unity in the
owned avatar's local frame. Source light: directional Euler(-35,-35,0), energy2;
fill(-2,2,-3),energy4,range8; background(.09,.105,.13),ambient(.78,.84,1),
energy.8,Filmic. Record URP/Godot lighting limits rather than claiming identical
HMD projection. Root owns admission/camera capture; no engine, GPU, Blender,
build or device command was run by this worker. e82/1615/history are untouched.
