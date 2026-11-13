# R21 feedback register — November2,2026

Base: completed R20 runtime4941c350b25bdd5835355a0ffca8090681ffb519/documentation056168cc4cf6743c1b91ea8c4fb951e91b287b9d. Fresh independent source/branch: C:/Users/aling/Documents/Codex/2026-11-02/ashfall-r21-interactions/source,quest3/r21-grip-pings-items. R20 rendering/loading policy is physically accepted and must remain stable. Earlier builds, active standalone source, Skyrim and original Gemini assets remain immutable; no VR launch/stop.

## New work

- [ ] GRIP-NORTH critical: on Quest, support-gripping any gun points it world-north and detaches aim from player/body turning/motion; offhand influence0/right dominant previously verified. Prior synthetic tests did not reproduce. Reproduce real XR coordinate/origin/late-update behavior and correct narrow shared path; preserve settings, damage/recoil/reloads and accepted scope hold.
- [ ] PING-ANCHOR: R19 moved only the lower icon to a camera-relative shell, leaving upper/column at intended hit point. Entire ping must stay anchored to the marked location. Constant apparent icon size must come from scaling or fixed-size rendering; no moved-world-point shell. Preserve tactical-map icons/owner lines, sticky type/privacy and large target stamp.
- [ ] ITEM-DISTANCE: reduce distant ground guns AND supplies/heals/build pickups rendering/loading. User suggests30m, then approximately3xzone-grenade diameter as a soft guide. Inspect existing zone size and choose/document a sensible short range, with useful approach behavior. Held/owned gear, actual authority entities, loot availability/respawn and interaction must remain correct. Avoid material transparency/fade cost.
- [ ] SIGNS: replace verbose NORTHLINE logistics paragraph with distinctive graphics/cool logos and very little text. Preserve exact source guards and functioning source-positioned scoreboards.
- [ ] ART-GAPS: pink portrait road banners and remaining semi-truck/rear white parts still incomplete; intersections partially accepted. Extend proven assembly families with original coherent art, collision/UV guards and native proof.
- [ ] TOWER-GRATES: side/cardinal central-tower grates still apparently unbreakable despite8recognized R18panels. Audit actual assemblies and collision/render membership, not only existing count; apply matching proven families across maps.
- [ ] MENU-STYLE: top pause menu with ASHFALL/RESUME/SETTINGS/INITIALIZE/HOME must visually match the good settings menu. Retain controls, layout readability and no unneeded instructional text.
- [ ] REVIVE-AUDIO: charging remains poor; inspect supplied video C:/Users/aling/Downloads/com.BigBoxVR.PopulationONE-20261023-234813-0.mp4 for genuine charge/ready/discharge reference and compare actual current routing/clip content. Preserve private-reference provenance/reuse restrictions.
- [ ] AWP-RELOAD: current reload contains shot echo/tail. User authorizes a clean reload from another gun; preserve AWP shot strength and all mechanics/other weapon sounds.
- [ ] DISTANCE-FIDELITY: distant edges still flicker. Explain actual public geometry LOD and texture mips; investigate aliasing conservatively without re-enabling unproven raw-surface postcut/clip-space-loading/default diagnostic paths or reducing whole-image resolution. Distinguish physical Quest acceptance from PC measurements.
- [ ] TRANSCRIPTION: user is switching local transcription to native ChatGPT and asks which is better. Current local text substitutes ASHFALL/Asheville and semi-trucks/some-instructs; fair engine ranking requires comparable audio/transcripts. Do not infer underlying local model or invent accuracy figures.

## Physically accepted by this report

R20 no longer crashes. Alpha landscape signs and scoreboards work. Some intersection repairs show progress. MellyPop lobby/game spawns are correct, wall-mounted weapons are nicely placed, and its scoreboard exists. Rendering looks decent overall. Preserve these findings separately from remaining all-map/hardware gaps. Previous health-pad and glide-cancellation acceptance remains inherited.

Implementation dispatch: actual T3 tasks for shared grip, art/fixtures, and menu/audio; primary owns ping anchors, ground-item rendering distance, integration/verification/release. Record actual task/model IDs and remaining limitations at handoff. All historical feedback remains linked through FEEDBACK-MASTER.md and PERSISTENT-BACKLOG-R20.md.

User steering: use60m for all ground gun/item visibility. No subagents or further T3 worker prompts. Earlier workers were cancelled/completed with queues held; primary performs all remaining work directly.
