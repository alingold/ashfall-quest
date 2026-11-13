extends SceneTree
var game:Node
var net:Node
var role:String
var checks=0
var failures:Array=[]
var ready_signal=false
var facts:Dictionary={}
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
func verify_dead(label:String):
	check(net.players[1].health==0 and net.players[1].shield==0,label+" zero health/shield replicated")
	if net.me()==1:
		var hud:Node=game.combat_hud.get_node("CompactMatchHUDR3")
		check(game.network_bridge._local_ghost_r13.active,label+" own body/hands are ghost materials")
		check(game.combat_hud.is_visible_in_tree() and hud.life_notice_r13.is_visible_in_tree(),label+" compact local death HUD visible")
		check(game.weapon.is_holstered and not game.revive_paddles.active,label+" local combat equipment stowed")
		check(game.player.collision_layer==0,label+" own ghost cannot intercept projectiles")
		check(game.player.input_enabled and game.player.collision_mask==1,label+" ghost can move without player bumping")
		check(not game.weapon.can_switch() and not game.weapon_inventory._can_interact() and not game.revive_paddles._allowed(),label+" ghost cannot use equipment")
		check(not game.center_label.visible,label+" no repeated large death toast")
	else:
		check(game.network_bridge.avatars.has(1),label+" remote host avatar exists")
		var avatar:Node=game.network_bridge.avatars[1];var friendly:bool=net.allied(net.me(),1)
		check(avatar.is_visible_in_tree()==friendly,label+" body visibility follows team")
		check(avatar.tag.is_visible_in_tree()==friendly and avatar.title.is_visible_in_tree()==friendly,label+" name/bars visibility follows team")
		check(avatar.collision_layer==0 and not avatar.gun.is_visible_in_tree(),label+" ghost has no shot blocker or gun")
		if friendly:
			check(avatar.ghost_r13.active and avatar.health_track.is_visible_in_tree(),label+" ally sees translucent monochrome body and empty health track")
func verify_alive(label:String):
	if net.me()==1:
		check(not game.network_bridge._local_ghost_r13.active,label+" own materials restored")
		check(game.player.collision_layer==2,label+" own collision restored")
		check(game.player.collision_mask==5 and not game.player.get_meta("ghost_r13",false) and game.weapon.can_switch(),label+" ordinary interaction gates and movement mask restored")
		check(not game.combat_hud.get_node("CompactMatchHUDR3").life_notice_r13.visible,label+" death notice cleared")
	else:
		var avatar:Node=game.network_bridge.avatars[1]
		check(avatar.is_visible_in_tree() and not avatar.ghost_r13.active and avatar.collision_layer==4,label+" restored body visible to both teams")
		check(avatar.tag.is_visible_in_tree()==net.allied(net.me(),1),label+" alive tag preserves friendly rule")
