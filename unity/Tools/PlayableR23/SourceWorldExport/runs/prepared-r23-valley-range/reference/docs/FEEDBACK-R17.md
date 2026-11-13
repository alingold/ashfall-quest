# R17 — November 1 Alpha/Quest feedback

Base: completed R16 `d6f74d0aac75d6d3e7719b3b092fd40f8541aa72`. Isolated source/branch: `D:\Codex\ASHFALL-R17-Alpha\source`, `quest3/r17-alpha`. Preserve prior builds, original assets, accepted weapon handling and manual initialization. No tool launches/stops VR or changes a running game.

| ID | Requested behavior / observation | Acceptance |
| --- | --- | --- |
| R17-01 | Remove all TTS/narration, including “the storm … valley” on entry. | Remove synthesized story voice paths/assets from the shipped game; keep weapon/item sounds and player microphone chat. Implemented and checked offline; human Quest acceptance pending. |
| R17-02 | Vertical combat initialization seems lost between menu and game. | Exercise manual menu initialization and transitions; guard staging physics and restore calibrated origin lift while retaining tracked poses/crouch. Implemented and checked offline; human Quest acceptance pending. |
| R17-03 | R16 clarity improved, but Alpha toward the center still looks poor. | Reduce actual render workload more aggressively with distance detail/batching; preserve far map visibility and avoid disruptive sniping LOD changes. Compare native counters and keep headset acceptance separate. Implemented and checked offline; human Quest acceptance pending. |
| R17-04 | Gray/green octagons with yellow crosses still do not heal. | Actual initialized actor and ENet authority restore green HP at10/s in offline tests. Calibration protection and contact diagnostics added. Original headset symptom remains unconfirmed; HP-versus-shield question unanswered. Implemented and checked offline; human Quest acceptance pending. |
| R17-05 | Maximum walk speed should be5x. | Update personal/host sliders and authoritative limits to500%; retain defaults and other multipliers. Implemented and checked offline; human Quest acceptance pending. |
| R17-06 | Lobby/game spawn choices work on Alpha. | Preserve white lobby vs black-person match spawn behavior. |
| R17-07 | Million-ammo readout is oversized. | Show a dash for infinite reserve presentation; preserve finite magazine/reload and reserve mechanics. Implemented and checked offline; human Quest acceptance pending. |
| R17-08 | Glass breaking works; pad chains now work. | Preserve these fixes. Avoid geometry/collision changes that close openings or alter pad paths. |
| R17-09 | Remove the percentage beside the sprint icon. | Keep the stamina icon/bar and remove its redundant numeric text. Implemented and checked offline; human Quest acceptance pending. |
| R17-10 | Alpha's outer respawn barrier should be an invisible wall. Other maps such as Magnum retain recovery barriers. | Add solid Alpha perimeter handling and remove only its horizontal boundary respawn behavior; preserve other map/fall rules. Implemented and checked offline; human Quest acceptance pending. |
| R17-11 | Bare red weapon hexagons remain without functioning drops. | Recognize them as cache sites, spawn six actual random weapons, hide authoring symbols in hosted play and preserve useful creative markers. Implemented and checked offline; human Quest acceptance pending. |
| R17-12 | Alpha semi-trucks, some vans/cars, beige fences and road intersections lack textures. | Added hash-bound original vehicle/wood/junction detail for17 matching maps (Alpha:7truck cabs,5van bodies,32tire/axle pieces,530wood pieces,7junctions and162wood breakable assemblies). Source artwork preserved; some exported props still need art repair. Implemented and checked offline; human Quest acceptance pending. |

All older feedback remains in FEEDBACK-MASTER.md and the retained registers. Do not turn an offline check, successful installation or screenshot into a claim of human Quest acceptance.
