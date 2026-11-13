extends SceneTree
class Actor extends CharacterBody3D:
	var xr_enabled:=true
	var input_enabled:=true
	var head_obstructed:=false
	var climbing_left:=false
	var climbing_right:=false
	var dominant_hand:="right"
	var _grabs:Dictionary={}
	var right_hand:=XRController3D.new()
	var left_hand:=XRController3D.new()
	var view:=Camera3D.new()
	func get_weapon_hand()->XRController3D:return right_hand
	func get_support_hand()->XRController3D:return left_hand
	func get_view_camera()->Camera3D:return view
	func is_hand_input_ready(_hand:XRController3D)->bool:return true
	func weapon_hand_climbing()->bool:return false
var failures:=0
var checks:=0
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures+=1;print("FAIL ",label)
class Rig extends Actor:
	func get_locomotion_anchor_position()->Vector3:return view.global_position
class Pair extends "res://scripts/dual_pistols.gd":
	func _vr_active()->bool:return true
var game:Node
var net:Node
var role:String
var facts:Dictionary={}
func wait_for(test:Callable,seconds:float=35)->bool:
	var deadline:int=Time.get_ticks_msec()+int(seconds*1000)
	while Time.get_ticks_msec()<deadline:
		await process_frame
		if test.call():return true
	return false
func barrier(color:int)->bool:
	if net.marks.size()!=2:return false
	for mark in net.marks.values():
		if int(mark.color)<color:return false
	return true
