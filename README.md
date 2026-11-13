# ASHFALL

The active Unity game is in [unity/](unity/PLAYABLE-R23.md), using Unity
6000.6.8f1. Start there for runtime entry points, authority boundaries, and the
engine/build workflow.

Run the maintained CPU and source checks from this directory:

```powershell
python unity/Tools/check.py all --output-root maintenance/checks-<unique-name>
```

[Test guide](unity/Tools/README.md) explains the checks and their limits.
Unity, builds, native peers and device operations remain under the existing
playtest hold. The shared `unity/Library` junction is an active dependency.

`game/` and `quest-r22/source/` / `quest-r24/source/` contain legacy Godot
sources and reference assets. They are separate from the Unity application;
some Unity conversion tools still consume their inputs. Keep those sources,
licenses and conversion inputs when retiring compiled releases or caches.

Historical evidence lives in `campaign-staging/` and `docs/agent-runs/`.
Current maintenance results and deletion inventories live in `maintenance/`.
Historical reports describe their recorded revision, not the current source.
