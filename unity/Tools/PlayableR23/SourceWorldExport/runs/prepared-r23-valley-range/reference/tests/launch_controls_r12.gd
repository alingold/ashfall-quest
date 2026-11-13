extends SceneTree
var game:Node
var net:Node
var checks=0
var failures:Array=[]
var facts:Dictionary={}
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func wait_for(test:Callable)->bool:
	var limit:int=Time.get_ticks_msec()+30000
	while Time.get_ticks_msec()<limit:
		await process_frame
		if test.call():return true
	return false
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
	check(net.host("Controls",25431,"r12")==OK,"real host starts")
	net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0})
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not game.network_bridge._preparing),"warmup ready")
	net.request("start");check(await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing),"match ready")
	for i in 5:await physics_frame
	freeze(game)
	var launch:Node=game.network_bridge.launch_r12;var actor:Node=game.player;var calibration:Transform3D=actor.xr_origin.transform
	actor.xr_enabled=true;game.field_menu.is_open=false;game.front_menu.session_overlay=false
	var trackers:Array=[]
	for i in 2:
		var tracker:=XRControllerTracker.new();tracker.type=XRServer.TRACKER_CONTROLLER;tracker.name="launch_r12_"+str(i);XRServer.add_tracker(tracker);trackers.append(tracker)
		var hand:XRController3D=actor.right_hand if i==0 else actor.left_hand;hand.tracker=tracker.name;hand.pose="grip";tracker.set_pose("grip",Transform3D.IDENTITY,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH);tracker.set_input("trigger",0.0);tracker.set_input("grip",0.0)
	# Choose an unused cabin different from the assigned starting lane. No forced pod assignment.
	var frame:Transform3D=launch.stations[0].frame;var cabin:Transform3D=frame*Transform3D(Basis.IDENTITY,Vector3((5-3.5)*3.6,.14,-6))
	actor.global_position=cabin.origin+Vector3.UP*.12;net.poses[1].root.origin=actor.global_position
	actor.right_hand.global_transform=cabin*Transform3D(Basis.IDENTITY,Vector3(0,1.25,-.5));actor.left_hand.global_transform=actor.right_hand.global_transform
	trackers[0].set_input("trigger",1.0);launch._physics_process(.016)
	check(net.pods_r12.is_empty(),"held trigger on entry does not automatically launch")
	trackers[0].set_input("trigger",0.0);launch._physics_process(.016);trackers[0].set_input("trigger",1.0);launch._physics_process(.016)
	check(net.pods_r12.has(1) and net.pods_r12[1].state=="flying" and int(net.pods_r12[1].slot)==5,"aimed tracked trigger launches chosen own-team cabin")
	launch._physics_process(.016);launch._physics_process(.016)
	check(actor.pod_carried_r12 and net.pods_r12[1].state=="flying","holding launch trigger does not immediately eject")
	check(actor.xr_origin.transform.is_equal_approx(calibration),"tracked control leaves calibration intact")
	var ride:Dictionary=net.pods_r12[1];var moving:Transform3D=Transform3D(ride.basis,net.pod_position_r12(ride)-Vector3.UP*.12)
	actor.left_hand.global_transform=moving*Transform3D(Basis.IDENTITY,Vector3(0,1.25,-.5));trackers[0].set_input("trigger",0.0);launch._physics_process(.016)
	game.field_menu.is_open=true;trackers[1].set_input("grip",1.0);launch._physics_process(.016);check(ride.state=="flying","open menu blocks pod controls")
	game.field_menu.is_open=false;launch._physics_process(.016);check(ride.state=="flying","closing menu with held grip does not eject")
	trackers[1].set_input("grip",0.0);launch._physics_process(.016);trackers[1].set_input("grip",1.0);launch._physics_process(.016)
	check(ride.state=="released","aimed left-hand grip explicitly ejects")
	launch._physics_process(.016);check(not actor.pod_carried_r12,"eject releases locomotion carrier")
	var engine:AudioStreamWAV=load("res://assets/audio/sfx/r12/pod_engine_r12.wav")
	check(engine.loop_mode==AudioStreamWAV.LOOP_FORWARD,"engine is a real bounded-lifetime loop")
	for cue in ["pod_launch_r12","pod_exit_r12"]:
		var sound:AudioStreamWAV=load("res://assets/audio/sfx/r12/"+cue+".wav");check(sound.loop_mode==AudioStreamWAV.LOOP_DISABLED,"one-shot does not loop "+cue)
	ride.time=net.now()-float(ride.duration)-1;launch._refresh_flights();for i in 3:await process_frame
	check(launch.flights.is_empty(),"expired pod and its engine voice are freed")
	for tracker in trackers:XRServer.remove_tracker(tracker)
	actor.xr_enabled=false
	var old:WeakRef=weakref(launch.structure);launch.clear();for i in 3:await process_frame
	check(old.get_ref()==null,"clearing deployment frees platform and audio subtree")
	# Select the narrowest real map and exercise the maximum eight teams there.
	net.active=false;net.players.clear();var smallest="";var width=INF;var catalog=preload("res://scripts/map_catalog1000.gd")
	for row in preload("res://scripts/archive_catalog1003.gd").rows():
		var info:Dictionary=catalog.info(row.id);var span:Vector2=info.max-info.min
		if minf(span.x,span.y)<width:width=minf(span.x,span.y);smallest=row.id
	preload("res://scripts/cyber_bird_registration.gd").select(game,smallest)
	for i in 6:await physics_frame;await process_frame
	freeze(game);net.active=true;net.stage="match";net.rules.map=smallest;net.rules.teams=8;net.rules.mode="revive";net.epoch+=1;net.poses.clear();net.pods_r12.clear()
	for id in range(1,9):net.players[id]={"team":id-1,"deaths":0,"name":"Pilot "+str(id)}
	check(launch.ensure_layout() and launch.stations.size()==8,"smallest map supports eight team platforms")
	for i in 4:await physics_frame
	var spawns:Array=[]
	for id in net.players:
		var at:Vector3=game.network_bridge._spawn_point(id,net.players,net.rules);spawns.append(at)
		check(game.active_sandbox_map.safe_spawn_r8(at),"maximum-team sky spawn has floor/headroom "+str(id))
		check(at.y>launch.top+60,"maximum-team deck remains high above map "+str(id))
	for a in 8:
		for b in range(a+1,8):check(spawns[a].distance_to(spawns[b])>18,"maximum-team start positions separate "+str(a)+"/"+str(b))
	facts={"smallest_map":smallest,"narrowest_span_metres":width,"teams":8,"synthetic_XR_inputs":true}
	net.players.clear();net.active=false;launch.clear();game._quitting=true;game.queue_free();for i in 3:await process_frame
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/LAUNCH-CONTROLS-R12.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"actual Main; synthetic XRControllerTracker buttons and controlled eight-team roster with real physics; XR runtime off, not physical headset acceptance"},"\t"));print("LAUNCH_CONTROLS_R12 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