func mark(color:int):net.request("mark",{"point":game.player.global_position,"normal":Vector3.UP,"color":color})
func run():
	var args:=OS.get_cmdline_user_args();role=args[0];game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
	if role=="host":
		check(net.host("Host R10",int(args[2]),"r10")==OK,"actual host")
		net.request("rules",{"map":"archive:classic-bigbox-vr","mode":"respawn","zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"catalog":true})
	else:check(net.join("127.0.0.1","Friend R10",int(args[2]),"r10")==OK,"actual client")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and net.players.size()==2 and not game.network_bridge._preparing),"both peers in loaded warmup")
	var waiting:Vector3=game.player.global_position;mark(1)
	if net.server:
		check(await wait_for(func():return barrier(1)),"both peers ready to start");net.request("start")
	check(await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing),"match starts on both peers")
	check(net.players[net.me()].spawn.distance_to(waiting)>10,"match spawn differs from warmup on this peer")
	var style:int=net.players[net.me()].style;net.request("character",{"style":posmod(style+1,7)})
	await create_timer(.3).timeout;check(int(net.players[net.me()].style)==style,"skin is locked on real peer")
	if net.server:
		var selected:Node=null
		for group in game.active_sandbox_map.gameplay_r8._panels_r8.values():
			for panel in group:
				if panel.glass_r10:selected=panel;break
			if selected!=null:break
		check(selected!=null,"glass assembly exists");selected.take_damage(1)
	check(await wait_for(func():return not net.archive_broken_r8.is_empty()),"break state crosses peer transport")
	await create_timer(.4).timeout
	var hidden=false
	for group in game.active_sandbox_map.gameplay_r8._panels_r8.values():
		for panel in group:
			if net.archive_broken_r8.has(panel.key):hidden=panel._broken and not panel.view.visible and panel.collision_layer==0
	check(hidden,"glass assembly and collider removed on this peer");mark(3)
	var revision:int=net.players[net.me()].spawn_revision
	if net.server:
		check(await wait_for(func():return barrier(3)),"both peers confirm break")
		var pads:Array=game.active_sandbox_map.gameplay_r8.pads
		check(pads.size()>=2,"two clear launch sites available")
		net.spawn_provider=func(id:int,players:Dictionary,_rules:Dictionary)->Vector3:return pads[players.keys().find(id)%pads.size()]+Vector3.UP*.12
		for id in net.players:net._spawn(id)
		net._publish()
	check(await wait_for(func():return int(net.players[net.me()].spawn_revision)>revision),"authoritative setup places peer on pad")
	var start_y:float=net.players[net.me()].spawn.y;var highest=start_y;var remote_high:Dictionary={};var until:int=Time.get_ticks_msec()+2400
	while Time.get_ticks_msec()<until:
		await physics_frame;highest=maxf(highest,game.player.global_position.y)
		if net.server:
			for id in net.poses:remote_high[id]=maxf(float(remote_high.get(id,net.players[id].spawn.y)),net.poses[id].root.origin.y)
	check(highest-start_y>25,"pad sustains high flight on actual peer ticks");facts.pad_height=highest-start_y
	if net.server:
		for id in net.players:check(float(remote_high.get(id,0))-net.players[id].spawn.y>25,"server accepts ascending movement for peer "+str(id))
	game.player.set_physics_process(false);mark(4)
	if net.server:
		check(await wait_for(func():return barrier(4)),"both peers complete flight")
		for id in net.players:
			var eid:String="r10_uzi_"+str(id);var capacity:int=preload("res://scripts/weapon_rarity_r6.gd").capacity(11,1)
			net.entities[eid]={"id":eid,"kind":"weapon","catalog":11,"owner":id,"rarity":1,"ammo":capacity,"capacity":capacity,"reload":"ready","position":Vector3.ZERO,"basis":Basis.IDENTITY};net._normalize_weapon(net.entities[eid]);net.players[id].inventory.append(eid)
		net._publish()
	var eid:String="r10_uzi_"+str(net.me());check(await wait_for(func():return game.network_bridge.indices.has(eid)),"authoritative Uzi granted and synchronized")
	var slot:int=game.network_bridge.indices[eid];var actor:=Rig.new();root.add_child(actor);actor.collision_layer=0;actor.add_child(actor.right_hand);actor.add_child(actor.left_hand);actor.add_child(actor.view);actor.global_position=game.player.global_position;actor.view.position=Vector3(0,1.65,0)
	var trackers:Array=[]
	for i in 2:
		var tracker:=XRControllerTracker.new();tracker.type=XRServer.TRACKER_CONTROLLER;tracker.name="r10_peer_"+str(i);XRServer.add_tracker(tracker);trackers.append(tracker)
		var hand:XRController3D=actor.right_hand if i==0 else actor.left_hand;hand.tracker=tracker.name;tracker.set_input("trigger",0.0);tracker.set_pose("grip",Transform3D.IDENTITY,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
	var pair:=Pair.new();game.weapon.add_child(pair);pair.setup(actor,false);pair.set_process(false);pair.set_physics_process(false);preload("res://scripts/arsenal_data.gd").apply(pair,11);preload("res://scripts/weapon_rarity_r6.gd").apply(pair,11,1)
	game.weapon.weapons[slot].shutdown();game.weapon.weapons[slot].queue_free();game.weapon.weapons[slot]=pair;pair.ammo_provider=game.weapon;pair.ammo_slot=slot;pair.set_meta("catalog_index",11);game.weapon._connect_weapon_events(pair)
	game.weapon.selected=slot;game.weapon.is_holstered=false;game.weapon._apply_selection();game.weapon.set_process(false);pair.set_process(false);game.network_bridge._sync_world();pair._process(.01)
	var shots:Array=[0,0];pair.shot.connect(func(_origin:Vector3,_direction:Vector3,_power:float):shots[pair.emitting_side_r10]+=1)
	for tracker in trackers:tracker.set_input("trigger",1.0)
	until=Time.get_ticks_msec()+6000;var last:int=Time.get_ticks_msec()
	while Time.get_ticks_msec()<until:
		await process_frame;var now:int=Time.get_ticks_msec();var delta:float=(now-last)*.001;last=now
		actor.right_hand.global_transform=Transform3D(Basis.IDENTITY,actor.global_position+Vector3(-.15,1.3,-.3));actor.left_hand.global_transform=actor.right_hand.global_transform;pair._process(delta)
	await create_timer(.3).timeout
	check(shots==[20,20],"simultaneous coincident triggers emit exactly 20 rounds per hand")
	check(net.entities[eid].ammo==0 and net.entities[eid].hands==[0,0],"both authoritative hand magazines stay empty")
	check(pair.guns[0].ammo==0 and pair.guns[1].ammo==0,"local guns do not refill from snapshots");facts.shots=shots;facts.authority=net.entities[eid].hands
	mark(8);check(await wait_for(func():return barrier(8)),"both peers finish firing checks")
	await create_timer(1).timeout
	var result={"passed":failures==0,"checks":checks,"failures":failures,"facts":facts,"scope":"two real loopback ENet Main peers; real multi-frame pad movement, replicated glass/skin lock, actual paired trigger processing using synthetic trackers; no headset or Internet"}
	var f=FileAccess.open(args[1],FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));print("PEER_RUNTIME_R10 ",JSON.stringify(result))
	# The host may leave first; its disconnect resets the client's rack and frees this pair.
	if is_instance_valid(pair):pair.shutdown()
	for tracker in trackers:XRServer.remove_tracker(tracker)
	game.network_bridge.leave_session();game._quitting=true;game.queue_free();actor.queue_free();await create_timer(.15).timeout;quit(0 if failures==0 else 1)
