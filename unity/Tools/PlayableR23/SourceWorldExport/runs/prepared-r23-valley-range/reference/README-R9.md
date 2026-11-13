# ASHFALL R9 isolated source

Base: completed R8 `354322f34adbf37ca7725007bd19b8926faf8725`. Branch: `standalone/r9-playtest-corrections`. Godot 4.7.2 Windows Desktop.

- One world-fixed podium with the selected full-size Witch Doctor slightly right of the world-fixed menu. Slow automatic rotation; four creator-labeled new variants and existing styles retained.

- Current physical muzzle pose drives projectiles and reticles, including visible aim damping. AWP scope stays engaged through the bolt cycle while support grip remains held. Accepted damage, recoil, reload and inventory parameters are preserved.

- All-map hollow-wall hand proxy and climb contact fix. Revive meter renders over world geometry; charging/facing and ready cues are one-shot with per-episode lifecycle. Accepted reference audio PCM is unchanged from restored R8.

- Blades, knives, frag and zone grenades have fixed form and no rarity stars; firearms keep five levels. Held weapons remain unlit by rarity glow.

- All 105 imported maps rebuilt through the shared gameplay adapter. Recognized door leaf/frame pieces become one breakable assembly, with no residual collision; recognized pane crosspieces disappear with the pane. Walking through or shooting a passage breaks it with sound and synchronized state. Bespoke geometry without recognizable source semantics remains a documented coverage limit.

- Source jump-pad material recognition applies throughout the library. Phoenix and Classic each receive twelve explicitly new orange octagonal pads with arrows, standable floors and overhead clearance; original pad locations were not reliably recoverable. Crane footings are not launch triggers. Real source weapon cache markers produce six actual random weapons, with bounded activation; source coordinates are retained.

- Removed the R8 floating scoreboards. Melly Pop original central megatron has four live scoreboard faces; its two WEAPONS boards have one of every active weapon below them. The flattened archives lack gameplay object tags, so unknown scoreboard/weapon-board surfaces cannot honestly be declared restored across all maps.

- Poppy separate high lobby floor is solid and distinct from validated arena spawns. Pending breakable collision is included in spawn validation. Chaos Magnum maps enforce Magnum-only loadouts/drops and rescue falls twenty metres below the platform baseline above the center platform.

- Whole-map far planes for main and scoped cameras, translucent shared hex forcefields, retained mipmaps/filtering. Removed redundant warmup and menu instruction text.

- Additional reported mixed scenery reproduced directly from raw source geometry without ASHFALL Main or previous maps. Original source overlap is retained rather than destructively guessing which architecture belongs. All prior source/builds and original donor assets remain unchanged.

Private original map PCK and matching R9 adapter PCK are required beside the executable. Source play: pass `--local-maps-pack=<absolute original PCK>` after `--`; the R9 adapter pack mounts alongside it. Both source fingerprints and prepared-resource hashes are checked; old adapter fallback is disabled. Prepared meshes stay outside Git. Rebuild with tests/bake_adapters_r9.gd using the private cache, archive_semantics_r9.json, and a private output directory; index/hash all 105 .res files and pack them under local_archive_r9/. Shared resolver changes apply to every map. Source-specific geometry bindings are explicit and hash-bound.

See FEEDBACK-REGISTER-R9.md, PORTING-MANIFEST-R9.json and sibling evidence for validation scopes. No headset or real Internet/microphone acceptance is claimed.

Rebuild tools are included in tools/r9/: index_adapters.cjs <private-prepared-directory> <file-list.json>, followed by pack_adapters.gd with the file list and destination PCK as its last two user arguments. Use --force-r9 when changing the resolver, so cached resources cannot hide algorithm edits. The recipe does not edit the original archive.

Source audit: all 105 original GLBs lack gameplay object names and node/mesh/scene/material extras. Seventy-nine map.json files retain rule/settings/event summaries, but no usable object surface/position bindings. The complete read-only audit is retained with the candidate. All 271 WAV assets are byte-identical to accepted restored R8.
