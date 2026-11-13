# Source UI font

Actual R23 has no project default-font override. Godot 4.7.2 embeds
`thirdparty/fonts/OpenSans_SemiBold.woff2` as its default runtime UI font
([source](https://github.com/godotengine/godot/blob/4.7.2-stable/scene/theme/default_theme.cpp#L1240)).
Unity previously used its unrelated `LegacyRuntime.ttf`.

The original WOFF2 and SIL Open Font License are retained here. `prepare.py`
decodes it with fontTools 4.60.1/Brotli 1.1.0 into the Unity resource
`R23GodotUI.ttf`. All 1,150 glyph outlines, horizontal metrics and character
mappings are identical after decoding. Source and output hashes are in
`source.json`; the script refuses different input/output bytes. Local Python
dependencies are under `deps` and do not enter Unity's Assets/build.

The UI now requests this resource. Unity import, atlas rendering and visual
comparison are still pending the next coordinated engine slot. The currently
running 222943 executable remains unchanged.
