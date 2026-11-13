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
	for id in ["archive:alfa-aglae-souza","archive:afterlife-gods-alamo-hank","archive:poppy-gods-poplightning","archive:classic-bigbox-vr"]:
		game=load("res://scripts/main.gd").new();root.add_child(game)
		for i in 4:await process_frame
		net=game.network_bridge.net
		check(net.host("Solo Host",25329,"r10")==OK,id+" actual solo host")
		net.request("rules",{"map":id,"mode":"respawn","zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"catalog":true})
		var ready:bool=await wait_stage("lobby");check(ready,id+" enters in-map warmup")
		if ready:
			var map:Node=game.active_sandbox_map;var map_ref:WeakRef=weakref(map);var origin:Transform3D=game.player.xr_origin.transform;var waiting:Vector3=game.player.global_position
			check(map.safe_spawn_r8(waiting),id+" warmup is grounded inside map")
			net.request("character",{"style":2});check(int(net.players[1].style)==2,id+" warmup identity accepted")
			net.request("start");check(await wait_stage("match"),id+" host start reaches actual match")
			check(map_ref.get_ref()==game.active_sandbox_map,id+" start reuses single map")
			check(map.safe_spawn_r8(net.players[1].spawn),id+" match spawn has floor/headroom")
			check(net.players[1].spawn.distance_to(waiting)>=10,id+" match relocates away from warmup")
			net.request("character",{"style":3});check(int(net.players[1].style)==2,id+" authority rejects mid-match skin change")
			game.field_menu.settings_page="pause";check(not game.field_menu._labels().has("SKINS"),id+" pause menu has no skins")
			facts.append({"id":id,"warmup":str(waiting),"match":str(net.players[1].spawn),"distance":net.players[1].spawn.distance_to(waiting),"source_spawns":map.spawn_points.size()})
			net.rules.time_limit=1;net.match_started=net.now()-2
			check(await wait_stage("lobby"),id+" end returns to warmup")
			check(map_ref.get_ref()==game.active_sandbox_map,id+" end reuses map")
			check(game.player.xr_origin.transform.is_equal_approx(origin),id+" calibration survives")
			game.network_bridge.leave_session();for i in 5:await process_frame
			check(map_ref.get_ref()==null,id+" leave releases map")
		game._quitting=true;game.queue_free();for i in 5:await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/HOST-TRANSITION-R10.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"xr":"off","scope":"actual Main solo ENet host warmup/start/end/leave and authoritative skin lock; no headset or Internet"},"\t"));print("HOST_TRANSITION_R10 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
