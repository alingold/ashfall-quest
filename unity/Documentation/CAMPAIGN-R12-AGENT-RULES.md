# Campaign r12: rules for every agent

The lead (Claude Opus, main session) owns integration: git, commits, Quest builds, the headset and the final merge.
The issue list with evidence is `Documentation/PLAYTEST-2026-11-15.md`.

## Shared checkout
- Every agent works in the same checkout: `~/ashfall/unity` (branch `playtest-r12`). Your shell may start in `/home/al`; `cd ~/ashfall/unity`.
- Do not run any git command that changes state: no commit, add, stash, checkout, reset, restore, rebase, merge or clean. Read-only git (`status`, `diff`, `log`, `show`, `blame`) is fine. The lead commits each workstream's files after review.
- **File ownership.** `~/ashfall/campaign/claims.txt` maps files to workstreams.
  - Edit only files claimed by your workstream, plus new files you create.
  - Before you edit an unclaimed existing file, append `<repo-relative path> <YOUR_WORKSTREAM>` to `claims.txt` (append only, never rewrite it), then edit.
  - If someone else claimed the file, do not edit it. Append a precise request (file, function, the exact change, and why) to `~/ashfall/campaign/requests/<OWNER>.md`.
  - Check `~/ashfall/campaign/requests/<YOUR_WORKSTREAM>.md` now and then. Do the requests addressed to you that are sound, and note each one done (or declined, with the reason) in that file.
- New files: name them with an `R26` prefix and your area (for example `R26RoomContinuity.cs`). Add them to `claims.txt`. Every new `.cs` file needs a `.meta` beside it with a fresh 32-hex GUID: two lines, `fileFormatVersion: 2` and `guid: <hex>`.
- Others are editing at the same time.
  - Keep the tree compiling: make changes in compile-safe steps and never leave your files broken for more than a few minutes.
  - If a compile fails only in a file you don't own, wait a minute and retry. Don't touch it. If it stays broken for more than 10 minutes, note it in `~/ashfall/campaign/requests/LEAD.md`.
- Keep diffs minimal and local to your issues. Put substantial new logic in a new `partial` class file or a new class rather than growing shared files.
- Match the local code style: dense, long lines, existing naming (R23/R24/R25 prefixes are history, not versions). Do not reformat code you don't change.
- **Line endings.** Most `.cs` files are CRLF. Preserve each file's existing line endings. If `git diff --stat` shows a whole file changed for a small edit, you broke the line endings; fix it immediately. In Python use `open(p, newline='')`.
- Do not create `.bak` files or timestamped copies inside the repo. Git is the backup.

## Verification
- **C# compile.** Takes about 6 s and needs no Unity license: `~/ashfall/tools/compile_check.sh ~/ashfall/unity --output <fresh dir under /tmp/r12-<you>-N>`. It must say `PASS`. All warnings in project code are errors.
- **CPU and LAN tests.** Run `~/ashfall/tools/cpu_checks.sh lan|modules|api|all --output <fresh dir>` (`lan` takes about 11 s). It uses the maintained suites in the untracked `~/ashfall/unity/Tools/` with C# 5 and warnings as errors, under Mono. Do not edit `Tools/`; describe the test changes you need in your report.
- **Scripted network player.** `Assets/Ashfall/Integration/Playable/R25NetBot.cs` supports these arguments:
  - `-ashfallNetBot host|join <ip>|join-code <code>`
  - `-ashfallNetBotRoutine default|soak|rejoin`
  - `-ashfallNetBotMap <id>`
  - `-ashfallNetBotLog <path>`
  - `-ashfallNetBotMinutes <n>`
  
  The Linux XR-off player build is `Ashfall.Editor.Playable.R26LinuxBuild.BuildLinux` (`-buildTarget Linux64 -ashfallLinuxBuild <new abs path>.x86_64`). The rig scripts will be in `~/ashfall/tools/mp-rig/` once the PERF workstream publishes them.
