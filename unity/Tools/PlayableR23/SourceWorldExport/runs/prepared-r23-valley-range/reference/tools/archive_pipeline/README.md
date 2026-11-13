# Original local archive pipeline snapshot

These are original acquisition/preparation/validation tools, not archive code or assets. This snapshot matches the owner workspace layout: `<work-root>/candidate/tools/archive_pipeline`, with research/evidence folders beside candidate. Run from the preserved owner workspace, or keep that layout when reproducing.

The public index/metadata, downloaded GLBs/textures/thumbnails, intermediate geometry and converted scenes remain outside Git in ignored `local_archive/`. They are excluded from ASHFALL licensing and exports. No redistribution grant is established. Do not publish or upload those bytes.

`inventory1003.py` measures current source headers/metadata and free space. `download1003.py` uses two polite workers, exact index thumbnail URLs, hash/length receipts, resume files and deduplicated shared textures. `prepare1003.py` expands integer/normalized GLB attributes and strips material loading. `matrix1003.py N` resumes through absolute index N with one bounded Godot child, memory/disk guards and per-map actual-game receipts; a work-root `STOP-MATRIX` file checkpoints after the active map. Use small batches. `repair_thumbnails1003.py` fetches only index-specified previews.

The Godot converter and fixtures are in the parent tools/tests directories. Imported cache data never runs scripts. The matrix source hash covers the converter, map adapter and geometry/lifecycle fixture. Common player grip-query and paged-menu integrity changes have separate focused fixtures. Physical VR/Internet acceptance remains separate.
