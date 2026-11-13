# Campaign working rules — R24

1. Base Godot work on `quest-r24/source`, snapshotted from the actual Opus R23 working tree. R22 is obsolete as an integration baseline. Preserve R23 and released APKs.
2. Deliver working changes. A task ends with changed source/assets, a focused check, and a merge decision. A proposal or review packet alone is unfinished work.
3. Merge useful, reversible improvements after a focused production check. Record remaining limits. Full-map coverage, perfect scores and three formal skin rounds are not prerequisites for a provisional build.
4. Do not merge a known serious regression, broken import, wrong baseline, or change that violates tracking/collision authority. Fix the concrete issue; do not start another certification chain.
5. One implementation owner per shared file. Stage disjoint asset work; WORLD integrates Godot, Skins produces models, Unity owns its runnable project.
6. Use enough workers for real independent deliverables, up to the user's allocated budget. Do not refill slots with audits or review padding. Integrate the ready queue before expanding it.
7. One focused review when it adds value. Fix small findings directly. No repeated reviews without a changed implementation or a concrete unanswered question.
8. One Blender/render worker and one engine/build/device job at a time. Preserve known-good files and use bounded jobs. No unsolicited headset launch.
9. Report outcomes: models built, visible defects fixed, features merged, builds run. Agent counts, checksums and document counts are not progress measures.
10. Keep one short decision list: merge, adapt, or park with a specific reason. Preserve raw evidence in files; do not rewrite historical reports or generate multiple manifests for routine edits.

Latest user instructions take precedence. Existing technical guides are references for the relevant task, not mandatory ceremony before every edit. Be explicit about what was actually tested.
