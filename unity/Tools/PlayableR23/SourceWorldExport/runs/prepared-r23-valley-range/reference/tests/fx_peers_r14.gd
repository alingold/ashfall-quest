extends SceneTree
var game:Node
var net:Node
var bridge:Node
var role:String
var checks:int=0
var failures:Array=[]
var facts:Dictionary={}
var shots:Array=[]
var shot_probe:Dictionary={}
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
func barrier(color:int,others_only:bool=false)->bool:
	for id in net.players:
		if others_only and id==1:continue
		if not net.marks.has(id) or int(net.marks[id].color)<color:return false
	return true
func probe_shot(kind:String,data:Dictionary):
	if kind!="shot" or int(data.player)==net.me():return
	shots.append(data.duplicate(true))
	var index:int=posmod(game._tracer_cursor-1,game.EFFECT_POOL_SIZE);var trace:Node3D=game._tracers[index]
	var start:Vector3=trace.global_position-trace.global_basis.y*.5
	var muzzle:Vector3=game.weapon.weapons[game.weapon.selected].get_shot_transform().origin
	shot_probe={"index":index,"origin":data.origin,"draw_start":start,"local_muzzle":muzzle,"origin_error":start.distance_to(data.origin),"local_separation":start.distance_to(muzzle),"no_local_emitter":game._tracer_emitters[index].get_ref()==null,"remaining":game._tracer_times[index],"width":trace.scale.x,"visible":trace.visible}
