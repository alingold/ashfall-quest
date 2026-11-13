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
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run():
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);var net:Node=game.network_bridge.net;check(net.host("Dual trigger test",25370,"test")==OK,"real Main host starts")
	var end:int=Time.get_ticks_msec()+15000
	while (net.stage!="lobby" or not net.world_ready or game.network_bridge._preparing) and Time.get_ticks_msec()<end:await process_frame
	freeze(game);check(net.world_ready,"host world is ready");net.rules.infinite_ammo=false
	var actor:=Rig.new();root.add_child(actor);actor.collision_layer=0;actor.add_child(actor.right_hand);actor.add_child(actor.left_hand);actor.add_child(actor.view);actor.global_position=game.player.global_position;actor.view.position=Vector3(0,1.65,0)
	var trackers:Array=[]
	for i in 2:
		var tracker:=XRControllerTracker.new();tracker.type=XRServer.TRACKER_CONTROLLER;tracker.name="r10_dual_"+str(i);XRServer.add_tracker(tracker);trackers.append(tracker)
		var hand:XRController3D=actor.right_hand if i==0 else actor.left_hand;hand.tracker=tracker.name;tracker.set_input("trigger",0.0);tracker.set_pose("grip",Transform3D.IDENTITY,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
	var pair:=Pair.new();game.weapon.add_child(pair);pair.setup(actor,false);pair.set_process(false);pair.set_physics_process(false);preload("res://scripts/arsenal_data.gd").apply(pair,11);preload("res://scripts/weapon_rarity_r6.gd").apply(pair,11,1)
	game.weapon.weapons[11].shutdown();game.weapon.weapons[11].queue_free();game.weapon.weapons[11]=pair;pair.ammo_provider=game.weapon;pair.ammo_slot=11;game.weapon._connect_weapon_events(pair)
	var entity={"id":"dual_test","kind":"weapon","catalog":11,"owner":1,"rarity":1,"ammo":pair.magazine_size,"capacity":pair.magazine_size,"reload":"ready","seated":pair.magazine_size,"hands":[pair.magazine_size/2,pair.magazine_size/2],"hand_states":["ready","ready"],"position":Vector3.ZERO,"basis":Basis.IDENTITY}
	net.entities.dual_test=entity;net.players[1].inventory=["dual_test"];game.network_bridge.indices.dual_test=11;game.network_bridge.local_ids[11]="dual_test";game.weapon_inventory.owned.assign([11]);game.weapon.selected=11;game.weapon.is_holstered=false;game.weapon._apply_selection()
	game.network_bridge._sync_world();pair._process(.01)
	for tracker in trackers:tracker.set_input("trigger",1.0)
	check(pair.visible and pair._input_allowed(),"paired gun is drawn and accepts input")
	check(actor.right_hand.get_float("trigger")>.9 and actor.left_hand.get_float("trigger")>.9,"both tracked trigger inputs are actually held")
	var shots:Array=[];pair.shot.connect(func(origin:Vector3,direction:Vector3,_power:float):shots.append({"origin":origin,"direction":direction}))
	for frame in 70:
		# Coincident/crossed hands expose position-based firing-side inference.
		actor.right_hand.global_transform=Transform3D(Basis.IDENTITY,actor.global_position+Vector3(-.15,1.3,-.3));actor.left_hand.global_transform=actor.right_hand.global_transform
		net.server_offset+=.09;pair._process(.09)
	var capacity:int=pair.magazine_size
	check(shots.size()==capacity,"both held triggers emit exactly their combined finite magazines")
	check(entity.ammo==0 and entity.hands==[0,0],"authority consumes both hands, not one hand twice")
	check(pair.guns[0].ammo==0 and pair.guns[1].ammo==0,"actual local guns empty and stay empty")
	var mat:=Pair.new();mat.shotgun_mode=true;game.weapon.add_child(mat);mat.setup(actor,false);preload("res://scripts/arsenal_data.gd").apply(mat,12);preload("res://scripts/weapon_rarity_r6.gd").apply(mat,12,1);mat.set_meta("catalog_index",12)
	game.weapon.weapons[12].shutdown();game.weapon.weapons[12].queue_free();game.weapon.weapons[12]=mat;mat.ammo_provider=game.weapon;mat.ammo_slot=12;game.weapon._connect_weapon_events(mat)
	var mat_capacity:int=mat.magazine_size;net.entities.mat_test={"id":"mat_test","kind":"weapon","catalog":12,"owner":1,"rarity":1,"ammo":mat_capacity,"capacity":mat_capacity,"reload":"ready","seated":mat_capacity,"hands":[mat_capacity/2,mat_capacity/2],"hand_states":["ready","ready"],"position":Vector3.ZERO,"basis":Basis.IDENTITY}
	net.players[1].inventory=["mat_test"];game.network_bridge.indices.mat_test=12;game.network_bridge.local_ids[12]="mat_test";game.weapon_inventory.owned.assign([12]);game.weapon.selected=12;game.weapon._apply_selection();mat.set_process(false);game.network_bridge._sync_world()
	for tracker in trackers:tracker.set_input("trigger",0.0)
	mat._process(.01);var pellets:Array=[0,0];mat.pellet_shot.connect(func(_origin:Vector3,_rays:Array[Vector3],_power:float):pellets[mat.emitting_side_r10]+=1)
	for tracker in trackers:tracker.set_input("trigger",1.0)
	for frame in 20:net.server_offset+=.1;mat._process(.1)
	check(pellets==[1,1],"Matadors remain one shot per press while both triggers are held")
	for pull in range(mat_capacity/2-1):
		for tracker in trackers:tracker.set_input("trigger",0.0)
		net.server_offset+=.5;mat._process(.5)
		for tracker in trackers:tracker.set_input("trigger",1.0)
		net.server_offset+=.5;mat._process(.5)
	check(pellets==[mat_capacity/2,mat_capacity/2] and net.entities.mat_test.hands==[0,0],"paired pellet relay consumes the correct finite hand magazines")
	var result={"passed":failures==0,"checks":checks,"failures":failures,"emissions":shots.size(),"capacity":capacity,"server_ammo":entity.ammo,"server_hands":entity.hands,"local_hands":[pair.guns[0].ammo,pair.guns[1].ammo],"scope":"actual dual-trigger weapon process and Main shot relay into authoritative host/bridge, controlled clock and synthetic tracked inputs; XR off, no headset"}
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/DUAL-TRIGGER-R10.json",FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));print("DUAL_TRIGGER_R10 ",JSON.stringify(result));pair.shutdown();for tracker in trackers:XRServer.remove_tracker(tracker)
	mat.shutdown();game.network_bridge.leave_session();game._quitting=true;game.queue_free();actor.queue_free();await create_timer(.15).timeout;quit(0 if failures==0 else 1)
