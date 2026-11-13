extends Node
var game:Node
var net:Node
var role:String
var output:String
var port:int
var mode:="before"
var checks:=0
var failures:Array=[]
var started:=0.0
var match_clock:=-1.0
var late_packet:Dictionary={}
var label:Label
var state_label:Label
var ghost_count:=0
var observed:Dictionary={}
var done:=false
func stamp()->float:return Time.get_ticks_msec()*.001
func check(ok:bool,text:String)->void:
	checks+=1
	if not ok:failures.append(text);print("FAIL ",role," ",text)
func wait_for(condition:Callable,text:String,seconds:=12.0)->bool:
	var until:=stamp()+seconds
	while stamp()<until:
		if condition.call():return true
		await get_tree().create_timer(.03).timeout
	check(false,text+" timeout");return false
func run()->void:
	Engine.max_fps=30
	var args:=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);mode=args[4] if args.size()>4 else "before";started=stamp()
	game=preload("res://scripts/main.gd").new();game.name="AshfallGame";get_tree().root.add_child(game)
	for i in 4:await get_tree().process_frame
	net=game.network_bridge.net
	if mode=="reconnect":
		net.changed.connect(_observe_health)
		net.disconnected.connect(func(_reason:String):
			if role!="server" and not done:observed.transport_loss=true;_rejoin_client())
	if role=="server":
		check(net.host("Host",port,"capture")==OK,"actual game listen host started")
		net.request("rules",{"map":"range","mode":"respawn","teams":0,"time_limit":0,"score_limit":0,"protection":0.0})
		if mode=="reconnect":await reconnect_sequence()
		else:await server_sequence()
	else:
		_make_caption()
		game.player.vertical_combat_world_baseline_m=1.71;game.player.vertical_combat_tracking_baseline=1.64
		await get_tree().create_timer(.7).timeout
		check(net.join("127.0.0.1","Guest",port,"capture")==OK,"actual game client join")
		await wait_for(func():return done,"client capture completion",70)
		if not done:finish()
func _make_caption()->void:
	var canvas:=CanvasLayer.new();canvas.layer=250;add_child(canvas)
	var backing:=ColorRect.new();backing.color=Color(.015,.02,.025,.94);backing.size=Vector2(1280,118);canvas.add_child(backing)
	label=Label.new();label.position=Vector2(18,10);label.add_theme_font_size_override("font_size",21);label.text="ASHFALL / "+mode.to_upper()+" / LOCAL MULTIPLAYER VALIDATION\nReal client render | scripted inputs | XR OFF | loopback only";canvas.add_child(label)
	state_label=Label.new();state_label.position=Vector2(18,67);state_label.add_theme_font_size_override("font_size",23);canvas.add_child(state_label)
func _process(_delta:float)->void:
	if net==null:return
	if net.active and net.players.size()==2 and net.stage=="lobby" and match_clock<0 and not observed.has("ready"):
		observed.ready=true;net.request("ready")
	if net.stage=="match" and not game.network_bridge._preparing and match_clock<0:match_clock=stamp();observed.match=true
	if mode=="reconnect" and net.stage=="match" and not game.network_bridge._preparing:
		var epochs:Array=observed.get("epochs",[])
		if not epochs.has(int(net.epoch)):epochs.append(int(net.epoch));observed.epochs=epochs
	if label!=null:
		var count:=0
		for value in game.network_bridge._projectiles.values():
			if is_instance_valid(value):count+=1
		state_label.text="STATE: "+net.stage.to_upper()+"   LIVE PROJECTILES: "+str(count)+"   "+str(observed.get("message","Waiting for READY / loading"))
		state_label.modulate=Color("ff6262") if net.stage=="lobby" and count>0 else Color("74f0a5")
		if net.stage=="lobby" and observed.has("injected"):ghost_count=maxi(ghost_count,count)
@rpc("authority","call_remote","reliable",0)
func note(text:String)->void:
	observed.message=text
	if text.begins_with("Injected"):observed.injected=true
