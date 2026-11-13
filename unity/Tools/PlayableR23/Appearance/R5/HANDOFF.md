Four genuine R5 gold outfits published and Appearance ownership released.

| Stable source ID | Runtime ID | Actual LIGHT triangles |
| ---: | --- | ---: |
| 53 | polymer_gold | 51,896 |
| 74 | hattie_golden | 30,012 |
| 75 | roy_golden | 27,900 |
| 76 | anna_golden | 38,704 |

`R23PlayerAppearance.AvailableStyles` now returns twelve real converted styles:
the unchanged eight seeds plus these four. Existing initialization, selection,
preview, receipt and glove events are unchanged. Root's `SourceIndex` UI binding
selects their actual R5 cards. No other UI/preview/root files were edited.
There are73 remaining unconverted registry entries; their cards must remain
unavailable. No fallback points those entries at another outfit's body.

The prescribed SOURCE-CONTENT SHA
`d4fb1053acfbaa7f3eb285879472bfdd136e09bf2f7e1f2dc6478f23d8f12bc7`
and contentID
`da1db74984ea1b82a8b60b7cb8baae9d31dc5483f387fdcf105f3dd0a308895c`
were verified first. Registry and draft JSON match the exact prescribed hashes.
Every consumed GLB, external image/buffer and XRTools wrist input is verified
against that manifest before conversion; hashes are in `receipt.json`. All four
actual LIGHT files exist, so none needed the source missing-LIGHT→PCVR fallback.
The bounded CLI recipe implements that fallback for future requested EXTRA_KITS.

New live payload is28,799,096bytes before small metas:40 shared content-hash PNGs
plus four meshbins/manifests, under64MiB. Exact source material factors and roles
are retained. Texture dimensions are capped at1024; glTF metallic/roughness/ORM
channels are repacked into the existing URP owner. Polymer glove material is the
actual LForearm surface material; the other three use their own source-authored
glove maps. All14 body/head pieces and every primitive are present. Meshes are
not decimated or substituted. Z, winding and UV V each reflect exactly once.
All264 preexisting seed files/metas were hash-checked byte-identical at publish.

`python unity/Tools/PlayableR23/Appearance/R5/check.py` passed exact source-vs-output
positions/normals/UV/indices, primitive/material IDs, complete parts, distinct
body hashes, stable labels/IDs, source hashes and texture/payload bounds.
Installed Runtime/EditorRuntime/BuildAndSmoke API compilation also passed.
Original source/e82/released builds remain untouched. Native preview/import,
URP appearance, headset/device and performance checks are unrun by this worker.

The existing owner retains at most current+pending assets per instance, disposing
the previous committed asset on replacement. It has no idle kit/glove cache;
this does not claim the original global2-kit/3-glove LRU reuse policy. Disk texture
deduplication is exact; separate player/preview instances retain their existing
independent runtime texture ownership. The source texture cap is implemented for
these new outputs; the eight seed payload bytes are deliberately preserved.

Future conversion is isolated by default:
`python unity/Tools/PlayableR23/Appearance/R5/prepare.py --ids <actual R5 EXTRA_KITS keys>`
writes only `StagedPayload`, refuses changes to existing outputs and checks the
cumulative64MiB budget before each new write. Only explicitly converted, checked
kits should be added to the selectable runtime catalog. `receipt.json` describes
the current four-gold conversion; the complete85 registry is not an art-coverage
claim. No Blender, engine, GPU, device or build command was run.
