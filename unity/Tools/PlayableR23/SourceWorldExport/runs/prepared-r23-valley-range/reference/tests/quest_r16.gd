extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var facts:Dictionary={}
class LaunchProbe extends Node3D:
	var launches=0
	var normal:=Vector3.ZERO
	func launch_from_pad_r10(_speed:float,direction:Vector3)->bool:launches+=1;normal=direction;return true
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func wait_map()->bool:
	var deadline=Time.get_ticks_msec()+90000
	while Time.get_ticks_msec()<deadline:
		await process_frame
		if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready:return true
	return false
func wait_stage(stage:String)->bool:
	var deadline=Time.get_ticks_msec()+90000
	while Time.get_ticks_msec()<deadline:
		await process_frame
		if game.network_bridge.net.stage==stage and game.network_bridge.playing():
			for i in 5:await physics_frame
			return true
	return false
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game)
	var menu:Node=game.front_menu
	menu.selected_map="archive:battlefield-paintball-gods-pitdevil";menu.activate(0)
	check(menu.page=="spawn_choice" and game.front_end_active,"READY asks for lobby/game before entering archive")
	menu._explore_from_r16("lobby");check(await wait_map(),"Paintball READY loads")
	var map:Node=game.active_sandbox_map
	check(map.statistics.native_warmup_spawns_r14==1,"roof-layer white lobby marker recovered")
	check(map.warmup_spawn_r8.distance_to(map.spawn)>25,"Paintball lobby and authored game sites are separate")
	check(game.player.global_position.distance_to(map.warmup_spawn_r8)<2,"READY lobby choice actually uses white marker floor")
	facts.paintball={"lobby":str(map.warmup_spawn_r8-map.global_position),"match":str(map.spawn-map.global_position),"match_source":map.spawn_origin,"lods":map.statistics.mesh_lods_r16}
	check(int(map.statistics.mesh_lods_r16.meshes)>50 and not map.statistics.mesh_lods_r16.hard_cutoff,"distance LODs applied without distance cutoff")
	game.paused=false;game.player.input_enabled=true
	for kind in ["soda","shaker"]:
		check(game.item_catalog_088.activate({"kind":"consumable","item":kind},false),kind+" grab accepted")
		check(game.field_consumables.held_kind==kind and int(game.field_consumables.stock[kind])>0,kind+" grab enters inventory and hand")
		var stock:int=game.field_consumables.stock[kind];var count:int=game.field_consumables.pickups.size()
		check(game.item_catalog_088.activate({"kind":"consumable","item":kind},true),kind+" trigger accepted")
		check(game.field_consumables.pickups.size()==count+1 and game.field_consumables.stock[kind]==stock,kind+" trigger only adds ground pickup")
	game.field_consumables.stow(false)
	# Room-scale contact is under the HMD, rather than a stationary root pivot.
	var actor:Node=game.player;var camera:Node=actor.xr_camera
	var old_camera:Transform3D=camera.transform;actor.xr_enabled=true;actor.tracking_available=true
	camera.position.x=2;camera.position.z=-1
	var feet:Vector3=actor.get_feet_position_r16()
	check(is_equal_approx(feet.x,camera.global_position.x) and is_equal_approx(feet.z,camera.global_position.z) and is_equal_approx(feet.y,actor.global_position.y),"room-scale pad feet follow HMD XZ and body floor height")
	actor.xr_enabled=false;camera.transform=old_camera
	# Overhead archive platforms are support surfaces, not buried terrain.
	var ceiling:=StaticBody3D.new();ceiling.collision_layer=1;ceiling.add_to_group("construction_terrain");root.add_child(ceiling)
	ceiling.global_position=Vector3(9000,2004,9000);var shape:=CollisionShape3D.new();var box:=BoxShape3D.new();box.size=Vector3(10,.2,10);shape.shape=box;ceiling.add_child(shape)
	await physics_frame
	var panel:=Transform3D(Basis.IDENTITY,Vector3(9000,2000,9000))
	var exclusions:Array[RID]=[]
	check(not game.field_builder._wall_fully_buried_091(panel,exclusions),"stacked archive deck does not block building beneath it")
	ceiling.add_to_group("terrain_surface")
	check(game.field_builder._wall_fully_buried_091(panel,exclusions),"actual terrain still rejects fully buried walls")
	ceiling.queue_free()
	# AWP pole crossing retains a continuous basis and exact firing direction.
	var Scope=preload("res://scripts/awp_scope.gd");var basis:Basis=Scope.world_level_basis(Vector3(0,.9,-.4).normalized());var crossed=false
	for angle in range(70,111):
		var forward:=Vector3(0,sin(deg_to_rad(float(angle))),-cos(deg_to_rad(float(angle))))
		var next:Basis=Scope.continuous_level_basis(forward,basis,crossed)
		check(next.is_finite() and next.x.dot(basis.x)>.99 and (-next.z).dot(forward)>.99999,"AWP continuous pole "+str(angle))
		if absf(forward.y)>.98:crossed=true
		basis=next
	# Pad clearance follows the normal despite an existing fall and stick input.
	var previous_position:Vector3=actor.global_position;actor.global_position=Vector3(9000,2200,9000);actor.velocity=Vector3(3,-50,8)
	var normal:=Vector3(.8,.6,0)
	check(actor.launch_from_pad_r10(45,normal),"tilted pad launch accepted while falling")
	actor.apply_locomotion_motion(Vector3.BACK,Vector3.FORWARD,false,false,1.0/30)
	check(actor.velocity.distance_to(normal*45)<.01,"initial pad trajectory is exactly face-normal at30Hz")
	actor.global_position=previous_position;actor.reset_motion()
	var sem=preload("res://scripts/map_semantics_r14.gd").new();root.add_child(sem)
	var probe:=LaunchProbe.new();root.add_child(probe);var center:=Vector3(9000,2300,9000)
	sem.jump_pads.assign([{"center":center,"normal":normal,"radius":1.3,"speed":45.0}])
	probe.global_position=center+normal*2;check(not sem.try_launch(probe),"outside pad does not launch")
	probe.global_position=center-normal*2;check(sem.try_launch(probe) and probe.normal.is_equal_approx(normal),"swept low-FPS contact catches pad chain")
	probe.queue_free();sem.queue_free()
	game._return_home0992();await process_frame;menu.selected_map="archive:battlefield-paintball-gods-pitdevil";menu._explore_from_r16("game")
	check(await wait_map(),"READY game choice reloads")
	map=game.active_sandbox_map
	check(actor.global_position.distance_to(map.spawn)<2 and actor.global_position.distance_to(map.warmup_spawn_r8)>25,"READY game choice uses authored match coordinates")
	game._return_home0992();await process_frame
	var net:Node=game.network_bridge.net
	check(net.host("R16 Quest",25461,"isolated")==OK,"real ENet host starts")
	net.request("rules",{"map":"archive:battlefield-paintball-gods-pitdevil","mode":"revive","teams":2,"catalog":true,"zone_enabled":false,"time_limit":0,"score_limit":0})
	check(await wait_stage("lobby"),"hosted Paintball warmup loads")
	check(not game.network_bridge.launch_r12.ensure_layout(),"Paintball has no sky platform in warmup")
	map=game.active_sandbox_map
	check(actor.global_position.distance_to(map.warmup_spawn_r8)<3,"host warmup uses white marker despite previous game exploration choice")
	for kind in ["soda","shaker"]:
		check(game.item_catalog_088.activate({"kind":"consumable","item":kind},false),"host catalog grab "+kind)
		for i in 8:await physics_frame
		check(game.field_consumables.held_kind==kind,"host catalog equips "+kind)
	# Exercise the authoritative provider with a real accepted-room-scale head offset.
	actor.set_physics_process(false);game.network_bridge.set_process(false)
	var row:Dictionary=map.semantics_r14.health_pads[0]
	var floor:Vector3=map._floor(row.center+Vector3.UP*2,5)+Vector3.UP*.12
	net._relocate_r12(1,floor+Vector3.LEFT*2)
	net.poses[1].head.origin=floor+Vector3.UP*1.7
	net.players[1].health=20.0;net.players[1].protection_until=0.0
	check(game.network_bridge._health_pad_rate_r14(1)>0,"host healing uses validated head XZ even when root is2m away")
	for i in 12:net._process(.1)
	check(float(net.players[1].health)>=30,"server actually restores health over time in warmup")
	check(map.semantics_r14.healing_rate_at(floor+Vector3.UP*3)==0,"pad cannot heal another storey")
	game.network_bridge.set_process(true);actor.set_physics_process(true)
	net.request("start");check(await wait_stage("match"),"Paintball Team Revive actual match starts")
	check(not game.network_bridge.launch_r12.ensure_layout() and net.players[1].get("deployment_r12","")=="","Paintball match uses arena spawn without deployment state")
	check(not net._pod_launch_r12(1,{"pod":"0:0"}),"authority refuses Paintball pod launch")
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/QUEST-R16.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"XR-off source integration, real ENet host, geometry and pose tests. Not physical Quest acceptance."},"\t"));file.close()
	print("QUEST_R16 checks=",checks," failures=",failures.size())
	game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
