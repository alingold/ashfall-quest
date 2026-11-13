# ASHFALL R7 isolated source

- 105 existing completed archive maps added to MAP SELECT, alongside Relay Valley, Sandbox Range and Cyber Bird:108 entries over27 pages, with thumbnails and creator names. Pick a card, then READY. The adjacent private ASHFALL-Local-Maps.pck is mounted automatically; keep the builds folder intact.

- Rarity glow is now world-pickup-only. Local and remote held weapons keep their normal materials; dropped weapons keep rarity colors/stars. Rarity levels, damage, recoil and capacities are unchanged.

- Isolated branch standalone/r7-library-ground-glow starts from completed 1004 checkpoint3d60cb0dac987a685e700d2511759b14e310f1b4. R6 runtime features were deliberately ported using a three-way content merge against the shared1.0.0.0 source. Latest1004 session nonce/serial/replay fences and finite reload ownership preserved and checked.

- Includes inherited Cyber Bird144-building city,384 extra real weapon drops,64 rideable/climbable cars, held map, surface zones, online bullet holes, compact host/voice controls and Argon skins. Initialization remains manual; settings/calibration seed copied unchanged from this user’s R6 profile into separate R7 userdata.

- Original105-map scenes/materials/dependencies were copied and packed byte-for-byte from the completed1004 local cache. Raw maps are local-use data with existing creator credits; no map archive was uploaded or published. The code/source checkpoint excludes the ignored local map cache. Original1004 and priorR6 source/builds remain untouched.

Godot4.7.2 Windows Desktop export. Runtime original assets are included; the private map PCK is separate from the code and game export. For source-mode map access, add --local-maps-pack=<absolute path to the supplied R7 builds/ASHFALL-Local-Maps.pck> after --. Never write into the original owner cache. See ../ASHFALL-R7-Evidence for selector, READY/unload, finite reload, replay-fence, native and peer receipts.
