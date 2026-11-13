extends SceneTree
var game:Node
var net:Node
var avatar:Node
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
func meshes()->Array:
	var result:Array=[]
	for item in [avatar.suit,avatar.head,avatar.left,avatar.right]:result.append_array(item.find_children("*","MeshInstance3D",true,false))
	return result
func check_ghost(label:String):
	for view in meshes():
		check(view.cast_shadow==GeometryInstance3D.SHADOW_CASTING_SETTING_OFF,label+" no ghost shadow "+view.name)
		for i in view.mesh.get_surface_count():
			var mat:Material=view.get_surface_override_material(i)
			check(mat is ShaderMaterial and mat.shader==preload("res://scripts/ghost_appearance_r13.gd").SHADER,label+" monochrome translucent surface "+view.name)
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net;check(net.host("Viewer",25601,"ghost")==OK,"real Main host")
	net.request("rules",{"map":"range","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0})
	var deadline:int=Time.get_ticks_msec()+30000
	while (net.stage!="lobby" or not net.world_ready or game.network_bridge._preparing) and Time.get_ticks_msec()<deadline:await process_frame
	check(net.world_ready,"loaded in-map warmup");freeze(game);net.stage="match";net.match_started=net.now();net.rules.downed_seconds=10.0
	var original:Dictionary=net.players[1].duplicate(true);var original_pose:Dictionary=net.poses[1].duplicate(true)
	for id in [2,3,4]:
		net.players[id]=original.duplicate(true);net.players[id].name="Ally Ghost" if id==2 else "Player "+str(id);net.players[id].team=0 if id in [2,4] else 1;net.players[id].inventory=[];net.players[id].protection_until=0
		net.poses[id]=original_pose.duplicate(true)
		for key in ["root","head","left","right","gun_pose"]:
			if net.poses[id].get(key) is Transform3D:net.poses[id][key].origin+=Vector3(id*3,0,0)
	net.players[1].team=0;net.players[1].protection_until=0
	avatar=load("res://scripts/net_avatar1000.gd").new();game.add_child(avatar);avatar.setup(2,net,game.network_bridge);game.network_bridge.avatars[2]=avatar
	net.poses[2].holstered=false;avatar._process(.02)
	check(avatar.is_visible_in_tree() and avatar.collision_layer==4,"alive remote visible and hittable")
	check(avatar.tag.is_visible_in_tree() and avatar.gun.is_visible_in_tree(),"alive friendly tag and drawn weapon visible")
	var saved:Array=[]
	for view in meshes():saved.append({"view":view,"mesh":view.mesh,"override":view.material_override,"shadow":view.cast_shadow})
	var kills:int=net.players[3].kills;net.hurt(2,1000,3,false,true,7)
	avatar._process(.02)
	check(net.players[2].life=="downed" and net.players[2].health==0 and net.players[2].shield==0,"lethal hit creates zero-pool downed ghost")
	check(net.players[2].deaths==1 and net.players[3].kills==kills+1,"one death and one enemy kill recorded")
	check(avatar.is_visible_in_tree() and avatar.tag.is_visible_in_tree() and avatar.title.text=="Ally Ghost","teammate sees ghost and name")
	check(avatar.health_track.is_visible_in_tree() and avatar.shield_track.is_visible_in_tree() and avatar.pools.text=="HP 0   SHIELD 0","empty friendly health/shield tracks remain visible")
	check(avatar.collision_layer==0 and not avatar.gun.is_visible_in_tree(),"ghost blocks no shots and draws no weapon")
	check_ghost("downed")
	net.hurt(2,1000,3,false,true,7);check(net.players[2].deaths==1 and net.players[3].kills==kills+1,"repeated ghost damage cannot double-count death/kill")
	net.players[1].team=1;avatar._process(.02)
	check(not avatar.is_visible_in_tree() and not avatar.title.is_visible_in_tree() and not avatar.health_track.is_visible_in_tree(),"opponent sees no ghost body, name or bars")
	net.players[1].team=0;avatar._process(.02)
	check(avatar.is_visible_in_tree() and avatar.can_revive(),"friendly perspective restores ghost/revive target")
	# Exercise the original revive authority and reach validation, without faking a life-state RPC.
	var chest:Vector3=net.poses[2].head.origin-Vector3.UP*.6
	net.poses[1].left.origin=chest+Vector3(-.13,0,.2);net.poses[1].right.origin=chest+Vector3(.13,0,.2);net._ready_until[1]=net.now()+3;net.request("revive",{"id":2});avatar._process(.02)
	check(net.players[2].life=="alive" and net.players[2].health==50,"authorized teammate revive succeeds")
	check(not avatar.ghost_r13.active and avatar.collision_layer==4,"revive restores opaque collidable alive avatar")
	for row in saved:check(row.view.mesh==row.mesh and row.view.material_override==row.override and row.view.cast_shadow==row.shadow,"revive preserves original mesh/material/shadow "+row.view.name)
	# Every accepted style supports death and restoration, including style replacement while dead.
	net.hurt(2,1000,3,false,true,7)
	for style in 7:
		net.players[2].style=style;avatar._process(.02);check_ghost("style "+str(style))
	net.players[2].downed_at=net.now()-11;net._process(.11);net.set_process(false);avatar._process(.02)
	check(net.players[2].life=="eliminated" and avatar.is_visible_in_tree() and avatar.tag.is_visible_in_tree(),"bleed-out keeps friendly ghost and tag")
	check(not avatar.can_revive(),"eliminated ghost obeys existing revive expiry")
	net.players[1].team=1;avatar._process(.02);check(not avatar.is_visible_in_tree(),"opponent also cannot see eliminated ghost");net.players[1].team=0
	# Own ghost retains a compact visible HUD; no persistent giant center toast.
	net.hurt(1,1000,3,false,true,0);game.network_bridge._sync_player();game.combat_hud.refresh(0)
	var hud:Node=game.combat_hud.get_node("CompactMatchHUDR3");hud._process(0)
	check(game.combat_hud.is_visible_in_tree() and hud.life_notice_r13.is_visible_in_tree(),"death notice parent HUD remains visible while dead")
	check(hud.life_notice_r13.text.contains("GHOST") and hud.life_notice_r13.no_depth_test,"compact ghost notice above HUD cannot clip behind geometry")
	check(game.network_bridge._local_ghost_r13.active and game.weapon.is_holstered and not game.revive_paddles.active,"own body/hands ghosted; combat equipment stowed")
	check(game.player.collision_layer==0,"own ghost cannot physically intercept enemy grenades")
	check(game.player.input_enabled and game.player.collision_mask==1,"ghost locomotion enabled with solid-world collision and no player bumping")
	check(not game.weapon.can_switch() and not game.weapon_inventory._can_interact() and not game.revive_paddles.equip(),"moving ghost cannot draw, collect or use paddles")
	var gun:Node=game.weapon.weapons[game.weapon.selected];var ammo:int=gun.ammo;game.weapon.fire();check(gun.ammo==ammo and not gun._input_allowed(),"moving ghost cannot discharge its weapon")
	var key:=InputEventKey.new();key.physical_keycode=KEY_W;key.pressed=true;Input.parse_input_event(key);var before_move:Vector3=game.player.global_position;game.player.set_physics_process(true)
	for i in 60:await physics_frame
	key=InputEventKey.new();key.physical_keycode=KEY_W;key.pressed=false;Input.parse_input_event(key);game.player.set_physics_process(false)
	check(game.player.global_position.distance_to(before_move)>.5,"ghost really walks over multiple ordinary physics steps")
	check(game.toast_time==0,"death does not repeat a large center toast")
	check(hud.feeds[0].text.contains("knocked down"),"Team Revive feed names knockdown correctly")
	# Death during an actual R12 pod journey must release the carrier into ghost movement.
	net._spawn(1);net.players[1].protection_until=0;var launch:Node=game.network_bridge.launch_r12
	launch.spawn_for(1,net.players);net._relocate_r12(1,launch.pod_pose(1).origin+Vector3.UP*.12);net._publish();net.request("pod_launch",{"pod":net.players[1].pod_key_r12})
	check(net.pods_r12.has(1) and net.pods_r12[1].state=="flying","death fixture boards an actual authorized pod")
	net.hurt(1,1000,3,false,true,7);net._step_pods_r12();launch._physics_process(.016)
	check(net.players[1].life=="downed" and net.pods_r12[1].state=="released" and not game.player.pod_carried_r12,"death in transit releases pod without reviving passenger")
	check(game.player.input_enabled and game.player.get_meta("ghost_r13",false) and game.weapon.is_holstered,"ejected ghost retains locomotion and equipment lock")
	# Deathmatch expiry uses ordinary supported respawn and restores visibility/materials.
	net.rules.mode="respawn";net._spawn(1);net._spawn(2);net.players[2].protection_until=0;net.hurt(2,1000,3,false,true,7);avatar._process(.02)
	check(net.players[2].life=="eliminated" and avatar.tag.is_visible_in_tree(),"deathmatch wait is a friendly ghost")
	net.players[2].respawn_at=net.now()-.1;net._process(.11);net.set_process(false);avatar._process(.02)
	check(net.players[2].life=="alive" and not avatar.ghost_r13.active and avatar.collision_layer==4,"deathmatch respawn restores live appearance")
	net.players[2].protection_until=0;net.poses[2].root.origin.y=-30;var deaths:int=net.players[2].deaths;net._process(.11);net.set_process(false)
	check(net.players[2].life=="eliminated" and net.players[2].health==0 and net.players[2].shield==0 and net.players[2].deaths==deaths+1,"fall death uses zero pools and one death")
	check(not net.combat_feed[0].downed and net.combat_feed[0].victim==2,"fall death also enters HUD notification feed")
	check(net.poses[2].root.origin.y>=-25 and net.players[2].health==0,"fallen moving ghost returns to supported arena without being revived")
	net.rules.teams=0;avatar._process(.02);check(not avatar.is_visible_in_tree(),"free-for-all hides every other ghost")
	facts={"skin_styles":7,"remote_meshes":meshes().size(),"lightweight":preload("res://scripts/asset_quality.gd").lightweight()}
	game.network_bridge.leave_session();check(not game.network_bridge._local_ghost_r13.active and game.player.collision_layer==2 and game.player.collision_mask==5 and not game.player.has_meta("ghost_r13"),"leaving restores local materials/collision and clears ghost input state");game._quitting=true;game.queue_free();for i in 3:await process_frame
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+("/GHOST-LOW-R13.json" if facts.lightweight else "/GHOST-R13.json"),FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"actual Main/avatars/physics/HUD plus controlled authoritative roster, damage and original revive/respawn methods; no XR or real transport"},"\t"));print("GHOST_R13 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