func run():
	var args:=OS.get_cmdline_user_args();role=args[0];game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
	net.event_received.connect(func(kind:String,data:Dictionary):
		if kind=="paddles_ready" and int(data.player)==net.me():ready_signal=true)
	if role=="host":
		check(net.host("Ghost Host",int(args[2]),"ghost")==OK,"real listen host")
		net.request("rules",{"map":"range","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"respawn_delay":5.0})
	else:check(net.join("127.0.0.1",role,int(args[2]),"ghost")==OK,"real client joins")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and net.players.size()==3 and not game.network_bridge._preparing),"three peers in shared warmup")
	var calibration:Transform3D=game.player.xr_origin.transform;mark(1)
	if net.server:check(await wait_for(func():return barrier(1)),"all loaded peers ready");net.request("start")
	check(await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing and game.network_bridge.avatars.size()==2),"Team Revive starts with actual avatars")
	var ally=0;var opponent=0
	for id in net.players:
		if id!=1:
			if net.allied(1,id):ally=id
			else:opponent=id
	check(ally!=0 and opponent!=0,"one teammate and one opponent perspective available");facts.viewer_team=net.players[net.me()].team;facts.host_team=net.players[1].team
	mark(2)
	if net.server:
		check(await wait_for(func():return barrier(2)),"both remote viewpoints loaded")
		var at:Vector3=game.active_sandbox_map.spawn
		net._relocate_r12(1,at);net._relocate_r12(ally,at+Vector3(0,0,.25));net.players[1].protection_until=0;net._publish()
		await create_timer(.4).timeout;net.hurt(1,1000,opponent,false,true,7)
	check(await wait_for(func():return net.players[1].life=="downed"),"authoritative death arrives across transport")
	for i in 5:await process_frame
	verify_dead("Team Revive")
	check(net.players[1].deaths==1 and net.players[opponent].kills==1,"single death/kill counters replicated")
	var dead_at:Vector3=net.players[1].spawn
	if net.me()==1:
		var key:=InputEventKey.new();key.physical_keycode=KEY_W;key.pressed=true;Input.parse_input_event(key);await create_timer(.7).timeout;key=InputEventKey.new();key.physical_keycode=KEY_W;key.pressed=false;Input.parse_input_event(key)
	check(await wait_for(func():return net.poses[1].root.origin.distance_to(dead_at)>1),"ordinary ghost walking is accepted and replicated")
	facts.ghost_walk_metres=net.poses[1].root.origin.distance_to(dead_at)
	if not net.server:mark(3)
	if net.server:
		check(await wait_for(func():return barrier(3,true)),"teammate and enemy confirm different ghost visibility")
		net._relocate_r12(ally,net._position(1)+Vector3(0,0,.25));net._publish();await create_timer(.4).timeout
		# Controlled ready charge; actual teammate command still passes the original
		# server life/team/reach/clear-line validator across transport.
		net._ready_until[ally]=net.now()+3.5;net._event("paddles_ready",{"player":ally,"until":net._ready_until[ally]})
	if net.me()==ally:
		check(await wait_for(func():return ready_signal),"teammate receives ready-charge signal");net.request("revive",{"id":1})
	check(await wait_for(func():return net.players[1].life=="alive" and net.players[1].health==50),"actual teammate revive replicated")
	for i in 5:await process_frame
	verify_alive("Revived");check(game.player.xr_origin.transform.is_equal_approx(calibration),"revive retains calibration");mark(4)
	if net.server:check(await wait_for(func():return barrier(4)),"all peers confirm restoration");net.request("home")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not game.network_bridge._preparing),"round ends in shared warmup")
	mark(5)
	if net.server:
		check(await wait_for(func():return barrier(5)),"all warmup peers reset")
		net.request("rules",{"mode":"respawn"});net.request("start")
	check(await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing),"deathmatch starts")
	mark(6)
	if net.server:
		check(await wait_for(func():return barrier(6)),"deathmatch peers ready");net.players[1].protection_until=0;net.hurt(1,1000,opponent,false,true,0)
	check(await wait_for(func():return net.players[1].life=="eliminated"),"deathmatch eliminated ghost arrives")
	for i in 5:await process_frame
	verify_dead("Deathmatch")
	if net.me()==1:check(game.combat_hud.get_node("CompactMatchHUDR3").life_notice_r13.text.contains("RESPAWN"),"deathmatch HUD counts down respawn")
	check(await wait_for(func():return net.players[1].life=="alive",12),"ordinary timed respawn across transport")
	for i in 5:await process_frame
	verify_alive("Respawned");check(game.player.xr_origin.transform.is_equal_approx(calibration),"respawn retains calibration");mark(7)
	if net.server:check(await wait_for(func():return barrier(7)),"all peers verify respawn");net.request("home")
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not game.network_bridge._preparing),"final return to warmup")
	mark(5);check(await wait_for(func():return barrier(5)),"all peers finish cleanly");await create_timer(1).timeout
	var result={"passed":failures.is_empty(),"checks":checks,"failures":failures,"role":role,"facts":facts,"scope":"three actual Main loopback ENet peers; host death, teammate/enemy ghost visibility, real validated revive with controlled charge, deathmatch ghost/timed respawn, HUD/equipment/calibration and warmup return; XR off, no physical controllers or Internet"}
	var f=FileAccess.open(args[1],FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));print("GHOST_PEERS_R13 ",JSON.stringify(result));game.network_bridge.leave_session();game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
