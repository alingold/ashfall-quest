# Third-party notices

The MIT license covers ASHFALL code. The CC0 dedication covers the authors'
original art and assets. Third-party components retain the terms below.

## Code, fonts and engine dependencies

| Component | Terms and retained notices |
| --- | --- |
| Godot XR Tools | MIT, Bastiaan Olij and contributors. The hand models and textures are separately offered under CC0 1.0 by DigitalN8m4r3 (Miodrag Sejic). [MIT text](Assets/StreamingAssets/PlayableR23/HandsWeapons/LICENSE.txt), [asset attribution](Assets/StreamingAssets/PlayableR23/HandsWeapons/ATTRIBUTION.txt), [upstream hands](https://github.com/GodotVR/godot-xr-tools/tree/master/addons/godot-xr-tools/hands). |
| Godot Engine filmic tone mapping | MIT, Godot Engine contributors, Juan Linietsky and Ariel Manzur. [Adapted shader](Assets/Ashfall/Integration/Playable/Rendering/Resources/R23GodotFilmic.shader), [MIT text](Assets/Ashfall/Integration/Playable/Rendering/Resources/GODOT-LICENSE.txt). |
| Open Sans SemiBold | SIL Open Font License 1.1, The Open Sans Project Authors. [R23GodotUI notice](Assets/Ashfall/Integration/Playable/UI/Resources/R23GodotUI-LICENSE.txt), [R23InventoryUI notice](Assets/Ashfall/Integration/Playable/UI/Resources/R23InventoryUI-LICENSE.txt). |
| Unity URP Lit shader excerpts | Unity Companion License. Unity Technologies notices appear in the production double-sided appearance shader and related editor comparison shaders. [Source notice](Assets/Ashfall/Integration/Playable/Appearance/Resources/R23AppearanceDoubleSided.shader), [license text](https://github.com/Unity-Technologies/Graphics/blob/master/LICENSE.md). |

The Unity Editor and built-in engine modules are covered by the
[Unity Editor terms](https://unity.com/legal/editor-terms-of-service). Package
Manager resolves the complete dependency set from [manifest.json](Packages/manifest.json)
and [packages-lock.json](Packages/packages-lock.json). Keep each package's
license and third-party notices with its upstream distribution. This includes
the OpenXR runtime binaries and license notices delivered by the OpenXR
package, and the upstream notices for NUnit, Mono.Cecil and Newtonsoft.Json
delivered by the corresponding Unity packages.

## Game assets

| Component | Terms and retained notices |
| --- | --- |
| Traversal and climbing audio | Apache-2.0. [License](Assets/StreamingAssets/PlayableR23/TraversalAudio/LICENSE.txt), [traversal attribution](Assets/StreamingAssets/PlayableR23/TraversalAudio/ATTRIBUTION.txt), [climb attribution](Assets/StreamingAssets/PlayableR23/TraversalAudio/CLIMB-ATTRIBUTION.txt). |
| Construction audio | The shipped ASHFALL procedural cues carry an earlier Apache-2.0 notice. The original work is also covered by the authors' CC0 dedication. [Retained license](Assets/StreamingAssets/PlayableR23/Construction/Audio/LICENSE), [cue attribution](Assets/StreamingAssets/PlayableR23/Construction/Audio/audio_reference099_ATTRIBUTION.txt). |
| Weapon and equipment models | See [CREDITS.md](CREDITS.md) and the per-model `*-ATTRIBUTION.txt` files in [HandsWeapons](Assets/StreamingAssets/PlayableR23/HandsWeapons) for creator, origin, applicable terms such as CC BY 4.0, and modifications. |
| Kestrel source model | Irondust's Sci-fi Soldier, CC0 1.0. [Source, changes and terms](Assets/StreamingAssets/PlayableR23/Bots/kestrel-PROVENANCE.txt). |
| CC0 source recordings and photo scans | Source recordings from the Free Firearm Sound Library, OpenGameArt and Freesound, and texture scans from Poly Haven, retain their upstream CC0 dedications. [Creator and source credits](CREDITS-ASSETS.md). |
| Community and official Population: ONE maps | The original creators and other rights holders retain their rights. The source archive is publicly viewable. Individual creator names and source URLs are recorded in `Assets/StreamingAssets/PlayableR23/Maps/*/SOURCE-LICENSE.txt`, `Assets/StreamingAssets/PlayableR23/Legacy/Maps/*/SOURCE-LICENSE.txt` and the [Battlefield Paintball notice](Assets/StreamingAssets/PlayableR23/World/SOURCE-LICENSE.txt). [Public archive](https://maps.deliciouswines.org/). |
| Legacy assets and character recreations | Existing source terms apply to copied Legacy material and the PBAN, OBSIDIAN and WITCH DOCTOR recreations. [Credits](CREDITS-ASSETS.md), [appearance attribution](Assets/StreamingAssets/PlayableR23/Appearance/ATTRIBUTION.txt). |

## Resolved Unity packages

The lockfile enumerates the built-in `com.unity.modules.*` entries under the
Unity Editor terms. The table covers the other bundled and registry packages.

| Package | Version | License in resolved distribution |
| --- | --- | --- |
| `com.unity.burst` | 2.0.0 | Unity Editor bundled package. The local package has no standalone license file. |
| `com.unity.collections` | 6.6.0 | Unity Companion License |
| `com.unity.ext.nunit` | 2.1.0 | Unity terms identified in `LICENSE.md`, plus `Third Party Notices.md` |
| `com.unity.graph-authoring` | 1.0.0 | Unity Editor bundled package. The local package has no standalone license file. |
| `com.unity.inputsystem` | 1.20.1 | Unity Companion License |
| `com.unity.mathematics` | 1.4.0 | Unity Companion License |
| `com.unity.nuget.mono-cecil` | 1.11.6 | Unity Companion License, plus `Third Party Notices.md` |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | Unity Companion License, plus `Third Party Notices.md` |
| `com.unity.profiling.core` | 1.0.3 | Unity Companion License |
| `com.unity.render-pipelines.core` | 17.6.0 | Unity Companion License, plus `THIRD PARTY NOTICES.md` |
| `com.unity.render-pipelines.universal` | 17.6.0 | Unity Companion License, plus `Third Party Notices.md` |
| `com.unity.render-pipelines.universal-config` | 17.6.0 | Unity Companion License |
| `com.unity.searcher` | 4.9.5 | Unity Companion License |
| `com.unity.services.authentication` | 3.8.0 | Unity terms identified in `LICENSE.md`, plus `Third Party Notices.md` |
| `com.unity.services.core` | 1.18.0 | Unity terms identified in `LICENSE.md` |
| `com.unity.services.deployment` | 1.7.2 | Unity terms identified in `LICENSE.md`, plus `Third Party Notices.md` |
| `com.unity.services.deployment.api` | 1.1.3 | Unity terms identified in `LICENSE.md` |
| `com.unity.services.multiplayer` | 2.3.3 | Unity terms identified in `LICENSE.md`, plus `Third Party Notices.md` |
| `com.unity.services.qos` | 1.5.1 | Unity terms identified in `LICENSE.md` |
| `com.unity.services.wire` | 1.6.1 | Unity terms identified in `LICENSE.md`, plus `Third Party Notices.md` |
| `com.unity.shadergraph` | 17.6.0 | Unity Companion License |
| `com.unity.test-framework` | 1.8.0 | Unity Companion License |
| `com.unity.test-framework.performance` | 6.6.0 | Unity Companion License, plus `Third Party Notices.md` |
| `com.unity.transport` | 6.6.0 | Unity Companion License |
| `com.unity.ugui` | 2.6.0 | Unity Companion License |
| `com.unity.xr.core-utils` | 2.6.0 | Unity Companion License |
| `com.unity.xr.legacyinputhelpers` | 3.0.1 | Unity Companion License, plus `Third Party Notices.md` |
| `com.unity.xr.management` | 4.7.0 | Unity Companion License |
| `com.unity.xr.openxr` | 1.18.0 | Unity Companion License for package source, Unity Package Distribution License otherwise, plus `Third Party Notices.md` |