func run():
	var args:=OS.get_cmdline_user_args();role=args[0];game=load("res://scripts/main.gd").new();root.add_child(game);bridge=game.network_bridge;net=bridge.net;net.event_received.connect(probe_shot)
	if role=="host":
		check(net.host("FX Host",int(args[2]),"fx14")==OK,"actual listen host")
		net.request("rules",{"map":"range","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"respawn_delay":15.0})
	else:check(net.join("127.0.0.1",role,int(args[2]),"fx14")==OK,"actual client joins")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and net.players.size()==3 and not bridge._preparing),"three Main peers loaded shared warmup")
	mark(1)
	if net.server:check(await wait_for(func():return barrier(1)),"all warmup peers ready");net.request("start")
	check(await wait_for(func():return net.stage=="match" and not bridge._preparing and bridge.avatars.size()==2),"match and actual remote avatars ready")
	game.player.set_physics_process(false);mark(2)
	var revision:int=net.players[net.me()].spawn_revision
	if net.server:
		check(await wait_for(func():return barrier(2)),"all controlled motion rigs ready")
		var base:Vector3=game.active_sandbox_map.spawn+Vector3(0,40,0)
		for id in net.players:
			var i:int=net.players.keys().find(id);net._relocate_r12(id,base+Vector3(i*8,0,0));net.players[id].protection_until=0
			var eid:String="fx14_cx4_"+str(id);var capacity:int=preload("res://scripts/weapon_rarity_r6.gd").capacity(0,1)
			net.entities[eid]={"id":eid,"kind":"weapon","catalog":0,"owner":id,"rarity":1,"ammo":capacity,"capacity":capacity,"reload":"ready","position":Vector3.ZERO,"basis":Basis.IDENTITY};net.players[id].inventory.append(eid)
		net._publish()
	var eid:String="fx14_cx4_"+str(net.me())
	check(await wait_for(func():return int(net.players[net.me()].spawn_revision)>revision and bridge.indices.has(eid)),"authoritative controlled staging and firearm replicated")
	game.weapon.selected=int(bridge.indices[eid]);game.weapon.is_holstered=false;game.weapon._apply_selection();game.player.input_enabled=true;mark(3)
	var max_lengths:Dictionary={};var phase_lengths:Dictionary={"glide":{},"dive":{}};var glide_seen=false;var dive_seen=false;var remote_moved=false
	var original:Vector3=bridge.avatars[1].global_position if net.me()!=1 else Vector3.ZERO
	if net.server:check(await wait_for(func():return barrier(3)),"all observer viewpoints ready")
	var begin:int=Time.get_ticks_msec();var previous:int=begin
	while (Time.get_ticks_msec()-begin<4200 if net.server else int(net.marks.get(1,{}).get("color",0))<4):
		await process_frame
		var now:int=Time.get_ticks_msec();var delta:float=clampf((now-previous)*.001,0,.05);previous=now
		if net.server:
			game.player.global_position+=Vector3(0,-.8,-3)*delta;game.player.velocity=Vector3(0,-.8,-3);game.player.movement_mode="glide" if now-begin<2100 else "dive";game.player.gliding=now-begin<2100;game.player.diving_r14=not game.player.gliding
		else:
			var avatar:Node=bridge.avatars[1];glide_seen=glide_seen or avatar.actor.gliding;dive_seen=dive_seen or avatar.actor.diving_r14;remote_moved=remote_moved or avatar.global_position.distance_to(original)>2
			for i in 2:
				var trail:Node=avatar.wakes_r14.trails[i]
				if trail.mesh.get_surface_count()>0:
					max_lengths[i]=maxf(float(max_lengths.get(i,0)),trail.distance)
					var phase:String="dive" if avatar.actor.diving_r14 else "glide"
					phase_lengths[phase][i]=maxf(float(phase_lengths[phase].get(i,0)),trail.distance)
		if now-begin>15000:check(false,"motion observer timeout");break
	if net.server:mark(4)
	else:
		check(glide_seen and dive_seen,"both glide and dive motion cross actual pose transport")
		check(remote_moved,"remote avatar moves through world")
		for i in 2:
			check(float(max_lengths.get(i,0))>3,"remote elbow "+str(i)+" produces moving wake mesh")
			check(float(phase_lengths.glide.get(i,0))>1 and float(phase_lengths.dive.get(i,0))>1,"remote elbow "+str(i)+" has geometry in both glide and dive")
		facts.wake_lengths=max_lengths;facts.phase_wake_lengths=phase_lengths;mark(4)
	if net.server:
		check(await wait_for(func():return barrier(4)),"both observers saw moving elbow geometry")
		var origin:Vector3=game.weapon.weapons[game.weapon.selected].get_shot_transform().origin
		bridge.shot(origin,[Vector3.FORWARD]);facts.shot_origin=origin
	else:
		check(await wait_for(func():return shots.size()>0),"remote shot event crosses ENet and creates tracer")
		check(shot_probe.visible and shot_probe.no_local_emitter,"remote tracer is visible without local muzzle follower")
		check(float(shot_probe.origin_error)<.001 and float(shot_probe.local_separation)>4,"remote tracer begins at shooter rather than viewer gun")
		check(is_equal_approx(float(shot_probe.remaining),.09) and is_equal_approx(float(shot_probe.width),1.8),"remote tracer uses longer thicker visibility settings")
		await process_frame
		var trace:Node3D=game._tracers[shot_probe.index];var start:Vector3=trace.global_position-trace.global_basis.y*.5
		check(start.distance_to(shot_probe.origin)<.001,"effect update does not drag remote ray to local weapon")
		facts.shot_probe=shot_probe
	mark(5)
	if net.server:
		check(await wait_for(func():return barrier(5)),"all peers confirmed remote tracer")
		net.players[1].protection_until=0;var opponent:int=0
		for id in net.players:
			if not net.allied(1,id):opponent=id;break
		net.hurt(1,1000,opponent,false,true,0)
	check(await wait_for(func():return net.players[1].life=="eliminated"),"authoritative ghost state arrives")
	for i in 5:await process_frame
	if net.me()!=1:
		var avatar:Node=bridge.avatars[1];var friendly:bool=net.allied(net.me(),1)
		check(avatar.is_visible_in_tree()==friendly,"ghost body preserves team-only visibility")
		check(avatar.wakes_r14.is_visible_in_tree()==friendly,"ghost wake hierarchy preserves team-only visibility")
		for trail in avatar.wakes_r14.trails:check(trail.visual.is_visible_in_tree()==friendly,"actual world-space ghost trail visual preserves team-only visibility")
		check(avatar.tag.is_visible_in_tree()==friendly and avatar.collision_layer==0,"ghost friendly tag and no enemy projectile blocker preserved")
		facts.ghost_viewer_allied=friendly
	# Dead players intentionally cannot ping. Surviving observers acknowledge the
	# visibility result; the host remains dead for this barrier.
	if not net.server:mark(6)
	check(await wait_for(func():return barrier(6,true)),"both observers complete FX and ghost checks");await create_timer(.5).timeout
	var result={"passed":failures.is_empty(),"checks":checks,"failures":failures,"role":role,"facts":facts,"scope":"Three actual Main loopback ENet peers; controlled physics-off moving host poses through regular bridge transport, actual remote avatar/wake mesh, validated bridge shot and remote tracer, team-only ghost visibility; XR off, Dummy audio, no microphone or Internet/headset acceptance"}
	var f:=FileAccess.open(args[1],FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));print("FX_PEERS_R14 ",JSON.stringify(result));bridge.leave_session();game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
