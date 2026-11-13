extends SceneTree
var game:Node
var net:Node
var checks=0
var failures:Array=[]
var facts:Array=[]
class RideProbe extends Node:
	var game:Node
	var travelled=0.0
	var last:=Vector3.INF
	var samples=0
	func _physics_process(_delta:float):
		if not game.player.pod_carried_r12:return
		var at:Vector3=game.player.global_position
		if last.is_finite():travelled+=last.distance_to(at)
		last=at;samples+=1
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func wait_stage(stage:String)->bool:
	var limit:int=Time.get_ticks_msec()+30000
	while Time.get_ticks_msec()<limit:
		await process_frame
		if net.stage==stage and net.world_ready and not game.network_bridge._preparing:
			for i in 5:await physics_frame
			return true
	return false
func run():
	for map_id in ["range","valley","cyber_bird","archive:alfa-aglae-souza","archive:poppy-gods-poplightning","archive:afterlife-gods-alamo-hank"]:
		game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
		check(net.host("Pilot",25411,"r12")==OK,map_id+" host")
		net.request("rules",{"map":map_id,"mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0})
		check(await wait_stage("lobby"),map_id+" in-map shared warmup")
		var warmup:Vector3=game.player.global_position;var calibration:Transform3D=game.player.xr_origin.transform;var launch:Node=game.network_bridge.launch_r12
		var probe:=RideProbe.new();probe.game=game;probe.process_physics_priority=100;game.add_child(probe)
		net.request("start");check(await wait_stage("match"),map_id+" starts Team Revive")
		check(launch.stations.size()==2,map_id+" separate team sky decks")
		for station in launch.stations:
			check(station.pods.instance_count==12 and station.bodies.size()==12,map_id+" exactly twelve available pods per platform")
			var forward:Vector3=-station.frame.basis.z;check(absf(forward.x)<.001 or absf(forward.z)<.001,map_id+" cardinal launch across map side")
		check(game.player.global_position.y>launch.top+60,map_id+" initial spawn is high above arena")
		if map_id=="cyber_bird":
			check(launch.top>120 and launch.top<300,map_id+" instanced giant birds and towers are below deployment; finished canopy remains above it")
			var drop:Vector3=launch.stations[0].frame*Vector3(0,-1,-20);var hit:Dictionary=game.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(drop,drop-Vector3.UP*1000,1))
			check(not hit.is_empty() and hit.position.y<launch.top+1,map_id+" direct sky-deck hot drop reaches the arena without an intervening ceiling")
		check(game.player.global_position.distance_to(warmup)>30,map_id+" warmup separate from team deployment")
		var map:Node=game.active_sandbox_map
		check(launch.staging(game.player.global_position),map_id+" spawns on a real team deck")
		if map!=null and map.has_method("safe_spawn_r8"):check(map.safe_spawn_r8(game.player.global_position),map_id+" deck floor/headroom and bounds valid")
		var before:Dictionary=net.players[1].duplicate(true);net.request("pod_launch",{"pod":before.pod_key_r12});check(net.pods_r12.is_empty(),map_id+" launch rejected from outside cabin")
		var pod:Transform3D=launch.pod_pose(1);game.player.global_position=pod.origin+Vector3.UP*.12;game.player.reset_motion();net.poses[1].root.origin=game.player.global_position
		net.request("pod_launch",{"pod":"wrong"});check(net.pods_r12.is_empty(),map_id+" wrong pod ownership rejected")
		var other_slot=11;var team:int=net.players[1].team;var alternative:Transform3D=launch.stations[team].frame*Transform3D(Basis.IDENTITY,Vector3((other_slot-5.5)*3.6,.14,-6))
		net._relocate_r12(1,alternative.origin+Vector3.UP*.12);net._publish();game.player.global_position=alternative.origin+Vector3.UP*.12;game.player.reset_motion()
		net.request("pod_launch",{"pod":"%d:%d:%d"%[net.epoch,team,other_slot],"end":Vector3(99999,99999,99999)})
		check(net.pods_r12.has(1),map_id+" explicit launch starts authorized flight")
		if net.pods_r12.has(1):
			check(int(net.pods_r12[1].slot)==11,map_id+" any unused own-team pod works, including twelfth pod")
			check((net.pods_r12[1].end-net.pods_r12[1].clear).dot(-alternative.basis.z)>0,map_id+" travel continues forward across arena")
			var ride:Dictionary=net.pods_r12[1];check(ride.end.y==ride.clear.y and ride.clear.y>ride.start.y+4 and ride.end.distance_to(ride.start)>8,map_id+" server chooses a cabin-clearance climb then bounded level travel")
			var sample:Vector3=net.pod_position_r12(ride,float(ride.time)+float(ride.duration)*.05);check(sample.is_finite() and sample.y>=ride.start.y,map_id+" initial powered climb is finite and rises")
			var start:Vector3=game.player.global_position
			await create_timer(.8).timeout
			var speed:float=(ride.start.distance_to(ride.clear)+ride.clear.distance_to(ride.end))/float(ride.duration)
			check(probe.travelled>speed*.35 and probe.samples>=5 and game.player.pod_carried_r12,map_id+" actual player carried over many physics ticks")
			check(game.player.xr_origin.transform.is_equal_approx(calibration),map_id+" flight retains calibration")
			net.request("pod_eject");for i in 4:await physics_frame
			check(net.pods_r12[1].state=="released" and not game.player.pod_carried_r12 and game.player.input_enabled,map_id+" early eject restores ordinary movement")
			var height:float=game.player.global_position.y;await create_timer(.3).timeout;check(game.player.global_position.y<height,map_id+" gravity works after eject")
			var count:int=net.pods_r12.size();net.request("pod_launch",{"pod":before.pod_key_r12});check(net.pods_r12.size()==count and net.pods_r12[1].state=="released",map_id+" used pod cannot be relaunched")
			# Controlled authority clock reaches fuel expiry without waiting through the whole map crossing.
			ride.state="flying";ride.time=net.now()-float(ride.duration)-.01;net.players[1].deployment_r12="flying";net._step_pods_r12();check(ride.state=="released" and net.players[1].deployment_r12=="released",map_id+" automatic expiry ejects without trapping player")
			facts.append({"map":map_id,"warmup":str(warmup),"deck":str(pod.origin),"ground_target":str(ride.ground_target),"duration":ride.duration})
		# Direct hot drop is a real departure from the platform, without any launch request.
		var edge:Vector3=launch.stations[0].frame*Vector3(26,.3,0);net._relocate_r12(1,edge);net._publish();game.player.global_position=edge;game.player.reset_motion();var at_y:float=edge.y;await create_timer(.4).timeout;check(game.player.global_position.y<at_y,map_id+" open side permits jumping/falling without a pod")
		net.request("home");check(await wait_stage("lobby"),map_id+" round returns to shared warmup")
		check(net.pods_r12.is_empty(),map_id+" round transition clears pod state")
		check(game.player.xr_origin.transform.is_equal_approx(calibration),map_id+" warmup/deploy/eject retains calibration")
		net.request("rules",{"mode":"respawn"});for i in 4:await physics_frame
		check(not is_instance_valid(launch.structure),map_id+" deathmatch has no team launch structures")
		net.request("start");check(await wait_stage("match"),map_id+" deathmatch starts")
		check(net.players[1].get("deployment_r12","")=="",map_id+" deathmatch uses arena, not pod deployment")
		if map!=null and map.has_method("safe_spawn_r8"):check(map.safe_spawn_r8(game.player.global_position),map_id+" deathmatch spawn floor/headroom")
		var seen:Dictionary={}
		for i in 25:seen[str(game.network_bridge._arena_spawn_r12(1,net.players,net.rules))]=true
		check(seen.size()>1,map_id+" deathmatch spawn choices vary")
		game.network_bridge.leave_session();game._quitting=true;game.queue_free();for i in 6:await process_frame
		check(not is_instance_valid(launch),map_id+" session launch system releases with Main")
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/LAUNCH-R14.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"actual solo ENet host Main, two modes, launch/eject/hot-drop and real multi-frame carrier motion; XR off, not headset acceptance"},"\t"));print("LAUNCH_R14 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)


