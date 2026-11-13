# ASHFALL Unity

The active game is `unity/`, using Unity **6000.6.8f1**. Runtime composition starts
at `Assets/Ashfall/Integration/Playable/R23PlayableLoop.cs`; native LAN ownership
starts at `Integration/Playable/Networking/R24LanSession.cs`. R23/R24 names identify
source history; they are parts of this project, not alternate installed games.

## Development

- Runtime and editor code: `Assets/Ashfall/Integration/Playable` and
  `Assets/Ashfall/Editor/Playable`.
- Rules and authority owners: `Assets/Ashfall/Modules`; shared contracts:
  `Core` and `RecoveryContracts`.
- CPU checks: [Tools/README.md](Tools/README.md).
- LAN behavior and storage: [networking guide](Tools/PlayableR23/Networking/README.md).
- Boundary contracts: [Documentation/README.md](Documentation/README.md).

From the repository root, run all maintained CPU/source checks with a fresh result
directory outside `unity/`:

```powershell
python unity/Tools/check.py all --output-root maintenance/checks-20261109
```

The runner compiles and executes CPU tests and compiles native API references. It
does not launch Unity, create peers, build an APK or contact a headset. Each run
keeps its logs and storage fault fixtures together in the requested directory.

## Engine and build workflow

The engine/build/device/live-playtest hold remains in force until explicitly
released. `Library` is a junction to the sole shared cache: do not delete it or open
its old probe project concurrently. Engine work requires the existing shared
`engine_lane.py` workflow and current coordination/lock checks; a successful source
compile does not grant an engine slot.

After release, Windows build entry is
`Ashfall.Editor.Playable.R23PlayableBuild.BuildWindows`, with
`-ashfallVRBuild <new absolute exe path>`. Focused editor smoke entry is
`Ashfall.Editor.Playable.R23SmokeControl.Run`, with
`-ashfallVRSmoke <new result.json>`. Both require the shared lane. PC VR uses OpenXR.

The preserved Quest playtest artifact is
`Builds/UnityQuest-20261108-video-fixes-r7/UnityQuest-20261107-180358/AshfallUnityQuest.apk`.
Its installation and headset behavior are separate from later source changes.

Historical results remain under `campaign-staging/` and `docs/agent-runs/`; they
record the tested revision and do not describe the current source automatically.
