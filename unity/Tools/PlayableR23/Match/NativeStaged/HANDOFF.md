# Actual local match native fixture — released

`Assets/Ashfall/Editor/Playable/R24LocalMatchSmoke.cs` + matching `.meta` are published and held. Staged source is byte-identical. Root owns CLI routing and native admission. No engine/device/native execution by this worker.

Entry: `new R24LocalMatchSmoke().Run()` (`IEnumerator`). `AutoFinish=true` writes terminal JSON and calls `R23SmokeControl.Finish`; composite callers may set false and read `Passed`/`Json`. 120s startup wait; 180s active bound including cleanup. Individual group errors are retained and do not manufacture later passes.

Exactly five receipt groups:

1. `ActualDefaultExploreUnchanged`: checks initial null selection before clearing anything; opening MATCH RULES does not start; actual existing UI GAME SPAWN loads Range with no match/zone/deck/life policy.
2. `ActualPracticeInfiniteOwners`: actual UI preset/start ? real Root/Health/standard kit. Actual M1014 fire, real stick-click/UP/BACK reload observes unlimited reserve -1 while stored finite shells remain unchanged. Real construction trigger/release places on actual Range geometry without debit. DAMAGE0 tested after actual protection expires.
3. `ActualFfaZoneHealthRespawn`: actual UI controls set source-legal zone wait0/speed20/damage8, damage0, shield73, protection10, respawn1 and time60. Checks original catalog-local square and .65 ratio, native supported outside floor, protection, actual direct-HP zone loss with unchanged shield, real delayed respawn/protection and clock results.
4. `ActualTeamReviveDeckConsoleCarrier`: actual source two-team Range deck/cabin colliders, fresh simulated tracked console trigger through Root.Update, real terrain landing plan, sole Rig carrier and preserved head/origin locals, fresh actual console eject; genuine downed?eliminated Health/UI, no fabricated ally or successful revive claim.
5. `ActualSceneCleanup`: ends real match/zone/carrier policy, restores original map through actual loader and selected map. Outer finally also restores tracked locals, original root pose and manual/pump/calibration flags. Actual round/inventory resets are intentional: use in Root's disposable smoke scene.

Root's Editor-only ManualSmoke XR branch is used; production DisplayRunning is unchanged. `Rig.SmokeXrMove` establishes real motor/frame state, `SmokeTrackedHands` publishes root-relative hand samples, then `SmokeUiUpdate` calls actual Root.Update. Flight-console input uses the same current-frame clock Root consumes, avoiding a stale dock pose while airborne. UI request—not a fabricated MatchActive assignment—starts every match. Public real `TickMatch` advances the actual host clock in .1s intervals; no replacement Health, collision world or floor exists.

Three additional captures use actual Rig.Head: `local-match-ffa-direct-zone`, `local-match-pod-actual-carrier`, `local-match-actual-ghost-waiting`. These are XR-off integration probes, not source-matched camera or HMD acceptance.

Additional UI correction: `SetLocalMatchLifeState(string,float,bool=false)` consumes the authoritative life/countdown/results values. Exact frozen `match_hud_r3.gd:59` notices; legacy nonmatch YOU DIED preserved. Downed/respawning/eliminated have HOME and no lifecycle-bypassing legacy RESTART; results has REMATCH. Flight binds real Health before HUD updates and clears it on EndMatch.

Checks: `Match/check.py` 217 source-pinned rules/life cases PASS. `Match/check_native_api.py` freshly compiled current EditorRuntime and full BuildAndSmoke fixture against installed Unity 6000.6 APIs, warnings as errors: PASS. Native not run here. First harness incorrectly appended runtime sources while importing the prebuilt runtime; corrected to compile a private current runtime DLL and reference it, without source suppression.
