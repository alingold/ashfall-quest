"""Run this one released Unity slot, refusing GPU queuing and enforcing its end."""
import datetime
import json
import pathlib
import queue
import subprocess
import sys
import threading
import time
import argparse
sys.dont_write_bytecode = True
from slot_disk_guard import SlotDiskGuard

root = pathlib.Path(__file__).resolve().parents[3]
arguments=argparse.ArgumentParser();arguments.add_argument('--release-until',required=True);arguments.add_argument('--floor-only',action='store_true');arguments.add_argument('--player-floor',action='store_true');arguments.add_argument('--source-ui',action='store_true');arguments.add_argument('--build-only',action='store_true')
arguments.add_argument('--motion-only',action='store_true')
arguments.add_argument('--valley-export',action='store_true')
arguments.add_argument('--valley-plan',type=pathlib.Path)
arguments.add_argument('--valley-only',action='store_true')
arguments.add_argument('--feedback-only',action='store_true')
arguments.add_argument('--arsenal-only',action='store_true')
arguments.add_argument('--r6-gameplay',action='store_true',help='Finite actual R6 UI and knife contact scene checks; XR disabled')
arguments.add_argument('--appearance-only',action='store_true',help='Finite source-camera outfit capture; XR disabled')
arguments.add_argument('--lan-spawn-health-range',action='store_true',help='Only remaining actual Root host Range guest readiness/placement/raw/HOME; XR disabled')
arguments.add_argument('--lan-spawn-health',action='store_true',help='Finite Root host Range/Alpha and client Range spawn-Health lifecycle; XR disabled')
arguments.add_argument('--video-feedback',choices=['solids','entry','lobby-hud'])
arguments.add_argument('--lower-grilles',action='store_true',help='Actual nine-map lower source grille break/reset and paired apertures; XR disabled')
arguments.add_argument('--family-passages',action='store_true',help='Actual seven-map complete source passage subsets; XR disabled')
arguments.add_argument('--tower-family',action='store_true',help='Actual seven-map tower break/reset and spawn-floor checks; XR disabled')
arguments.add_argument('--classic-passages',action='store_true',help='Actual Classic337 owner break/reset and authored floors; XR disabled')
arguments.add_argument('--classic-source-cuts',action='store_true',help='Actual Classic718 source-cut owner break/reset and authored floors; XR disabled')
arguments.add_argument('--local-match',action='store_true',help='Actual deliberate local-match UI, zone, life and pod composition; XR disabled')
arguments.add_argument('--local-match-fixes',action='store_true',help='Only observed Practice and zone-view repairs plus cleanup; XR disabled')
arguments.add_argument('--ghost-reload',action='store_true',help='Independent actual Practice reload priority and source ghost lifecycle; XR disabled')
arguments.add_argument('--source-loot',action='store_true',help='Actual source28 rack,18 checkpoint props, finite grenade pickup and Alpha caches; XR disabled')
arguments.add_argument('--archive-ghost',action='store_true',help='Actual Alpha and both Magnum source ghost recovery; XR disabled')
arguments.add_argument('--archive-items',action='store_true',help='Actual Explore Alpha sparse caches/scatter and mounted red rack; XR disabled')
arguments.add_argument('--source-training',action='store_true',help='Actual source practice/rescue/paddles and positive sparse archive; XR disabled')
arguments.add_argument('--training-fixes',action='store_true',help='Only observed source rescue visibility/contact and final-stage Range camera repairs; XR disabled')
arguments.add_argument('--baseline-ground',action='store_true',help='Actual source baseline layout/custody/settle and Range matched cameras; XR disabled')
arguments.add_argument('--training-capture',action='store_true',help='Only source Range world-layer camera correction; no gameplay interactions')
arguments.add_argument('--entry-map',help='One explicit catalog ID for native Start/restart/floor/render checks')
arguments.add_argument('--parity-recipe',type=pathlib.Path,help='Finite matched Godot camera capture recipe; no gameplay feature tests')
arguments.add_argument('--quest-build',action='store_true',help='Explicit approved Android slot, capped at40minutes')
arguments.add_argument('--quest-skip-release-lint',action='store_true')
arguments.add_argument('--quest-native-receipt',type=pathlib.Path)
arguments.add_argument('--quest-checkpoint-receipt',type=pathlib.Path)
arguments.add_argument('--quest-baseline-receipt',type=pathlib.Path)
arguments.add_argument('--quest-feedback-receipt',type=pathlib.Path)
arguments.add_argument('--quest-arsenal-receipt',type=pathlib.Path)
arguments.add_argument('--quest-entry-receipt',type=pathlib.Path)
arguments.add_argument('--quest-output-root',type=pathlib.Path,help='Fresh artifact root on a drive with sufficient space; no cache or historical build is moved')
arguments.add_argument('--source-freeze',type=pathlib.Path)
arguments.add_argument('--disk-monitor-receipt',type=pathlib.Path)
arguments.add_argument('--cache-backpacks',action='store_true')
arguments.add_argument('--tactical-map',action='store_true')
arguments.add_argument('--tactical-capture',action='store_true')
arguments.add_argument('--tactical-alpha',action='store_true')
arguments.add_argument('--tactical-hands',action='store_true')
arguments.add_argument('--valley-bots',action='store_true')
arguments.add_argument('--warden-visual',action='store_true',help='Finite actual Warden source-mesh and four Unity-only visual captures; XR off')
arguments.add_argument('--ordinary-hurt',action='store_true',help='Finite accepted shield-health loss through existing actor hurt pool; XR off')
arguments.add_argument('--valley-recovery',action='store_true',help='Finite source campaign recovery timing and mode isolation; XR off')
arguments.add_argument('--ragdolls',action='store_true',help='Finite original source geometry and bounded corpse lifecycle; XR off')
arguments.add_argument('--warden-defense',action='store_true',help='Finite actual Warden constructed cover and guard/cadence controls; XR off')
arguments.add_argument('--warden-combat',action='store_true',help='Finite actual Warden carried damage and production attack controls; XR off')
arguments.add_argument('--valley-routes',action='store_true',help='Finite source guard route queries/controller/autonomous cases; XR off')
arguments.add_argument('--valley-missions',action='store_true',help='Finite normal Relay Valley progression/checkpoint fixture; no physical-combat or extraction-craft acceptance')
arguments.add_argument('--valley-combat',action='store_true')
arguments.add_argument('--valley-gun',action='store_true')
arguments.add_argument('--source-pad-cue',action='store_true')
arguments.add_argument('--zone-danger-audio',action='store_true')
arguments.add_argument('--backpack-release',action='store_true')
arguments.add_argument('--source-ballistic-roles',action='store_true')
arguments.add_argument('--awp-current-ray',action='store_true')
arguments.add_argument('--dt11-audio-stacks',action='store_true')
arguments.add_argument('--late-head-collision',action='store_true')
arguments.add_argument('--bot-feedback-optics',action='store_true')
arguments.add_argument('--selective-finish-followup',action='store_true')
arguments.add_argument('--feedback-round2-followup',action='store_true')
arguments.add_argument('--nonmetal-v7-controls',action='store_true')
arguments.add_argument('--feedback-round2',action='store_true')
arguments.add_argument('--feedback-257cfbea',action='store_true')
arguments.add_argument('--published-range',action='store_true')
arguments.add_argument('--nonmetal-v7',action='store_true')
arguments.add_argument('--selective-finish',action='store_true')
arguments.add_argument('--bot-front-faces',action='store_true')
arguments.add_argument('--source-soundscape',action='store_true')
arguments.add_argument('--pickup-98254',action='store_true')
arguments.add_argument('--pickup-98254-remaining',action='store_true')
arguments.add_argument('--sheen-98254',action='store_true')
arguments.add_argument('--building-98254',action='store_true')
arguments.add_argument('--building-beam-98254',action='store_true')
arguments.add_argument('--crouch-98254',action='store_true')
arguments.add_argument('--overhang-98254',action='store_true')
arguments.add_argument('--gallery-tracer-98254',action='store_true')
arguments.add_argument('--paddles-98254',action='store_true')
arguments.add_argument('--tdm-hud-98254',action='store_true')
arguments.add_argument('--overhang-walk-98254',action='store_true')
arguments.add_argument('--soundscape-capture',action='store_true')
arguments.add_argument('--cache-drink',action='store_true')
arguments.add_argument('--gallery-hand-capture',action='store_true')
arguments.add_argument('--gallery-hand-fit',action='store_true')
arguments.add_argument('--legacy-score-capture',action='store_true')
arguments.add_argument('--world-scores',action='store_true')
arguments.add_argument('--zone-pads',action='store_true')
arguments.add_argument('--reverse-incline',action='store_true')
arguments.add_argument('--hand-world',action='store_true')
arguments.add_argument('--compact-pod',action='store_true')
arguments.add_argument('--backpack-full-6237',action='store_true')
arguments.add_argument('--interaction-6237',action='store_true')
arguments.add_argument('--cloth-6237',action='store_true')
arguments.add_argument('--feedback-6237',action='store_true')
arguments.add_argument('--post-playtest',action='store_true')
arguments.add_argument('--post-playtest-fixes',action='store_true')
arguments.add_argument('--post-playtest-remaining',action='store_true')
arguments.add_argument('--item-handling',action='store_true')
arguments.add_argument('--menu-usability',action='store_true')
arguments.add_argument('--menu-gallery-only',action='store_true')
arguments.add_argument('--menu-gallery-followup',action='store_true')
arguments.add_argument('--mixed-cache',action='store_true')
arguments.add_argument('--working-art',action='store_true')
arguments.add_argument('--rarity-highlights',action='store_true')
arguments.add_argument('--support-grip',action='store_true')
arguments.add_argument('--glove-articulation',action='store_true')
arguments.add_argument('--surface-audio',action='store_true')
arguments.add_argument('--lan-lobby',action='store_true')
arguments.add_argument('--lan-lobby-layout',action='store_true')
arguments.add_argument('--lan-presence',action='store_true')
arguments.add_argument('--lan-presence-frame',action='store_true')
arguments.add_argument('--lan-map',action='store_true')
arguments.add_argument('--lan-round',action='store_true')
options=arguments.parse_args();cutoff=datetime.datetime.fromisoformat(options.release_until)
if cutoff.tzinfo is None:raise SystemExit('UTC offset required for concrete release')
start = datetime.datetime.now(datetime.timezone.utc)
seconds = min(2400 if options.quest_build else 900, (cutoff - start).total_seconds())
if seconds <= 0:
    raise SystemExit("Released engine window has ended")
