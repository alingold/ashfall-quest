extends SceneTree
var game:Node
var net:Node
var checks=0
var failures:Array=[]
var facts:Dictionary={}
var sequence=10000
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func wait_stage(stage:String)->bool:
	var deadline:int=Time.get_ticks_msec()+120000
	while Time.get_ticks_msec()<deadline:
		await process_frame
		if net.stage==stage and net.world_ready and game.network_bridge.playing():
			for i in 5:await physics_frame
			return true
	return false
func pose(at:Vector3,bad_hand:bool=false)->Dictionary:
	sequence+=1
	return {"root":Transform3D(Basis.IDENTITY,at),"head":Transform3D(Basis.IDENTITY,at+Vector3.UP*1.7),"left":Transform3D(Basis.IDENTITY,at+Vector3(-.3,1.2,0)),"right":Transform3D(Basis.IDENTITY,at+Vector3(30 if bad_hand else .3,1.2,0)),"seq":sequence,"net_epoch":net.epoch,"epoch":net.epoch,"spawn_revision":int(net.players[1].spawn_revision)}
func relocate(at:Vector3):
	net._relocate_r12(1,at);net._publish();game.player.global_position=at;game.player.reset_motion()
	for i in 3:await physics_frame
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
	check(net.host("R14 Core",25421,"isolated")==OK,"actual ENet host")
	net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"infinite_builds":true})
	check(await wait_stage("lobby"),"Alpha hosted warmup loads")
	net.request("start");check(await wait_stage("match"),"Alpha actual deathmatch starts")
	var map:Node=game.active_sandbox_map;var bridge:Node=game.network_bridge;var actor:Node=game.player
	actor.set_physics_process(false);bridge.set_process(false)
	check(not map.semantics_r14.marker_visuals.visible,"native spawn symbols hidden in actual match")
	check(map.statistics.native_deathmatch_spawns_r14>0,"native black markers become deathmatch sites")
	var calibration:Transform3D=actor.xr_origin.transform
	# Real high-speed descent packets and independent invalid controller input.
	await relocate(map.spawn+Vector3.UP*220)
	var at:Vector3=net._position(1)
	for i in 30:
		at+=Vector3(.15,-5.0,0);net._last_pose[1]=net.poses[1].duplicate(true);net._last_pose[1].time=net.now()-.04
		net._accept_pose(1,pose(at,i%2==0));check(net._position(1).distance_to(at)<.001,"fast fall body accepted sample "+str(i))
	check(net.pose_stats_r14.sanitized_hands>=15,"invalid tracked hand sanitized without freezing body")
	check(net.poses[1].right.origin.distance_to(at)<5,"sanitized hand remains near valid body")
	var origin:Vector3=preload("res://scripts/map_catalog1000.gd").info(str(net.rules.map)).origin
	var rectangle:=Rect2(Vector2(at.x-origin.x,at.z-origin.z)-Vector2.ONE,Vector2.ONE*2)
	net.zone={"from":rectangle,"next":rectangle,"hold_until":net.now()+100,"ends":net.now()+200,"index":0,"final":true};net.rules.zone_enabled=true
	net.players[1].health=100.0;net.players[1].protection_until=0.0;net._last_tick=0.0;net._process(.15)
	check(net.players[1].health==100.0,"zone uses current accepted fall position, not abandoned spawn")
	at+=Vector3(5,0,0);net._last_pose[1].time=net.now()-.1;net._accept_pose(1,pose(at,true));net._last_tick=0.0;net._process(.15)
	check(net.players[1].health<100.0,"moving current body outside zone applies authoritative damage")
	net.zone.clear();net.rules.zone_enabled=false
	# Repeated impossible movement corrects the client, rather than leaving stale zone coordinates.
	net._budget.erase(-1) # Synthetic burst above must not consume the recovery test packet budget.
	var revision:int=net.players[1].spawn_revision
	net._last_pose[1]=net.poses[1].duplicate(true);net._last_pose[1].time=net.now();net._accept_pose(1,pose(at+Vector3(1000,0,0)))
	await create_timer(.28).timeout
	net._last_pose[1].time=net.now();net._accept_pose(1,pose(at+Vector3(1000,0,0)))
	check(net.players[1].spawn_revision>revision and net.pose_stats_r14.corrected>0,"persistent rejected pose issues authoritative recovery")
	# Door and window contacts use accepted match body updates, with actual source panels.
	var tested:Dictionary={}
	for panel in map.find_children("*","StaticBody3D",true,false):
		if not panel.has_meta("archive_breakable_r8"):continue
		var kind:String="window" if panel.glass_r10 else "door"
		if tested.has(kind):continue
		var center:Vector3=panel.to_global(panel.box.get_center())-Vector3.UP*.9
		await relocate(center)
		net._accept_pose(1,pose(center,true));panel._physics_process(.2)
		check(panel._broken and panel.collision_layer==0 and not panel.view.visible,"actual match "+kind+" contact removes complete visual/collision")
		tested[kind]=true
		if tested.size()==2:break
	check(tested.size()==2,"Alpha has real source doors and glass in test")
	# Actual authoritative build request, retaining world support and reach validation.
	var built=false;var builder:Node=game.field_builder
	for site in map.loot_points:
		if built:break
		var floor:Vector3=map._floor(site+Vector3.UP,3)
		if not floor.is_finite():continue
		await relocate(floor+Vector3.UP*.12)
		for offset in [Vector3(0,0,-3),Vector3(3,0,0),Vector3(0,0,3),Vector3(-3,0,0)]:
			var target:Vector3=map._floor(floor+offset+Vector3.UP*2,5)
			if not target.is_finite():continue
			var transform:=Transform3D(Basis.IDENTITY,target+Vector3.UP*(builder.PANEL_SIDE*.5-.03))
			var entry:Dictionary={"kind":"wall","at":transform,"key":builder._slot_for(transform,"wall"),"support":false}
			var before:int=net.builds.size();net.request("build",{"entries":[entry]})
			if net.builds.size()>before:built=true;break
	check(built,"actual match building creates authoritative supported wall")
	# Native health face works on the host and does not heal a ghost or a player above the roof.
	check(not map.semantics_r14.health_pads.is_empty(),"Alpha authored health pads recovered")
	if not map.semantics_r14.health_pads.is_empty():
		var health:Dictionary={};var stand:=Vector3.INF
		for candidate in map.semantics_r14.health_pads:
			var floor:Vector3=map._floor(candidate.center+Vector3.UP*.7,2)
			if floor.is_finite() and map.semantics_r14.healing_rate_at(floor+Vector3.UP*.12)>0:health=candidate;stand=floor+Vector3.UP*.12;break
		check(not health.is_empty(),"native health pad has physically standable contact")
		if health.is_empty():health=map.semantics_r14.health_pads[0];stand=health.center+health.normal*.12
		await relocate(stand)
		map.semantics_r14.stations_r18.ready_at.clear();net.station_ready_r18.clear()
		print("HEAL_DIAG point=",stand," rate=",bridge._health_pad_rate_r14(1)," life=",net.players[1].life," position=",net._position(1))
		net.players[1].health=40.0;net.players[1].shield=10.0;var before:float=net.players[1].health
		await create_timer(.35).timeout
		check(net.players[1].health==100 and net.players[1].shield==100,"station instantly fills both health and shield")
		var contact:Dictionary=bridge._station_contact_r18(1)
		check(not contact.is_empty() and net.station_ready_r18.has(contact.key),"host owns shared station recharge")
		if not contact.is_empty():
			check(float(net.station_ready_r18[contact.key])-net.now()>14.4,"station has15second cooldown")
			net.players[1].health=50.0;net.players[1].shield=20.0;await create_timer(.25).timeout
			check(net.players[1].health==50 and net.players[1].shield==20,"cooldown prevents repeat restoration")
			net.station_ready_r18[contact.key]=net.now()-.1;await create_timer(.25).timeout
			check(net.players[1].health==100 and net.players[1].shield==100,"station becomes usable after recharge")
		net.players[1].life="downed";before=net.players[1].health;await create_timer(.25).timeout
		check(net.players[1].health==before,"pad cannot revive or heal ghost")
		net.players[1].life="alive";await relocate(health.center+Vector3.UP*4)
		check(bridge._health_pad_rate_r14(1)==0,"pad cannot heal a different floor")
	# Dive and tilted impulse run through actual player physics, with calibration unchanged.
	actor.input_enabled=true;actor.global_position=map.spawn+Vector3.UP*200;actor.reset_motion();actor.velocity=Vector3(0,-1,0);actor._dive_forward_r14=true
	for i in 49:actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,false,false,.02)
	check(not actor.diving_r14,"dive does not start before one second of falling")
	for i in 25:actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,true,false,.02)
	check(actor.diving_r14 and not actor.gliding and actor.velocity.y < -45,"forward after fall arms fast dive and overrides glide")
	actor._dive_forward_r14=false;actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,true,false,.02)
	check(not actor.diving_r14 and actor.gliding,"release forward permits normal glide")
	actor.reset_motion();actor.input_enabled=true
	actor.launch_from_pad_r10(45,Vector3.UP)
	actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,true,false,.10)
	check(not actor.gliding,"pad face clearance still protects initial launch")
	for i in 6:actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,true,false,.10)
	check(actor.gliding and not actor._pad_ascent_r10,"spreading arms cancels ascent after short clearance")
	var normal:=Vector3(.25,.25882,.93301).normalized();actor.reset_motion();actor.input_enabled=true
	check(actor.launch_from_pad_r10(45,normal) and actor.velocity.is_equal_approx(normal*45),"tilted pad launches along authored face normal")
	check(actor.xr_origin.transform.is_equal_approx(calibration),"movement retains calibration transform")
	check(preload("res://scripts/flight_trail0992.gd").RADIUS==.065,"flight trail width is 2.5 times accepted base")
	var marks:Node=game.building_input.team_marks;check(marks.reach>=100000,"ping has no former 80/150 m cap")
	facts={"pose_stats":net.pose_stats_r14.duplicate(true),"doors_and_windows":tested,"native_deathmatch":map.statistics.native_deathmatch_spawns_r14,"native_warmup":map.statistics.native_warmup_spawns_r14,"jump_pads":map.semantics_r14.jump_pads.size(),"health_pads":map.semantics_r14.health_pads.size()}
	bridge.set_process(true);actor.set_physics_process(true);bridge.leave_session();game._quitting=true;game.queue_free();for i in 6:await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/CORE-R14.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"actual Main ENet host and Alpha deathmatch, physics/collision/authority checks; XR off, no headset or Internet acceptance"},"\t"));print("GAMEPLAY_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
