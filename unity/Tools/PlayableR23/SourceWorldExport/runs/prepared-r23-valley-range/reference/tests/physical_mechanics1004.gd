extends SceneTree
class World extends Node3D:
	var player:Node
	var field_builder:Node
var failures:Array=[]
var checks=0
var facts:Dictionary={}
var world:Node3D
var actor:Node
var rack:Node
var trackers:Dictionary={}
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node)->void:
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func box(at:Vector3,size:Vector3,parent:Node=null)->StaticBody3D:
	var body=StaticBody3D.new();var shape=CollisionShape3D.new();var mesh=BoxShape3D.new();mesh.size=size;shape.shape=mesh;body.add_child(shape);(world if parent==null else parent).add_child(body);body.global_position=at;return body
func settle()->void:
	await physics_frame;await physics_frame
func run()->void:
	world=World.new();root.add_child(world)
	actor=preload("res://scripts/player.gd").new();world.add_child(actor);actor.setup(false);freeze(actor);world.player=actor
	rack=preload("res://scripts/weapon_rack.gd").new();world.add_child(rack);rack.setup(actor);freeze(rack)
	actor.xr_enabled=true
	for hand in [actor.left_hand,actor.right_hand]:
		var t=XRControllerTracker.new();t.type=XRServer.TRACKER_CONTROLLER;t.name="physical1004_"+str(hand.get_instance_id());XRServer.add_tracker(t);hand.tracker=t.name;hand.pose="grip";t.set_pose("grip",Transform3D.IDENTITY,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH);t.set_input("grip",0.0);t.set_input("trigger",0.0);t.set_input("primary",Vector2.ZERO);trackers[hand]=t
	await process_frame;actor.update_hand_input_rearm(.2)
	var inventory=preload("res://scripts/weapon_inventory.gd").new();world.add_child(inventory);inventory.player=actor;inventory.rack=rack;inventory.owned.assign([0,2,27]);rack.inventory=inventory;inventory.set_process(false)
	rack.selected=27;rack.is_holstered=false;rack._apply_selection();freeze(rack)
	var gun:Node=rack.weapons[27];gun.reset_weapon();rack.Arsenal.apply(gun,27);gun._process(0)
	var launches:Array=[];rack.explosive_launch.connect(func(data:Dictionary):launches.append(data))
	for shot in 3:
		gun.cooldown=0;gun._requires_trigger_release=false;gun.fire();await process_frame
		check(launches.size()==shot+1,"actual rocket firing emits exactly one launch "+str(shot+1))
		if shot<2:
			check(inventory.has_weapon(27),"launcher retained while internal rounds remain")
			var hand:XRController3D=gun.left_hand;var tracker=trackers[hand];tracker.set_input("grip",0.0);gun._poll_vr();hand.global_position=gun.get_offered_magazine_transform().origin;tracker.set_input("grip",1.0);gun._poll_vr()
			check(gun.reload_state=="held_mag","actual support hand grabs offered rocket")
			hand.global_position=gun.model.to_global(gun.ROCKET_INSERT+Vector3.RIGHT*.35);gun._poll_vr();check(gun.reload_state=="held_mag","off-axis rocket insertion is refused")
			var destination:Vector3=gun.ROCKET_MOUTH;destination.z=maxf(destination.z,gun._mag_grab_local.z+gun.required_rocket_insertion_travel()+.01)
			hand.global_position=gun.model.to_global(destination);gun._poll_vr();check(gun.reload_state=="ready" and gun.ammo==1 and gun.rocket_reserve==1-shot,"actual complete insertion stroke uses one internal round")
			tracker.set_input("grip",0.0);gun._poll_vr()
		else:check(not inventory.has_weapon(27) and inventory.expended.has(27),"third shot consumes launcher through deferred rack/inventory signal")
	facts.rocket={"launches":launches.size(),"remaining":gun.rocket_reserve,"owned":inventory.has_weapon(27)}
	rack.is_holstered=true;rack._apply_selection();freeze(rack);actor.xr_enabled=false;actor.global_position=Vector3(-10,0,0);actor.reset_motion()
	var terrain=box(Vector3(0,-.25,0),Vector3(40,.5,40));terrain.add_to_group("construction_terrain")
	var builder=preload("res://scripts/field_builder.gd").new();world.add_child(builder);world.field_builder=builder;builder.setup(actor,rack,world);freeze(builder)
	await settle()
	var state={"trigger":false,"cycle":false,"eligible":true,"tracking":true,"origin":Vector3(.31,1.6,.21),"direction":Vector3(.12,-.15,-1).normalized(),"head":Vector3(.31,1.6,.21)}
	builder.tick_input(state);state.trigger=true;builder.tick_input(state);var at:Transform3D=builder._placement
	check(builder.preview_valid,"actual construction input obtains valid exposed wall preview")
	var loose=RigidBody3D.new();var loose_shape=CollisionShape3D.new();loose_shape.shape=BoxShape3D.new();loose_shape.shape.size=Vector3(.5,.5,.5);loose.add_child(loose_shape);world.add_child(loose);loose.freeze=true;loose.global_position=at.origin
	await settle();builder.tick_input(state)
	var held_count=builder.pieces.size();builder.tick_input(state);check(builder.pieces.size()==held_count,"held trigger does not commit construction before release")
	state.trigger=false;builder.tick_input(state)
	check(builder.pieces.size()>0,"actual release-trigger construction succeeds through a loose item")
	var count=builder.pieces.size();var charges=builder.charges;builder.tick_input(state);check(builder.pieces.size()==count and builder.charges==charges,"released trigger cannot duplicate construction or spend twice")
	var exclusions:Array[RID]=[actor.get_rid()]
	check(builder._wall_fully_buried_091(Transform3D(Basis.IDENTITY,Vector3(6,-2,0)),exclusions),"actual terrain ray probes reject completely buried wall")
	check(not builder._wall_fully_buried_091(Transform3D(Basis.IDENTITY,Vector3(6,.5,0)),exclusions),"partly exposed wall remains eligible")
	var archive=Node3D.new();archive.name="ArchiveGeometry";world.add_child(archive)
	var boundary=box(Vector3(-8,1,-3),Vector3(2,2,.2),archive);var wall=box(Vector3(-12,1,-3),Vector3(2,2,.2),archive);wall.add_to_group("climbable")
	await settle()
	check(actor.find_grab_surface(Vector3(-8,1,-2.94)).is_empty(),"archive invisible boundary cannot become climbing anchor")
	check(not actor.find_grab_surface(Vector3(-12,1,-2.94)).is_empty(),"authored climbable archive wall exposes actual anchor")
	actor.global_position=Vector3(-12,0,-2.3);actor.left_hand.global_position=Vector3(-12,1,-2.94);actor._grab_cooldown=0;actor._update_grab(actor.left_hand,true)
	check(actor._grabs.has(actor.left_hand),"actual player establishes physical wall grip")
	var before:Vector3=actor.global_position;actor.apply_climb_motion(Vector3.UP*.15,1.0/90);check(actor.global_position.y>before.y+.08,"actual anchored climb uses collision-aware player movement");actor._update_grab(actor.left_hand,false,false);check(actor._grabs.is_empty(),"grip release clears physical anchor")
	var net=preload("res://scripts/net_session1000.gd").new();world.add_child(net);net.set_process(false);net.active=true;net.server=true;net.stage="match";net.rules.teams=0;net.rules.headshots=1.0
	for id in [1,2]:net._add_player(id,"RayFixture","");net.players[id].loaded=true;net.players[id].protection_until=0;net.players[id].shield=0
	var bridge=preload("res://scripts/net_bridge1000.gd").new();world.add_child(bridge);bridge.game=world;bridge.net=net;bridge.set_process(false)
	net.poses[1]={"head":Transform3D(Basis.IDENTITY,Vector3(8,1.25,2))};net.poses[2]={"head":Transform3D(Basis.IDENTITY,Vector3(8,1.7,-4))}
	bridge._hit_rays(1,{"catalog":0,"time":net.now(),"origin":Vector3(8,1.25,2),"directions":[Vector3.FORWARD]});var body_damage=100-net.players[2].health;check(body_damage>0,"actual bridge ray applies body damage to remote pose")
	net.players[2].health=100;net.poses[1].head.origin.y=1.7
	bridge._hit_rays(1,{"catalog":0,"time":net.now(),"origin":Vector3(8,1.7,2),"directions":[Vector3.FORWARD]});var head_damage=100-net.players[2].health;check(head_damage>body_damage,"actual ray head intersection applies larger one-star damage")
	var blocker=box(Vector3(8,1.5,-1),Vector3(2,3,.2));await settle();net.players[2].health=100
	bridge._hit_rays(1,{"catalog":0,"time":net.now(),"origin":Vector3(8,1.7,2),"directions":[Vector3.FORWARD]});check(net.players[2].health==100,"physical wall occludes authoritative remote-player ray damage")
	facts.damage={"body":body_damage,"head":head_damage,"wall_blocked":net.players[2].health==100};facts.build={"placed":count,"cost":999-charges}
	await verify_builds_and_projectiles(bridge,net)
	for tracker in trackers.values():XRServer.remove_tracker(tracker)
	net.close(false);rack.shutdown();trackers.clear();world.queue_free();await process_frame;await process_frame
	var result={"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"actual player/rack/gun/inventory and field builder, synthetic XRControllerTracker grip inputs with XR runtime off; real local physics queries and authoritative bridge damage against controlled remote poses. Minimal rig, not Main/multi-process or physical headset proof."};var file=FileAccess.open(OS.get_cmdline_user_args()[0],FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();print("PHYSICAL1004 "+JSON.stringify(result));quit(0 if failures.is_empty() else 1)
func verify_builds_and_projectiles(bridge:Node,net:Node)->void:
	# Controlled authoritative states exercise the actual collision/proxy adapter.
	net.builds.platform={"id":"platform","kind":"floor","at":Transform3D(Basis.IDENTITY,Vector3(-12,1,2)),"key":"controlled_platform","support":false,"health":140.0,"owner":2};bridge._sync_builds();await settle()
	var hit=world.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(-12,3,2),Vector3(-12,.5,2),1))
	check(not hit.is_empty() and hit.collider.get_meta("network_id1000","")=="platform","replicated build state creates actual collision")
	actor.global_position=Vector3(-12,1.15,2);actor.reset_motion()
	for tick in 12:actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,false,false,1.0/90);await physics_frame
	check(actor.is_supported() and actor.global_position.y>.8,"actual player can stand on replicated floor")
	net.hurt_build("platform",1000);bridge._sync_builds();await settle()
	hit=world.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(-12,3,2),Vector3(-12,.5,2),1));check(hit.is_empty(),"authoritative destruction removes replica collision")
	net.poses[2].head.origin=Vector3(8,1.7,-2.5);net.players[2].health=100;net.players[2].shield=0
	bridge._spawn_projectile({"id":900,"player":1,"kind":"smaw","origin":Vector3(8,1.5,2),"velocity":Vector3.FORWARD*12,"net_epoch":net.epoch})
	var until=Time.get_ticks_msec()+2000
	while bridge._projectiles.has(900) and Time.get_ticks_msec()<until:await physics_frame
	check(not bridge._projectiles.has(900) and not net.projectile_poses.has(900),"actual rocket wall contact cleans flight and snapshot")
	check(net.players[2].health<100,"actual rocket contact reaches authoritative explosion damage")
	var rocket_damage=100-net.players[2].health
	bridge._spawn_projectile({"id":901,"player":1,"kind":"frag","origin":Vector3(12,.2,0),"velocity":Vector3(8,0,0),"net_epoch":net.epoch})
	var frag=bridge._projectiles[901];await create_timer(2.0).timeout
	check(is_instance_valid(frag) and frag.ground_roll0991<=3.3 and Vector2(frag.linear_velocity.x,frag.linear_velocity.z).length()<.3,"actual grounded frag settles within bounded travel")
	until=Time.get_ticks_msec()+2500
	while bridge._projectiles.has(901) and Time.get_ticks_msec()<until:await physics_frame
	check(not bridge._projectiles.has(901),"actual frag fuse clears authoritative projectile")
	bridge._spawn_projectile({"id":902,"player":1,"kind":"zone","origin":Vector3(10,.3,0),"velocity":Vector3.DOWN,"net_epoch":net.epoch});await create_timer(.5).timeout
	var zone=bridge._projectiles[902];check(zone.stuck and zone.freeze and zone.global_position.y>=.03,"actual zone grenade settles above floor")
	bridge._clear_projectiles();await settle();check(bridge._projectiles.is_empty(),"round teardown clears actual projectiles")
	facts.replica_physics={"rocket_damage":rocket_damage,"scope":"real replica collision and rigid-body rocket/frag/zone simulation from controlled authoritative dictionaries; no UDP delivery in this minimal rig"}