- **Unity editor.** It is at `~/Unity/Hub/Editor/6000.6.8f1/Editor/Unity` and is licensed. Only the PERF workstream and the lead may run the editor on `~/ashfall/unity`, and never two editors at once. If you need a runtime check, describe it in your report or in `requests/LEAD.md`.
- Old Windows docs mention an "engine/build hold" and `engine_lane.py`. That applies to the Windows machine only; ignore it here.
- Do not touch the Quest headset or adb. The lead runs device tests.

## Network protocol
- Host and clients always come from the same build, so protocol changes are allowed.
- If you change any datagram layout or add a message kind, say so explicitly in your report. Do not bump the lobby protocol version string; the lead bumps it once at integration.

## Report (your final message)
1. Root cause of each issue you own, with file:line evidence.
2. Every file you changed or created, and why.
3. How you verified it: compile result plus any runtime evidence, with paths.
4. What is still unverified, especially anything that needs two real headsets.
5. Risks: protocol changes, behaviour changes other workstreams should know about, requests you filed or received.

## r15 wave (2026-11-16 night): extra rules
- Issues: the "r14 full-lobby session" section at the end of `Documentation/PLAYTEST-2026-11-15.md`. HEAD at the start of the wave is the r14 commit plus docs. The branch is still `playtest-r12`.
- **The lead may be offline for hours.** Do not stop to ask questions; nobody will answer. Make the sensible call, note it under Risks in your report, and finish the whole job.
- **Write your final report to `~/ashfall/campaign/reports/<WS>.md`** as well as in your final message.
- **Runtime evidence.** You may build the Linux XR-off rig player and run NetBot peers locally (LAN host plus join on 127.0.0.1) for scripted evidence.
  - Build only through the shared lock, one build at a time: `flock ~/ashfall/campaign/rig-build.lock ~/ashfall/tools/mp-rig/build_player.sh --out ~/ashfall/builds/r15-<ws>-<n> [--source tree]`. The build reads the shared tree, so other agents' in-progress edits come along; if they break the build, wait and retry.
  - Keep runs short, put logs under `~/ashfall/mp-rig-runs/r15-<ws>-*`, and kill only processes you started, by PID (never `pkill -f`).
  - Still never run the Unity editor on `~/ashfall/unity` itself, and never touch the Quest or adb.
- **Godot reference.** The original Godot version is on the read-only Windows mount: `/media/al/Windows/Users/aling/Documents/ashfall/`. The newest full Godot project is `campaign-staging/20261104-three-chats/world/review-13-ui-maps-r1/full-candidate/` (scripts in `scripts/*.gd`). Never write there.
- Models: every r15 agent is Codex Astra. Keep tool output small and read files in parts; the campaign is long.

## r16 wave (2026-11-17 night): extra rules (these override the r15 ones where they differ)
- Issues: `Documentation/PLAYTEST-2026-11-17.md`. HEAD at the start of the wave is r15 (8b0c5f33) plus this doc commit. The branch is still `playtest-r12`.
- The r15 claims, requests and reports were archived (`campaign/claims-r15.txt`, `campaign/requests-r15/`, `campaign/reports/r15/`), so `claims.txt` starts empty. Earlier reports are useful background, especially for repeat failures: read `campaign/reports/r15/<WS>.md` for the area you inherit.
- Write your final report to `~/ashfall/campaign/reports/<WS>.md`.
- The lead may be offline for hours. Don't stop to ask; make the sensible call and record it under Risks.
- **Repeat failures.** FLY, PICKUP, REVIVE and the warm-up flow have each been "fixed" before and still failed in the headset. A desktop NetBot pass alone is not enough. Find the headset-only cause:
  - The author plays SEATED (tracked head about 1.06 m, origin lifted by height calibration).
  - Use the realism options `-ashfallNetBotStereo quest3 -ashfallNetBotSeated 1.06 -ashfallNetBotHeadSway 0.01 -ashfallNetBotTrace true` (see `~/ashfall/campaign/win2/` and `~/ashfall/campaign/videos/win2-README.txt`) where they apply.
  - Name the exact condition that made the r15 fix miss.