func server_sequence()->void:
	if not await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing,"actual match ready",30):finish();return
	await get_tree().create_timer(1).timeout
	check(game.network_bridge.avatars.size()==1,"remote avatar created in real game")
	var origin:Vector3=game.player.global_position+Vector3(0,1.5,-2)
	net._event("projectile",{"id":8801,"player":1,"kind":"smaw","origin":origin,"velocity":Vector3(0,0,-.2)})
	await get_tree().create_timer(.3).timeout
	late_packet=net.projectile_poses[8801].duplicate(true)
	note.rpc("Rocket exists during MATCH")
	await get_tree().create_timer(1.2).timeout
	net.request("home");note.rpc("Returned home; the active world should be cleared")
	await get_tree().create_timer(1).timeout
	# Replay an authentic stored match packet over the real transport after its lobby snapshot.
	# The delay is injected by this fixture; no claim is made about Internet latency or loss.
	net._flight.rpc(late_packet);net._event_rpc.rpc("projectile",late_packet);note.rpc("Injected delayed prior-match packet")
	await get_tree().create_timer(2).timeout
	check(net.projectile_poses.is_empty(),"host clears projectile snapshot on home")
	finish_remote.rpc();await get_tree().create_timer(2.0).timeout;finish()
func _rejoin_client()->void:
	await get_tree().create_timer(.65).timeout
	if done:return
	check(not net.active and game.front_end_active,"real disconnect returns client to menu")
	check(net.join("127.0.0.1","Guest",port,"capture")==OK,"same client requests reconnect")
func _observe_health()->void:
	if role=="server" or not net.players.has(net.me()) or net.stage!="match":return
	var state:Dictionary=net.players[net.me()]
	if state.life=="downed" and game.dead:observed.actual_downed=true
	if state.life=="alive" and state.health==50 and game.health==50 and not game.dead:observed.actual_revived=true
@rpc("authority","call_remote","reliable",0)
func inspect_reconnect()->void:
	await wait_for(func():return not game.network_bridge._preparing and net.players.has(net.me()) and net.players[net.me()].loaded,"client restored world")
	var state:Dictionary=net.players[net.me()];var eid:String=state.inventory[0]
	var index:int=game.network_bridge.indices.get(eid,-1)
	check(index>=0,"reconnected entity has a real local gun instance")
	if index>=0:
		var gun:Node=game.weapon.weapons[index];check(gun.ammo==0 and gun.reload_state=="needs_mag","reconnected empty gun stays empty and unloaded")
	check(game.weapon.reserves.pistol==1048576 and game.field_builder.charges==1000006,"real reserve/build providers restored")
	check(game.player.vertical_combat_world_baseline_m==1.71 and game.player.vertical_combat_tracking_baseline==1.64,"simulated calibration values retained across disconnect")
	check(observed.get("transport_loss",false),"client observed actual ENet loss")
	check(observed.get("actual_downed",false) and observed.get("actual_revived",false),"real client applied downed and revived health/input state")
	reconnect_proof.rpc_id(1)
@rpc("any_peer","call_remote","reliable",0)
func reconnect_proof()->void:
	if role=="server" and net.players.has(multiplayer.get_remote_sender_id()):observed.client_checked=true
