# ASHFALL

A multiplayer VR shooter for Meta Quest, made in Unity 6.

**Want to play?** [PLAY.md](PLAY.md) has the download link and step-by-step install. It's one download and about 10 minutes of setup. Current version: **r19** (1.3.18-playtest-r19).

## Source

- `unity/` is the Unity 6000.6.8f1 project. Start with [unity/PLAYABLE-R23.md](unity/PLAYABLE-R23.md) for runtime entry points, authority boundaries and the build workflow. [unity/Documentation/ADDING-MAPS.md](unity/Documentation/ADDING-MAPS.md) covers adding maps.
- Files of 10 MB or more (map geometry and similar) are on Google Drive. [LARGE-FILES.md](LARGE-FILES.md) lists them and explains how to fetch them.
- CPU and source checks: `python unity/Tools/check.py all --output-root maintenance/checks-<unique-name>`. The [test guide](unity/Tools/README.md) explains the checks and their limits.
- `docs/` holds older planning notes.

## License

- Code: MIT ([LICENSE](LICENSE)).
- ASHFALL's own art and assets: CC0 1.0 ([unity/LICENSE-ASSETS.md](unity/LICENSE-ASSETS.md)).
- Third-party content keeps its own license and credit. This covers the community maps, the Godot XR Tools hands and the audio packs; see [unity/THIRD-PARTY-NOTICES.md](unity/THIRD-PARTY-NOTICES.md) and [unity/CREDITS-ASSETS.md](unity/CREDITS-ASSETS.md).
