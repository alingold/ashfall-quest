extends "res://tests/r18net/peer_base_r18.gd"
## Single real Main. An archive map can be retired (disconnect, kick, host leaves, quick
## re-selection) after enter() but before its deferred _settle() completes. The staging
## freeze meta "archive_loading_r17" must not outlive the map that set it, or the player is
## left with frozen physics and unrestored calibration in the next scene. R17 result: the
## meta IS cleared by leave()/_exit_tree(); the deferred archive _settle() and community
## _refresh() still run on the retired node and print SCRIPT ERROR (launcher fails on it).
## Run: run-peers.ps1 -Roles host -Script tests/r18net/map_abort_r18.gd
const ARCHIVE:="archive:classic-bigbox-vr"
func run():
	check(net.host("Abort Host",port,"abort")==OK,"solo listen host")
	net.request("rules",{"map":"range","mode":"respawn","zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0})
	check(await wait_for(loaded_warmup,60),"solo warmup loaded")
	# Case A: retire immediately after enter (same frame as the network _prepare select()).
	# On the host, request() publishes synchronously and _prepare() runs select()/enter()
	# before returning; _settle() is still deferred, i.e. the window a kick/disconnect hits.
	net.request("rules",{"map":ARCHIVE})
	check(map_id()==ARCHIVE,"archive map entered synchronously")
	var staged:bool=int(game.player.get_meta("archive_loading_r17",0))!=0
	facts.staged_meta_seen=staged
	bridge.leave_session()
	await frames(4)
	for i in 8:await physics_frame
	var leftover:int=int(game.player.get_meta("archive_loading_r17",0))
	facts.meta_after_abort=leftover
	check(leftover==0,"staging freeze meta cleared when the loading archive map is retired")
	# Physics must run again offline: open range exploration and fall onto its floor.
	game.front_menu.selected_map="range";game._start_from_title()
	await frames(10)
	game.player.global_position+=Vector3.UP*3.0;game.player.velocity=Vector3.ZERO
	var y0:float=game.player.global_position.y
	for i in 30:await physics_frame
	facts.offline_drop_metres=y0-game.player.global_position.y
	check(y0-game.player.global_position.y>.5,"player physics not frozen after aborted archive load")
	await finish("MAP_ABORT_R18","One real Main, solo listen host, XR off; archive map retired between enter() and deferred _settle(); desktop physics sampled offline afterwards.")