func reconnect_sequence()->void:
	if not await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing,"reconnect fixture match",30):finish();return
	await get_tree().create_timer(.5).timeout
	var guest:int=0
	for id in net.players:
		if id!=1:guest=int(id)
	var prior:Dictionary=net.players[guest].duplicate(true);var eid:String=prior.inventory[0]
	# Use actual avatar contact validation, with nearby positions and the charged timer scripted.
	net.rules.mode="revive";net.rules.teams=2
	for state in net.players.values():state.team=0
	var close_spawn:Vector3=game.player.global_position+Vector3.UP*.2
	net.spawn_provider=func(id:int,_players:Dictionary,_rules:Dictionary)->Vector3:return close_spawn+Vector3(.45 if id!=1 else 0.0,0,0)
	net._spawn(1);net._spawn(guest);net._publish();await get_tree().create_timer(.3).timeout
	await wait_for(func():return net.poses[1].root.origin.distance_to(net.poses[guest].root.origin)<1.0,"scripted nearby revive positions",3)
	net.hurt(guest,250,0);check(net.players[guest].life=="downed","actual game enters teammate downed state")
	await get_tree().create_timer(.25).timeout
	net._ready_until[1]=net.now()+3.5;var charge_until:float=net._ready_until[1]
	check(game.network_bridge.avatars[guest].can_revive(),"real remote avatar exposes teammate revive")
	var host_pose:Dictionary=net.poses.get(1,{});var guest_pose:Dictionary=net.poses.get(guest,{})
	if not host_pose.is_empty() and not guest_pose.is_empty():
		var chest:Vector3=guest_pose.head.origin+Vector3.DOWN*.6
		print("REVIVE_DIAGNOSTIC ",JSON.stringify({"hostRoot":str(host_pose.root.origin),"guestRoot":str(guest_pose.root.origin),"hostSpawn":str(net.players[1].spawn),"guestSpawn":str(net.players[guest].spawn),"distanceLeft":host_pose.left.origin.distance_to(chest),"distanceRight":host_pose.right.origin.distance_to(chest),"clearLeft":game.network_bridge._clear(host_pose.left.origin,chest),"clearRight":game.network_bridge._clear(host_pose.right.origin,chest),"valid":game.network_bridge._validate("revive",1,{"target":guest})}))
	game.network_bridge.avatars[guest].revive()
	check(net.players[guest].life=="alive" and net.players[guest].health==50,"real geometry-validated avatar revive succeeds")
	check(net._ready_until[1]==charge_until,"actual avatar revive keeps charge window")
	await get_tree().create_timer(.25).timeout
	net.rules.mode="respawn";net._spawn(guest);net._publish()
	net.entities[eid].ammo=0;net.entities[eid].reload="needs_mag";net.entities[eid].seated=0
	net.players[guest].reserves.pistol=1048576;net.players[guest].builds=1000006;net._publish()
	await get_tree().create_timer(.7).timeout
	var initial_rack:int=game.weapon.weapons.size()
	net.multiplayer.multiplayer_peer.disconnect_peer(guest)
	await wait_for(func():return not net.players.has(guest),"actual server disconnect")
	await wait_for(func():return net.players.size()==2,"same client reconnects",20)
	var next_guest:int=0
	for id in net.players:
		if id!=1:next_guest=int(id)
	check(next_guest!=guest,"full game reconnect changes peer ID")
	check(net.players[next_guest].inventory==prior.inventory and net.players[next_guest].token==prior.token,"full game reconnect retains identity and entity IDs")
	await wait_for(func():return net.players[next_guest].loaded,"reconnect loading barrier",20)
	inspect_reconnect.rpc();await wait_for(func():return observed.get("client_checked",false),"client actual-state proof")
	for round_no in range(2,5):
		net.request("restart")
		await wait_for(func():return net.stage=="match" and net.epoch==round_no and not game.network_bridge._preparing,"real repeated match",20)
		await get_tree().create_timer(.5).timeout
		check(game.weapon.weapons.size()<=initial_rack+3,"real weapon rack remains bounded on round "+str(round_no))
		check(game.weapon_inventory.owned.size()==3,"real host inventory rebuilt on round "+str(round_no))
	net.request("home");await get_tree().create_timer(.5).timeout;finish_remote.rpc();await get_tree().create_timer(2).timeout;finish()
@rpc("authority","call_remote","reliable",0)
func finish_remote()->void:
	if mode=="reconnect":
		check(observed.get("epochs",[]).size()==4,"real client entered all four match epochs")
	check(observed.has("match"),"client reached actual map")
	check(net.stage=="lobby" and game.front_end_active,"client returned to actual pedestal menu")
	check(ghost_count==0,"delayed packets do not resurrect lobby projectiles")
	observed.message="PASS: no ghost projectile" if ghost_count==0 else "REPRODUCED: old projectile returned in lobby"
	await get_tree().create_timer(1.3).timeout;finish()
func finish()->void:
	if done:return
	done=true
	var scope:String="actual game processes and client view; scripted inputs; authentic packet replay with deliberately injected delay; XR off; local loopback, not Internet/headset proof"
	if mode=="reconnect":scope="two actual game processes; geometry-validated avatar revive with seeded charged window, ENet disconnect/reconnect, empty real weapon, reserves, simulated calibration and four match epochs; loopback only, no headset or Internet proof. Graphics/recording mode is specified by launch arguments."
	var report:Dictionary={"role":role,"mode":mode,"checks":checks,"failures":failures,"passed":failures.is_empty(),"ghostProjectiles":ghost_count,"seconds":stamp()-started,"scope":scope}
	var file:=FileAccess.open(output,FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close();print("GAME_REPRO1001 ",JSON.stringify(report))
	net.close(false);game._quitting=true;game.queue_free();get_tree().quit(0 if failures.is_empty() else 1)
