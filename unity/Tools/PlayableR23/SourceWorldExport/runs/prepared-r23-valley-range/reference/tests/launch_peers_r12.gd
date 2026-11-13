extends SceneTree
var game:Node
var net:Node
var role:String
var checks=0
var failures:Array=[]
var facts:Dictionary={}
class RideProbe extends Node:
	var game:Node
	var samples=0
	var max_error=0.0
	var travelled=0.0
	var last:=Vector3.INF
	func _physics_process(_delta:float):
		var net:Node=game.network_bridge.net;var id:int=net.me()
		if not net.pods_r12.has(id) or not game.player.pod_carried_r12:return
		if net.pods_r12[id].get("state","")!="flying":return
		var at:Vector3=game.player.global_position
		if last.is_finite():travelled+=last.distance_to(at)
		last=at;samples+=1;max_error=maxf(max_error,at.distance_to(net.pod_position_r12(net.pods_r12[id])))
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func wait_for(test:Callable,seconds:float=35)->bool:
	var limit:int=Time.get_ticks_msec()+int(seconds*1000)
	while Time.get_ticks_msec()<limit:
		await process_frame
		if test.call():return true
	return false
func mark(color:int):net.request("mark",{"point":game.player.global_position,"normal":Vector3.UP,"color":color})
func barrier(color:int)->bool:
	if net.marks.size()!=3:return false
	for value in net.marks.values():
		if int(value.color)<color:return false
	return true
func run():
	var args:=OS.get_cmdline_user_args();role=args[0];game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
	if role=="host":
		check(net.host("Pilot Host",int(args[2]),"r12")==OK,"actual listen host")
		net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0})
	else:check(net.join("127.0.0.1",role,int(args[2]),"r12")==OK,"actual friend joins")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and net.players.size()==3 and not game.network_bridge._preparing),"three loaded warmup peers")
	var warmup:Vector3=game.player.global_position;var calibration:Transform3D=game.player.xr_origin.transform;mark(1)
	if net.server:
		check(await wait_for(func():return barrier(1)),"all peers in shared warmup");net.request("start")
	check(await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing),"Team Revive starts across transport")
	var launch:Node=game.network_bridge.launch_r12;check(await wait_for(func():return launch.stations.size()==2),"team deck geometry available")
	var probe:=RideProbe.new();probe.game=game;probe.process_physics_priority=100;game.add_child(probe)
	var me:int=net.me();var p:Dictionary=net.players[me];check(launch.staging(game.player.global_position),"local peer starts on team deck");check(game.player.global_position.distance_to(warmup)>30,"warmup differs from team start")
	for other in net.players:
		if other==me:continue
		var distance:float=net.players[other].spawn.distance_to(p.spawn)
		check(distance<12 if int(net.players[other].team)==int(p.team) else distance>30,"teammates together; opponents separated")
	mark(2);var revision:int=p.spawn_revision
	if net.server:
		check(await wait_for(func():return barrier(2)),"all peers verify deployment")
		# Position each real client at its assigned cabin; launch remains a client command through authority.
		for id in net.players:net._relocate_r12(id,launch.pod_pose(id).origin+Vector3.UP*.12)
		net._publish()
	check(await wait_for(func():return int(net.players[me].spawn_revision)>revision),"authoritative cabin setup arrives")
	var pod_key:String=net.players[me].pod_key_r12;var health:float=net.players[me].health;var inventory:Array=net.players[me].inventory.duplicate()
	net.request("pod_launch",{"pod":"another player's pod"});await create_timer(.15).timeout;check(not net.pods_r12.has(me),"cross-pod command rejected")
	net.request("pod_launch",{"pod":pod_key,"end":Vector3(90000,90000,90000)})
	check(await wait_for(func():return net.pods_r12.has(me) and net.pods_r12[me].state=="flying"),"authorized pod flight replicated")
	var start:Vector3=game.player.global_position;await create_timer(1.1).timeout
	var journey:Dictionary=net.pods_r12[me];var speed:float=(journey.start.distance_to(journey.clear)+journey.clear.distance_to(journey.end))/float(journey.duration)
	check(probe.travelled>speed*.6 and game.player.pod_carried_r12,"real peer player follows moving pod over multiple physics frames")
	# Sample after the carrier physics step. An idle timer can run after a clock-sync
	# packet but before the next physics step, giving a stale-root/new-clock comparison.
	check(probe.samples>=5 and probe.max_error<2,"local ride agrees with authority clock on actual physics frames")
	check(net.players[me].health==health and net.players[me].inventory==inventory,"launch retains health and inventory")
	check(game.player.xr_origin.transform.is_equal_approx(calibration),"launch retains calibration")
	mark(3)
	if net.server:
		check(await wait_for(func():return barrier(3)),"all peers in flight")
		for id in net.players:check(net.poses[id].root.origin.distance_to(net.pod_position_r12(net.pods_r12[id]))<3,"server receives correct moving pilot pose "+str(id))
		var ride:Dictionary=net.pods_r12[me];var packet:Dictionary=net.poses[me].duplicate(true);packet.seq=int(net._last_pose.get(me,{}).get("seq",0))+1
		var saved_flight:float=net.rules.flight;var saved_fling:float=net.rules.fling;net.rules.flight=.25;net.rules.fling=0.0
		for key in ["root","head","left","right"]:packet[key].origin+=Vector3(1000,0,0)
		var previous:Dictionary=net.poses[me].duplicate(true);previous.time=net.now()-.3;var old_at:Vector3=net.pod_position_r12(ride,net.now()-.3);var shift:Vector3=old_at-previous.root.origin
		for key in ["root","head","left","right"]:previous[key].origin+=shift
		net._last_pose[me]=previous;net._accept_pose(me,packet)
		check(net.poses[me].root.origin.distance_to(net.pod_position_r12(ride))<.2 and int(net.poses[me].seq)==int(packet.seq),"authority clamps arbitrary client ride origins and accepts sparse carrier updates at minimum flight/fling settings")
		net.rules.flight=saved_flight;net.rules.fling=saved_fling
	# Each player independently chooses early eject; peers see that state.
	net.request("pod_eject");check(await wait_for(func():return net.pods_r12[me].state=="released" and not game.player.pod_carried_r12),"client eject accepted and locomotion restored")
	var y:float=game.player.global_position.y
	for i in 30:await physics_frame
	check(game.player.global_position.y<y,"normal falling after pod eject ("+str(y)+" -> "+str(game.player.global_position.y)+")")
	check(game.player.input_enabled and game.player.xr_origin.transform.is_equal_approx(calibration),"eject retains controls and calibration")
	facts={"team":net.players[me].team,"pod":pod_key,"flight_metres":probe.travelled,"destination":str(net.pods_r12[me].ground_target),"ride_physics_samples":probe.samples,"max_physics_sync_error_metres":probe.max_error};mark(4)
	if net.server:
		check(await wait_for(func():return barrier(4)),"all peers eject independently");net.request("home")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not game.network_bridge._preparing),"end returns everyone to shared warmup")
	check(net.pods_r12.is_empty() and not game.player.pod_carried_r12,"round clears all carrier state")
	mark(7);check(await wait_for(func():return barrier(7)),"all peers complete warmup return");await create_timer(1).timeout
	var f=FileAccess.open(args[1],FileAccess.WRITE);var result={"role":role,"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"three actual Main loopback ENet peers, shared warmup, two teams, launch/riding/eject/round return; no XR/headset/Internet"};f.store_string(JSON.stringify(result,"\t"));print("LAUNCH_PEERS_R12 ",JSON.stringify(result));game.network_bridge.leave_session();game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
