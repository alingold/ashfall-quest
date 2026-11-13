extends SceneTree
var game:Node
var net:Node
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func wait_stage(stage:String)->bool:
	var deadline:int=Time.get_ticks_msec()+30000
	while Time.get_ticks_msec()<deadline:
		await process_frame
		if net.stage==stage and not game.network_bridge._preparing:
			for i in 4:await physics_frame
			return true
		await create_timer(.02).timeout
	return false
func run():
	for id in ["archive:poppy-gods-poplightning","archive:magnum-chaosvr"]:
		game=load("res://scripts/main.gd").new();root.add_child(game)
		for i in 4:await process_frame
		net=game.network_bridge.net
		check(net.host("Solo Host",25329,"r9")==OK,id+" actual solo host")
		net.request("rules",{"map":id,"mode":"respawn","zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"catalog":true})
		var ready:bool=await wait_stage("lobby");check(ready,id+" enters in-map warmup")
		if ready:
			var map:Node=game.active_sandbox_map;var map_ref:WeakRef=weakref(map);var origin:Transform3D=game.player.xr_origin.transform
			check(not game.front_end_active and map.safe_spawn_r8(game.player.global_position),id+" warmup is grounded inside map")
			check(net.can_start(),id+" solo host can start")
			if id.contains("poppy"):check(game.player.global_position.y>100,"Poppy warmup is on separate high platform")
			net.request("start");check(await wait_stage("match"),id+" host start reaches actual match")
			check(map_ref.get_ref()==game.active_sandbox_map,id+" start reuses single map")
			check(map.safe_spawn_r8(game.player.global_position),id+" actual match spawn has floor/headroom")
			if id.contains("poppy"):check(game.player.global_position.y<90,"Poppy match uses arena rather than lobby")
			if map.magnum_only_r9():
				for entity in net.entities.values():
					if entity.kind=="weapon":check(int(entity.catalog)==7,"Chaos authoritative world and inventory contain only Magnum")
				var before:int=net.players[1].spawn_revision
				game.player.global_position.y=map.fall_level_r9-1
				net.poses[1].root.origin=game.player.global_position
				map._hazard_clock=.2;map._physics_process(.2)
				check(net.players[1].spawn_revision>before,"Chaos fall causes authoritative respawn revision")
				check(net.players[1].spawn.is_equal_approx(map.center_respawn_r9),"Chaos authoritative rescue is above center platform")
				for i in 4:await physics_frame
				check(game.player.global_position.distance_to(map.center_respawn_r9)<1,"Chaos authoritative rescue reaches actual player")
			facts.append({"id":id,"warmup":str(map.warmup_spawn_r8),"match":str(net.players[1].spawn),"fall_level":map.fall_level_r9})
			net.rules.time_limit=1;net.match_started=net.now()-2
			check(await wait_stage("lobby"),id+" round end returns to warmup")
			check(map_ref.get_ref()==game.active_sandbox_map and game.player.global_position.distance_to(map.warmup_spawn_r8)<3,id+" round end gathers without duplicate city")
			check(game.player.xr_origin.transform.is_equal_approx(origin),id+" calibration origin survives transitions")
			game.network_bridge.leave_session();for i in 5:await process_frame
			check(map_ref.get_ref()==null,id+" leave releases entire map")
		game._quitting=true;game.queue_free();for i in 5:await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/HOST-TRANSITION-R9.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"xr":"off","scope":"actual Main, real solo ENet host warmup/start/end/leave; Poppy arena separation and authoritative Chaos fall rescue; no headset or Internet"},"\t"));print("HOST_TRANSITION_R9 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
