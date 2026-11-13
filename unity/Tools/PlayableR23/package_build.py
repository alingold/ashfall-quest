"""Package the verified build without Editor backup files; never launches it."""
import json
import pathlib
import zipfile

root = pathlib.Path(__file__).resolve().parents[3]
build = (root / json.loads((root / 'docs/agent-runs/20261104-three-chats/unity/playable-r23/current-build.json').read_text(encoding='utf-8-sig'))['build']).parent
output = build.with_suffix(".zip")
if output.exists():
    raise SystemExit("Preserve previous package")
instructions = """ASHFALL R23 Unity VR — provisional offline playtest

Extract the entire ZIP. Use Quest Link/Air Link with the Meta OpenXR runtime,
then run AshfallR23VR.exe. This is a PC VR build, not a standalone Quest APK.

Starts in the actual source skyline/deck lobby with its avatar pedestal.
Maps: Battlefield Paintball, ALFA and the original Sandbox Range. Eight original skins.
MAP SELECT chooses the map. READY opens LOBBY SPAWN / GAME SPAWN; choose one to enter.
Point the controller laser and release trigger to activate a menu button.
Left Menu / right B opens pause; HOME returns to the lobby. These new aim/menu
bindings still require a headset retest. Settings/catalog/characters use the lobby.
Left stick: move. Right stick: turn. Support-hand A/X: jump. Left stick click: sprint.
Either grip near a weapon: pick up in that hand. That hand's trigger fires.
Weapon-hand A/X: advance reload stage (eject, insert, charge). Or use opposite grip at actual
magazine/charging-handle contact points. Follow the live reload instruction.
Use menu RESTART to reset the local round.
Free hand grip at a wall: climb/release fling. Spread hands airborne: glide.
Free hand trigger: hold construction preview, release to place, A/X cancel.
Practice mannequins have actual health/shield, source damage and reset.
Settings includes automatic/manual standing height calibration. No world scaling.
MK18 common:18rounds, committed3round burst. AKM common:20rounds, automatic.
Separate persistent magazines, shared source rifle reserve2400; no refill onswitch.
Source head collision and ledge mantle are enabled. Hand finger poses remain static.
Actual ALFA pads launch the player. Shoot built cover to break it; unsupported panels collapse.
Lobby LOADOUT opens independent weapon inspection: grip to hold/fire/reload;
release to return it. Preview ammunition never changes your carried inventory.
Owned LOADOUT: point at an owned item; release grip to equip in that hand,
or release trigger to drop onto reachable clear ground. Weapon ammo is preserved.
Nearby source soda/shaker: free-hand grip to collect. Open soda with other grip,
then tilt to your headset; shield restores over ten active seconds. Shake the
shaker before drinking for instant shield. Banana: opposite-hand grip and pull
down at least10cm for each of four peels, then bring to the headset for real
health/stamina restoration. Three slots include weapons; drop a gun to free one.
SPR common:4rounds, semi-auto, separate sniper reserve2400, source2x scope.
The compact gameplay HUD is separate from menu controls.
Full modes, complete inventory, multiplayer and complete R23 parity are unfinished.

Verified: Unity PlayMode map floor/collision and horizontal movement,
analog grip pickup, firing/ammo debit, real mannequin hit, damage/death/restart,
menu release click, three skin loads/renders and podium, standing-height calibration,
actualALFA asynchronous load/nativefloor, native constructedwall/debit, gripclimb/pull,
upward releasefling and spreadhand glide through the actual sole root controller.
Exported XR-disabled player:120Start/restart floor cases passed, all3maps/entries,
nonzero HMDX/Z and two eyeheights;1116collision checks readable,0falls and no old
collision-cooking failures. Source lobby/menu/settings/HUD captured and compared.
Native tests also passed source head-hull/mantle, controller-heading across vertical
poses, real firearm-to-cover damage/destruction, support cascade and authored pads.
Five additional skin payloads were checked; their appearance still needs visual checking.
Windows OpenXR build passed. This corrected build has NOT been tested by a human.
No automatic headset/device launch. Full source UI/VR fidelity remains in progress.
"""
(build / "PLAYTEST.txt").write_text(instructions, encoding="utf-8")
with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
    for path in sorted(build.rglob("*")):
        if path.is_file() and not any("BackUpThisFolder" in part for part in path.parts):
            archive.write(path, path.relative_to(build))
with zipfile.ZipFile(output) as archive:
    names = set(archive.namelist())
    for required in ["AshfallR23VR.exe", "UnityPlayer.dll", "PLAYTEST.txt",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/World/map.json",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/World/geometry.bytes",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/Maps/range/map.json",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/Maps/range/geometry.bytes",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/Appearance/obsidian.meshbin",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/Construction/build_panel_user.png",
                     "AshfallR23VR_Data/StreamingAssets/PlayableR23/MenuAudio/source.json"]:
        if required not in names:
            raise SystemExit("Missing package member: " + required)
    if not any("UnityOpenXR" in name for name in names):
        raise SystemExit("OpenXR native plugin absent")
    bad = archive.testzip()
    if bad:
        raise SystemExit("ZIP checksum failed: " + bad)
print(json.dumps(dict(package=str(output), bytes=output.stat().st_size, files=len(names))))