command = [sys.executable, str(root / "skins_pipeline/tools/gpu_worker.py"),
           "--owner", "unity-r23-released-slot", "--nonblocking", "--", "powershell.exe",
           "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
           str(root / ("unity/Tools/PlayableR23/corrected_player_floor.ps1" if options.player_floor else "unity/Tools/PlayableR23/floor_diagnostic.ps1" if options.video_feedback or options.ordinary_hurt or options.valley_recovery or options.ragdolls or options.warden_visual or options.warden_defense or options.warden_combat or options.source_pad_cue or options.zone_danger_audio or options.backpack_release or options.source_ballistic_roles or options.awp_current_ray or options.dt11_audio_stacks or options.late_head_collision or options.valley_routes or options.valley_missions or options.selective_finish_followup or options.feedback_round2_followup or options.nonmetal_v7_controls or options.feedback_round2 or options.feedback_257cfbea or options.published_range or options.nonmetal_v7 or options.selective_finish or options.bot_front_faces or options.bot_feedback_optics or options.gallery_tracer_98254 or options.paddles_98254 or options.tdm_hud_98254 or options.overhang_walk_98254 or options.overhang_98254 or options.crouch_98254 or options.building_beam_98254 or options.building_98254 or options.sheen_98254 or options.pickup_98254_remaining or options.pickup_98254 or options.soundscape_capture or options.source_soundscape or options.valley_gun or options.valley_combat or options.valley_bots or options.tactical_hands or options.tactical_alpha or options.tactical_capture or options.tactical_map or options.cache_backpacks or options.cache_drink or options.gallery_hand_capture or options.gallery_hand_fit or options.legacy_score_capture or options.world_scores or options.zone_pads or options.reverse_incline or options.hand_world or options.compact_pod or options.backpack_full_6237 or options.interaction_6237 or options.cloth_6237 or options.feedback_6237 or options.lan_round or options.lan_map or options.lan_presence_frame or options.lan_presence or options.lan_lobby_layout or options.lan_lobby or options.surface_audio or options.glove_articulation or options.support_grip or options.working_art or options.rarity_highlights or options.menu_usability or options.menu_gallery_only or options.menu_gallery_followup or options.mixed_cache or options.item_handling or options.post_playtest_remaining or options.post_playtest_fixes or options.post_playtest or options.floor_only or options.source_ui or options.motion_only or options.valley_only or options.feedback_only or options.arsenal_only or options.r6_gameplay or options.appearance_only or options.lan_spawn_health_range or options.lan_spawn_health or options.lower_grilles or options.family_passages or options.tower_family or options.classic_passages or options.classic_source_cuts or options.local_match or options.local_match_fixes or options.ghost_reload or options.source_loot or options.archive_ghost or options.archive_items or options.source_training or options.training_fixes or options.training_capture or options.baseline_ground or options.entry_map or options.parity_recipe else "unity/Tools/PlayableR23/build_after_maps_release.ps1")), "-MapsReleased"]
