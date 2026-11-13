# Matched appearance capture ? staged only

Ready for Root to copy **only** `R24MatchedAppearanceCapture.cs` and its meta to `Assets/Ashfall/Editor/Playable/`; `merge-map.json` gives exact paths. No Assets, existing smoke/composite,115 outfit payloads, history or source files changed. No native execution or new screenshot is claimed.

Entry after an admitted XR-off startup is ready in the **actual lobby**, with UI on Ready or Appearance:

```csharp
yield return R24MatchedAppearanceCapture.Run(loop, freshDedicatedOutputDirectory, note);
```

No automatic startup/CLI route or Finish call is added; Root owns that wrapper. The output directory must be new or empty so captures/history cannot be overwritten. The fixture requires already-live115 real styles, uses `UI.OpenAppearance` / actual `BrowseAppearance` callback / `Preview.InspectPresentation`, and requires committed ProfileId,RequestedId,SourceIndex,visible head/body and actual glove meshes. It never directly substitutes an avatar or calls `Preview.Show` as a fallback for a broken production route.

Seven source styles: **Prowler81, Stacy Throwback73, Anna Tabby25, Tox42, Haze Nuclear36, Sir Box-a-lot60, PJ Golden103**. Six genuine source counterparts exist here for the first two: LIGHT/after front,wrists,head in `campaign-staging/20261104-three-chats/world/morning-r5/r5/art-fixes-outfits-v001/`. The executed script SHA is `abb06da6c542a3e04ac8f0baf497ec988028b5eb28eef1fc0ea0e6df2459f448`, matching that native receipt; result.json reports all four style/tier after rows passed. No before,PCVR or comparison montage is mislabeled as the LIGHT source counterpart.

`source-recipe.json` records every source path,SHA and byte size; `source-pins.json` is the compact pin list. Six PNGs, executed script, original result/receipt and frozen-R6 manifest/registry/gallery/body/glove scripts are pinned (14 files). Runtime refuses changed pins before touching the scene. Source images are referenced **in place**, not copied or regenerated. R6 manifest SHA remains b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111.

The11 planned outputs are six `matched-camera-{prowler,stacy_throwback}-{front,wrists,head}.png` plus five `probe-source-{id}-front.png`. The latter have **no matched counterpart**, explicitly carry source-labelled-probe classification, and never acquire an invented source image. Every image is960?960, verticalFOV38, near.02, source camera/target Z reflected once in the **existing actual avatar frame**:

| View | Godot camera | Godot target |
|---|---|---|
| front | (0,1.1,-3.7) | (0,1,0) |
| wrists | (0,1.05,-1.9) | (0,1,0) |
| head | (0,1.65,-1.05) | (0,1.62,0) |

The avatar/mesh/material/hand transforms are not reshaped for framing. RotationMultiplier is temporarily0 to stop the production turntable; real Browse still performs its normal entry-facing action. Layers isolate only the committed body and existing preview gloves, excluding the menu,podium,world and player. Before/after transform,mesh and material-identity checks guard each render. There is no HMD projection/comfort claim.

Actual source numeric environment recipe: directional Godot Euler(-35,-35,0),energy2; fill(-2,2,-3),energy4,range8; background(.09,.105,.13),ambient(.78,.84,1),energy.8,FILMIC exposure1/white1. Unity source rotation is reflected once to Ry(+35)Rx(+35), relative to the same avatar frame. Existing lights, ambient colors/mode/probe/intensity, reflection intensity,skybox,fog,sun and Filmic session are saved/restored. The fixture preserves the current input-domain qualification: it **does not qualify Gamma radiance or enable pipeline HDR** merely to claim Filmic success. Each receipt records actual color space,qualification,Filmic status and recorded-pass delta. Source numeric light energy is not a proved URP/Godot radiometric equivalence; material response,attenuation,environment/output domain remain qualified comparison limits.

The real production neutral preview hand meshes/materials/relaxed transforms are preserved. The older executed Godot images use its then-current two26-joint glove assembly. Differences in neutral wrist/knuckle/bone presentation must be compared as actual differences; this fixture does not rotate fingers or replace gloves to match a picture. Source R5 counterparts vs live frozen-R6 inputs are explicitly recorded; no whole-render or hand-pose parity is claimed.

Capture loops bound at150s total,20s/style and five settled frames/view; original committed preview restoration has a separate20s bound. A failed capture still restores layers/lights/environment/cameras/session and the actual prior browse/committed style route. An outer finally also restores flags/resources and queues the prior production browse on coroutine disposal; disposal cannot await its async commit; input pumps/rig enable/turntable/page/preview visibility restore. Tracking/root matrices are never written and are checked unchanged. No gameplay Start,world/inventory/Health mutation or persistent settings change occurs. Native receipt `matched-appearance.json` is emitted only by actual execution with per-image hashes and committed IDs; successful completion requires all11 captures.

Checks run: `python .../MatchedCapture/check.py` PASS14 source pins/six exact960 PNGs/source-script camera literals/seven stable IDs,66 reflected projection cases and reflected directional basis; installed Unity6000.6.8f1 full BuildAndSmoke API compile with warnings-as-errors PASS. Native rendering, tone, cleanup under interruption and image comparison remain **UNRUN**. Root may integrate the two staged files and route a future admitted capture; this worker has no engine admission.