- **Rig limits on this PC.** Each graphical Linux peer uses about 2.4 GB of VRAM (RTX 5070 Ti, 16 GB) and about 1.2 GB of RAM. Several agents share the GPU.
  - Run at most 2 peers at once.
  - Before you launch, check `nvidia-smi --query-gpu=memory.used --format=csv,noheader`. If it is above 10000 MiB, wait and retry.
  - A peer that crashes in `GfxDeviceClient` or hangs with no `bot.jsonl` hit the VRAM limit; it is not your bug.
  - Builds still go through `flock ~/ashfall/campaign/rig-build.lock`, in `~/ashfall/builds/r16-<ws>-<n>`. Don't delete builds (see the deletion rule below). List your superseded build directories under "Builds to delete" in your report, and the lead deletes them. Each build is 5.6 GB, so rebuild only when you need to.
  - Kill only your own PIDs. Never `pkill -f`: it also matches your own shell.
- Models: Codex gpt-6.2-sol and gpt-6.2-astra. Keep tool output small and read big files in parts.

## r17 wave (2026-11-17 late night): the r16 rules apply, plus these
- Issues: `Documentation/PLAYTEST-2026-11-17b.md`. HEAD at the start is r16 (d5ebe5ef) plus this doc commit.
- The r16 claims, requests and reports are archived (`campaign/claims-r16.txt`, `campaign/requests-r16/`, `campaign/reports/r16/`), and `claims.txt` starts empty. Read `campaign/reports/r16/<WS>.md` and `campaign/reports/r15/<WS>.md` for the area you inherit.
- **Godot reference (corrected).** The full game source is `/media/al/Windows/Users/aling/Documents/ashfall/campaign-staging/20261104-three-chats/unity/Inventory/r3/inputs/quest-r22/source/scripts/` (243 scripts). The review-13 full-candidate named in the r15 rules has only part of the game (menus, scopes, sights). Read-only; never write there.
- Write your final report to `~/ashfall/campaign/reports/<WS>.md`.

## No deleting with `rm -r` or `rm -f` (2026-11-18, all waves)
- Never run `rm` with `-r`, `-f` or `-rf`, including inside a `trap ... EXIT` cleanup. Codex stops and waits for a human's approval on these commands even with full access. On the night of 2026-11-17, VEHICLEAIM, MUSIC and BULLETSPEED each sat on one of these prompts from about 23:50 until 08:33.
- For temporary files, make a fresh directory under `~/ashfall/campaign/tmp/<WS>/` and leave it there. Don't clean it up. The lead deletes it.
- To get rid of old builds or files, list their paths under "Builds to delete" in your report. The lead deletes them.

## r18 wave (2026-11-18 night): the r16 and r17 rules apply, plus these
- Issues: `Documentation/PLAYTEST-2026-11-18.md`. HEAD at the start is r17 (d042464a) plus this doc commit.
- The r17 claims, requests and reports are archived (`campaign/claims-r17.txt`, `campaign/requests-r17/`, `campaign/reports/r17/`). `claims.txt` starts with three pre-assigned files:
  - `R24PlayableMatchRules.cs` and `R24MatchRulesUi.cs` belong to MODES;
  - `R24WorldScoreboards.cs` belongs to MENUS.
  - Other workstreams send changes to those files as requests.
- Read `campaign/reports/r17/`, `r16/` and `r15/` for the area you inherit.
- Builds go in `~/ashfall/builds/r18-<ws>-<n>`. Never delete anything with `rm -r` or `rm -f` (see the section above). Temp files go in `~/ashfall/campaign/tmp/<WS>/`.
- Write your final report to `~/ashfall/campaign/reports/<WS>.md`.

## r19 wave (2026-11-18 late night): the r16-r18 rules apply, plus these
- Issues: `Documentation/PLAYTEST-2026-11-18b.md`. HEAD at the start is r18 (c7742d71) plus this doc commit.
- The r18 claims, requests and reports are archived (`campaign/claims-r18.txt`, `campaign/requests-r18/`, `campaign/reports/r18/`), and `claims.txt` starts empty. Read `campaign/reports/r18/` for the area you inherit.
- The maintained Tools suites now pass `cpu_checks.sh all` (TOOLS18). Keep them passing. Describe any Tools change you need in your report.
- Builds go in `~/ashfall/builds/r19-<ws>-<n>`. Never run `rm` with -r or -f. Write your final report to `~/ashfall/campaign/reports/<WS>.md`.