if options.source_ui:command.append('-SourceUi')
if options.motion_only:command.append('-MotionOnly')
if options.valley_only:command.append('-ValleyOnly')
if options.feedback_only:command.append('-FeedbackOnly')
if options.arsenal_only:command.append('-ArsenalOnly')
if options.r6_gameplay:command.append('-R6Gameplay')
if options.appearance_only:command.append('-AppearanceOnly')
if options.lan_spawn_health_range:command.append('-LanSpawnHealthRange')
if options.lan_spawn_health:command.append('-LanSpawnHealth')
if options.video_feedback:command+=['-VideoFeedback',options.video_feedback]
if options.lower_grilles:command.append('-LowerGrilles')
if options.family_passages:command.append('-FamilyPassages')
if options.tower_family:command.append('-TowerFamily')
if options.classic_passages:command.append('-ClassicPassages')
if options.classic_source_cuts:command.append('-ClassicSourceCuts')
if options.local_match:command.append('-LocalMatch')
if options.local_match_fixes:command.append('-LocalMatchFixes')
if options.ghost_reload:command.append('-GhostReload')
if options.source_loot:command.append('-SourceLoot')
if options.archive_ghost:command.append('-ArchiveGhost')
if options.archive_items:command.append('-ArchiveItems')
if options.source_training:command.append('-SourceTraining')
if options.training_fixes:command.append('-TrainingFixes')
if options.training_capture:command.append('-TrainingCapture')
if options.menu_usability:command.append('-MenuUsability')
if options.menu_gallery_only:command.append('-MenuGalleryOnly')
if options.menu_gallery_followup:command.append('-MenuGalleryFollowup')
if options.mixed_cache:command.append('-MixedCache')
if options.item_handling:command.append('-ItemHandling')
if options.post_playtest_remaining:command.append('-PostPlaytestRemaining')
if options.post_playtest_fixes:command.append('-PostPlaytestFixes')
if options.working_art:command.append('-WorkingArt')
if options.rarity_highlights:command.append('-RarityHighlights')
if options.support_grip:command.append('-SupportGrip')
if options.glove_articulation:command.append('-GloveArticulation')
if options.surface_audio:command.append('-SurfaceAudio')
if options.lan_lobby:command.append('-LanLobby')
if options.lan_lobby_layout:command.append('-LanLobbyLayout')
if options.lan_presence:command.append('-LanPresence')
if options.lan_presence_frame:command.append('-LanPresenceFrame')
if options.lan_map:command.append('-LanMap')
if options.lan_round:command.append('-LanRound')
if options.gallery_tracer_98254:command+=['-Feedback6237','-GalleryTracer98254Only']
if options.paddles_98254:command+=['-Feedback6237','-Paddles98254Only']
if options.tdm_hud_98254:command+=['-Feedback6237','-TdmHud98254Only']
if options.overhang_walk_98254:command+=['-Feedback6237','-OverhangWalk98254Only']
if options.overhang_98254:command+=['-Feedback6237','-Overhang98254Only']
if options.crouch_98254:command+=['-Feedback6237','-Crouch98254Only']
if options.building_beam_98254:command+=['-Feedback6237','-BuildingBeam98254Only']
if options.building_98254:command+=['-Feedback6237','-Building98254Only']
if options.sheen_98254:command+=['-Feedback6237','-Sheen98254Only']
if options.pickup_98254_remaining:command+=['-Feedback6237','-Pickup98254RemainingOnly']
if options.pickup_98254:command+=['-Feedback6237','-Pickup98254Only']
if options.source_soundscape:command+=['-Feedback6237','-SourceSoundscapeOnly']
if options.soundscape_capture:command+=['-Feedback6237','-SoundscapeCaptureOnly']
if options.selective_finish_followup:command+=['-Feedback6237','-SelectiveFinishFollowupC1Only']
if options.feedback_round2_followup:command+=['-Feedback6237','-FeedbackRound2FollowupOnly']
if options.nonmetal_v7_controls:command+=['-Feedback6237','-NonmetalV7ControlsOnly']
if options.feedback_round2:command+=['-Feedback6237','-FeedbackRound2Only']
if options.feedback_257cfbea:command+=['-Feedback6237','-Feedback257Only']
if options.published_range:command+=['-Feedback6237','-PublishedRangeOnly']
if options.nonmetal_v7:command+=['-Feedback6237','-NonmetalV7Only']
if options.selective_finish:command+=['-Feedback6237','-SelectiveFinishC1Only']
if options.bot_front_faces:command+=['-Feedback6237','-BotFrontFacesC1Only']
if options.source_pad_cue:command+=['-Feedback6237','-SourcePadCueOnly']
if options.zone_danger_audio:command+=['-Feedback6237','-ZoneDangerAudioOnly']
if options.backpack_release:command+=['-Feedback6237','-BackpackReleaseOnly']
if options.source_ballistic_roles:command+=['-Feedback6237','-SourceBallisticRolesOnly']
if options.awp_current_ray:command+=['-Feedback6237','-AwpCurrentRayOnly']
if options.dt11_audio_stacks:command+=['-Feedback6237','-Dt11AudioStacksOnly']
if options.late_head_collision:command+=['-Feedback6237','-LateHeadCollisionOnly']
if options.bot_feedback_optics:command+=['-Feedback6237','-BotFeedbackC1Only']
if options.valley_gun:command+=['-Feedback6237','-ValleyGunOnly']
if options.valley_combat:command+=['-Feedback6237','-ValleyCombatOnly']
if options.valley_bots:command+=['-Feedback6237','-ValleyBotsOnly']
if options.warden_visual:command+=['-Feedback6237','-WardenVisualOnly']
if options.ordinary_hurt:command+=['-Feedback6237','-OrdinaryHurtOnly']
if options.valley_recovery:command+=['-Feedback6237','-ValleyRecoveryOnly']
if options.ragdolls:command+=['-Feedback6237','-RagdollsOnly']
if options.warden_defense:command+=['-Feedback6237','-WardenDefenseOnly']
if options.warden_combat:command+=['-Feedback6237','-WardenCombatOnly']
if options.valley_missions:command+=['-Feedback6237','-ValleyMissionsOnly']
if options.valley_routes:command+=['-Feedback6237','-ValleyRoutesOnly']
if options.tactical_hands:command+=['-Feedback6237','-TacticalHandsOnly']
if options.tactical_alpha:command+=['-Feedback6237','-TacticalAlphaOnly']
if options.tactical_capture:command+=['-Feedback6237','-TacticalCaptureOnly']
if options.tactical_map:command+=['-Feedback6237','-TacticalMapOnly']
if options.cache_backpacks:command+=['-Feedback6237','-CacheBackpacksOnly']
if options.cache_drink:command+=['-Feedback6237','-CacheDrinkOnly']
if options.gallery_hand_capture:command+=['-Feedback6237','-GalleryHandCaptureOnly']
if options.gallery_hand_fit:command+=['-Feedback6237','-GalleryHandFitOnly']
if options.legacy_score_capture:command+=['-Feedback6237','-LegacyScoreCaptureOnly']
if options.world_scores:command+=['-Feedback6237','-WorldScoresOnly']
if options.zone_pads:command+=['-Feedback6237','-ZonePadsOnly']
if options.reverse_incline:command+=['-Feedback6237','-ReverseInclineOnly']
if options.hand_world:command+=['-Feedback6237','-HandWorldOnly']
if options.compact_pod:command+=['-Feedback6237','-CompactPodOnly']
if options.backpack_full_6237:command+=['-Feedback6237','-BackpackFull6237Only']
if options.interaction_6237:command+=['-Feedback6237','-Interaction6237Only']
if options.cloth_6237:command+=['-Feedback6237','-Cloth6237Only']
if options.feedback_6237:command.append('-Feedback6237')
if options.post_playtest:command.append('-PostPlaytest')
if options.baseline_ground:command.append('-BaselineGround')
if options.entry_map:command+=['-EntryMap',options.entry_map]
if options.parity_recipe:command+=['-ParityRecipe',str(options.parity_recipe.resolve())]
if options.build_only:command.append('-BuildOnly')
if options.quest_build:
    command=[sys.executable,str(root/'skins_pipeline/tools/gpu_worker.py'),'--owner','unity-r23-quest-first-build','--nonblocking','--','powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'unity/Tools/PlayableR23/quest_first_build.ps1'),'-MapsReleased']
    if options.quest_skip_release_lint:command+=['-SkipReleaseLint']
    if options.quest_native_receipt:command+=['-NativeReceipt',str(options.quest_native_receipt.resolve())]
    if options.quest_checkpoint_receipt:command+=['-CheckpointReceipt',str(options.quest_checkpoint_receipt.resolve())]
    if options.quest_baseline_receipt:command+=['-BaselineReceipt',str(options.quest_baseline_receipt.resolve())]
    if options.quest_feedback_receipt:command+=['-FeedbackReceipt',str(options.quest_feedback_receipt.resolve())]
    if options.quest_arsenal_receipt:command+=['-ArsenalReceipt',str(options.quest_arsenal_receipt.resolve())]
    if options.quest_entry_receipt:command+=['-MapEntryReceipt',str(options.quest_entry_receipt.resolve())]
    if options.source_freeze:command+=['-SourceFreeze',str(options.source_freeze.resolve())]
    if options.quest_output_root:command+=['-BuildOutputRoot',str(options.quest_output_root.resolve())]
if options.valley_export:
    export=root/'unity/Tools/PlayableR23/SourceWorldExport'
    if options.valley_plan is None:raise SystemExit('Explicit fresh Valley plan required; executed captures are immutable')
    command=[sys.executable,str(root/'campaign-staging/20261104-three-chats/world/engine_lane.py'),'--',sys.executable,str(root/'skins_pipeline/tools/gpu_worker.py'),'--owner','unity-r23-valley-export','--nonblocking','--',sys.executable,str(export/'run_valley_batch.py'),'--plan',str(options.valley_plan.resolve()),'--godot',str(root/'campaign-staging/20261104-three-chats/world/toolchain/godot-isolated/Godot_v4.7.2-stable_win64.exe'),'--deadline',options.release_until]
def human_player_running():
    probe=subprocess.run(['tasklist.exe','/FI','IMAGENAME eq AshfallR23VR.exe','/FO','CSV','/NH'],capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
    return 'ashfallr23vr.exe' in probe.stdout.lower()
if human_player_running():raise SystemExit('Human Unity player active; heavy admission deferred')
disk_guard = None
if options.quest_build:
    if options.disk_monitor_receipt is None:
        raise SystemExit('Quest build requires a fresh absolute D: --disk-monitor-receipt')
    disk_guard = SlotDiskGuard(options.disk_monitor_receipt,options.quest_output_root or root/'unity/Builds')
    refusal = disk_guard.sample(initial=True)
    if refusal:
        disk_guard.finish(dict(disposition=refusal, admitted=False))
        raise SystemExit(refusal)
child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, creationflags=subprocess.CREATE_NO_WINDOW)
if disk_guard is not None:
    disk_guard.worker_pid = child.pid
lines = queue.Queue()
def read():
    for line in child.stdout:
        lines.put(line)
threading.Thread(target=read, daemon=True).start()
end = time.monotonic() + seconds
next_human_check=time.monotonic()+1
reason = "completed"
try:
    if disk_guard is not None:
        disk_guard.write()
    while child.poll() is None:
        try:
            line = lines.get(timeout=.5)
            print(line, end="", flush=True)
            if "GPU_QUEUE_WAIT" in line:
                reason = "GPU busy: nonblocking admission refused"
                break
        except queue.Empty:
            pass
        if time.monotonic() >= end:
            reason = "Released slot deadline reached"
            break
        if time.monotonic()>=next_human_check:
            next_human_check=time.monotonic()+1
            if disk_guard is not None:
                refusal = disk_guard.sample()
                if refusal:
                    reason = refusal
                    break
            if human_player_running():
                reason='Human player started; own diagnostic tree preempted'
                break
except BaseException as error:
    reason = 'Wrapper monitoring error: ' + str(error)
    raise
finally:
    if child.poll() is None:
        # Only this worker and its descendants belong to the bounded slot.
        subprocess.run(["taskkill.exe", "/PID", str(child.pid), "/T", "/F"], check=False)
        child.wait(timeout=20)
    while not lines.empty():
        print(lines.get(), end="", flush=True)
    if disk_guard is not None:
        refusal = disk_guard.sample()
        if refusal and reason == 'completed':
            reason = refusal + ' (observed at worker exit)'
    result = dict(start=start.isoformat(), end=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  worker_pid=child.pid, exit_code=child.returncode, disposition=reason)
    if disk_guard is not None:
        disk_guard.finish(result)
        path = disk_guard.receipt.with_name(disk_guard.receipt.stem + '-terminal.json')
    else:
        path = root / "docs/agent-runs/20261104-three-chats/unity/playable-r23/released-slot-result.json"
    path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result), flush=True)
sys.exit(child.returncode if reason == "completed" else 75)
