extends Node
const Session=preload("res://scripts/net_session1000.gd")
var net:Node
var role:String
var output:String
var port:int
var client_count:int
var checks:=0
var failures:Array=[]
var phases:Array=[]
var acks:Dictionary={}
var pose_serial:=0
var _pose_clock:=0.0
var loading_epoch:=-1
var started:=0.0
var finished:=false
var late_combat:=0
var advance_after_disconnect:=false
var former_peer:=0
var former_token:=""
var before_reconnect:Dictionary={}
func stamp()->float:return Time.get_ticks_msec()*.001
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",role," ",label)
func peer(label:String)->int:
	for id in net.players:
		if net.players[id].name==label:return int(id)
	return 0
func wait_for(condition:Callable,label:String,seconds:float=9.0)->bool:
	var until:float=stamp()+seconds
	while stamp()<until:
		if condition.call():return true
		await get_tree().create_timer(.025).timeout
	check(false,label+" timed out at "+net.stage+" players="+str(net.players.size()));return false
func run()->void:
	var args:=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);client_count=int(args[3]);started=stamp()
	net=Session.new();get_tree().root.add_child(net)
	# Protocol/state fixture, not a geometry or headset simulation. Real-game tests run separately.
	net.validator=func(_action:String,_id:int,_data:Dictionary)->bool:return true
	net.spawn_provider=func(id:int,_players:Dictionary,_rules:Dictionary)->Vector3:return Vector3((id%2)*.5,.2,0)
	net.event_received.connect(func(kind:String,_data:Dictionary):
		if net.stage!="match" and kind in ["projectile_pose","projectile","shot"]:late_combat+=1)
	net.disconnected.connect(func(_reason:String):
		if role!="server" and phases.has("host_loss"):check(not net.active and net.players.is_empty(),"host loss clears the local session");finish())
	if role=="server":
		check(net.host("Dedicated",port,"matrix",true)==OK,"dedicated socket starts")
		await server_sequence()
	else:
		await get_tree().create_timer(.2+float(role.substr(1))*.10).timeout
		check(net.join("127.0.0.1",role,port,"matrix")==OK,"loopback join requested")
		while not finished and stamp()-started<100:await get_tree().create_timer(.05).timeout
		if not finished:check(false,"client watchdog");finish()
func _process(delta:float)->void:
	if net==null or not net.active:return
	if role=="server" and advance_after_disconnect and net.players.size()==client_count-1:
		advance_after_disconnect=false;phase.rpc_id(net.admin,"restart",{})
	if net.stage=="loading" and loading_epoch!=int(net.epoch):
		loading_epoch=int(net.epoch)
		if role=="server":net.register_world([])
		else:net.request("loaded",{"epoch":net.epoch})
	if role=="server" or net.stage!="match" or not net.players.has(net.me()):return
	_pose_clock+=delta
	if _pose_clock<.04:return
	_pose_clock=0;pose_serial+=1
	var p:Dictionary=net.players[net.me()];var at:Vector3=p.get("spawn",Vector3(0,.2,0))
	net.send_pose({"root":Transform3D(Basis.IDENTITY,at),"head":Transform3D(Basis.IDENTITY,at+Vector3.UP*1.7),"left":Transform3D(Basis.IDENTITY,at+Vector3(-.25,1.2,-.2)),"right":Transform3D(Basis.IDENTITY,at+Vector3(.25,1.2,-.2)),"weapon":0,"holstered":false,"seq":pose_serial})
func command_all(kind:String,data:Dictionary={})->void:
	acks.clear();phase.rpc(kind,data)
	await wait_for(func():return acks.size()>=client_count,"ack "+kind)
@rpc("authority","call_remote","reliable",0)
func phase(kind:String,data:Dictionary)->void:
	phases.append(kind)
	match kind:
		"configure":
			if net.is_host():net.request("rules",{"map":"range","mode":"respawn","teams":2,"protection":0.0,"score_limit":0,"time_limit":0,"respawn_delay":1.0})
		"ready":net.request("ready")
		"pickup":net.request("pickup",{"id":data.id})
		"drop":
			if net.entities.has(data.id) and int(net.entities[data.id].owner)==net.me():net.request("drop",{"id":data.id,"position":Vector3(0,.7,-1)})
		"steal":
			if int(net.entities[data.id].owner)!=net.me():net.request("drop",{"id":data.id,"position":Vector3.ZERO});net.request("reload",{"id":data.id,"state":"ready"})
		"build":
			if role=="p1":net.request("build",{"entries":[{"kind":"wall","key":"qa_wall","at":Transform3D(Basis.IDENTITY,Vector3(0,1,-2)),"support":true}]})
		"revive":
			if role=="p1":net.request("revive",{"id":int(data.target)})
		"home":
			if net.is_host():net.request("home")
		"restart":
			if net.is_host():net.request("restart")
		"duplicate_loaded":
			if role=="p2":net.request("loaded",{"epoch":net.epoch})
		"reconnect","fresh_late_join","reconnect_after_round":
			if role=="p2":
				var token:String=net.reconnect_token;net.close(false)
				await get_tree().create_timer(.6).timeout
				if kind=="fresh_late_join":net.reconnect_token=""
				net.join("127.0.0.1",role,port,"matrix")
				await wait_for(func():return net.active and net.players.has(net.me()),"rejoin handshake")
				check(net.reconnect_token!=token if kind=="fresh_late_join" else net.reconnect_token==token,"identity reservation semantics")
				if net.stage=="match":net.request("loaded",{"epoch":net.epoch})
		"observe":
			check(net.stage==data.stage,"replicated "+str(data.stage))
			if data.has("owner"):check(int(net.entities[data.id].owner)==int(data.owner),"single ownership replicated")
			if data.has("empty"):check(net.entities[data.id].ammo==0 and net.entities[data.id].reload=="needs_charge","unloaded staged reload state preserved")
			if data.has("builds"):check(net.builds.size()==int(data.builds),"build state replicated")
			if data.has("epoch"):check(int(net.epoch)==int(data.epoch),"round epoch replicated")
		"host_loss":pass
		"late_packet_check":check(late_combat==0,"delayed prior-match packets do not reach lobby renderers")
	await get_tree().create_timer(.12).timeout
	if net.active:ack.rpc_id(1,role,kind)
@rpc("any_peer","call_remote","reliable",0)
func ack(label:String,kind:String)->void:
	if role=="server" and peer(label)==multiplayer.get_remote_sender_id():acks[label]=kind
func server_sequence()->void:
	if not await wait_for(func():return net.players.size()==client_count,"all initial clients"):finish();return
	check(net.admin==peer("p1"),"first guest owns dedicated lobby")
	await command_all("configure");await command_all("ready")
	if not await wait_for(func():return net.stage=="match","initial match"):finish();return
	check(net.poses.size()==client_count,"initial poses present")
	var gun:Dictionary={"id":"contested","kind":"weapon","catalog":8,"owner":0,"ammo":0,"capacity":75,"reload":"needs_charge","seated":75,"position":Vector3(0,.7,-1),"basis":Basis.IDENTITY,"rocket_reserve":0}
	net.register_world([gun]);await get_tree().create_timer(.15).timeout;await command_all("pickup",{"id":"contested"})
	var owner:int=net.entities.contested.owner;var claims:=0
	for p in net.players.values():
		if p.inventory.has("contested"):claims+=1
	check(owner>0 and claims==1,"simultaneous pickup has exactly one owner")
	await command_all("steal",{"id":"contested"});check(net.entities.contested.owner==owner and net.entities.contested.ammo==0,"non-owner cannot mutate gun")
	await command_all("observe",{"stage":"match","id":"contested","owner":owner,"empty":true})
	await command_all("drop",{"id":"contested"});check(net.entities.contested.owner==0 and net.entities.contested.reload=="needs_charge","dropping preserves pending M60 state")
	await command_all("pickup",{"id":"contested"});check(net.entities.contested.ammo==0,"repickup does not refill empty gun")
	var reserves_before:=0;var builds_before:=0
	for p in net.players.values():p.reserves.pistol=1000000;p.builds=1000000;reserves_before+=int(p.reserves.pistol);builds_before+=int(p.builds)
	net.register_world([{"id":"ammo_supply","kind":"ammo","family":"pistol","amount":16,"owner":0,"position":Vector3(0,.7,-1),"basis":Basis.IDENTITY},{"id":"build_supply","kind":"builds","amount":6,"owner":0,"position":Vector3(0,.7,-1),"basis":Basis.IDENTITY}]);await get_tree().create_timer(.15).timeout
	await command_all("pickup",{"id":"ammo_supply"});await command_all("pickup",{"id":"build_supply"})
	var reserves_after:=0;var builds_after:=0
	for p in net.players.values():reserves_after+=int(p.reserves.pistol);builds_after+=int(p.builds)
	check(reserves_after-reserves_before==16 and not net.entities.has("ammo_supply"),"ammo supply awards once without cap")
	check(builds_after-builds_before==6 and not net.entities.has("build_supply"),"build supply awards once without cap")
	await command_all("build");check(net.builds.size()==1,"one authoritative build");await command_all("observe",{"stage":"match","builds":1})
	var first:int=peer("p1");var second:int=peer("p2")
	for p in net.players.values():p.team=0
	net.rules.mode="revive";net._publish();var hp:float=net.players[second].health
	net.hurt(second,50,first);check(net.players[second].health==hp and net.players[second].shield==100,"friendly fire disabled")
	net.hurt(second,250,0);check(net.players[second].life=="downed","damage produces authoritative downed state")
	net._ready_until[first]=net.now()+3.5;var until:float=net._ready_until[first];await command_all("revive",{"target":second})
	check(net.players[second].life=="alive" and net.players[second].health==50,"client command revives teammate")
	check(net._ready_until[first]==until,"revive retains original ready window")
	net.rules.mode="respawn";net.players[second].protection_until=0;net.hurt(second,250,0)
	await wait_for(func():return net.players[second].life=="alive","timed respawn")
	check(net.players[second].health==100,"respawn resets health")
	net.players[second].health=37;net._publish();await command_all("duplicate_loaded")
	check(net.players[second].health==37,"duplicate loading acknowledgment cannot respawn/heal a living player")
	former_peer=second;former_token=net.players[second].token;before_reconnect=net.players[second].duplicate(true)
	await command_all("reconnect");second=peer("p2")
	check(second!=former_peer and second>0,"reconnect uses new transport identity")
	check(net.players[second].inventory==before_reconnect.inventory and net.players[second].reserves==before_reconnect.reserves and net.players[second].builds==before_reconnect.builds,"reconnect preserves inventory and reserves")
	for eid in net.players[second].inventory:check(net.entities[eid].owner==second,"reconnect rebinds owned entity")
	await command_all("fresh_late_join");second=peer("p2")
	check(net.players[second].loaded and not net.players[second].spectator,"fresh respawn late join becomes active")
	for token in net._reconnect:net._reconnect[token].until=net.now()-.01
	await get_tree().create_timer(.2).timeout
	check(net._reconnect.is_empty(),"expired reservation cleaned")
	for e in net.entities.values():check(int(e.owner)>=0,"no orphaned reserved ownership")
	net.hurt_build(net.builds.keys()[0],1000);check(net.builds.is_empty(),"build damage removes authoritative build")
	for round_no in range(2,5):
		await command_all("restart");await wait_for(func():return net.stage=="match" and net.epoch==round_no,"repeated match "+str(round_no))
		await command_all("observe",{"stage":"match","epoch":round_no,"builds":0})
		check(net.players.size()==client_count and net.entities.size()==client_count*3,"repeated match has bounded starting entities")
	var before_epoch:int=net.epoch;advance_after_disconnect=true
	await command_all("reconnect_after_round")
	await wait_for(func():return net.stage=="match" and net.epoch>before_epoch,"round advanced during disconnect")
	second=peer("p2");check(net.players[second].inventory.size()==3,"cross-round reconnect receives current loadout")
	for eid in net.players[second].inventory:check(net.entities.has(eid) and int(net.entities.get(eid,{}).get("owner",0))==second,"cross-round reconnect owns existing current-round entity")
	check(net.players[second].builds==int(net.rules.starting_builds),"cross-round reconnect resets round reserves")
	await command_all("observe",{"stage":"match","epoch":before_epoch+1,"builds":0})
	await command_all("home");await wait_for(func():return net.stage=="lobby","return to lobby");await command_all("observe",{"stage":"lobby"})
	# Simulate a valid packet delayed across independent transport channels, without changing network settings.
	net._flight.rpc({"id":77,"net_epoch":net.epoch});net._event_rpc.rpc("projectile",{"id":77,"net_epoch":net.epoch})
	await get_tree().create_timer(.25).timeout;await command_all("late_packet_check")
	await command_all("host_loss");net.close(false);await get_tree().create_timer(.6).timeout;check(not net.active,"dedicated host closes cleanly");finish()
func finish()->void:
	if finished:return
	finished=true
	var receipt:Dictionary={"role":role,"checks":checks,"failures":failures,"passed":failures.is_empty(),"seconds":stamp()-started,"phases":phases,"scope":"local UDP; simulated commands and poses; seeded charged state; permissive geometry validator; no headset/Internet proof"}
	var file:=FileAccess.open(output,FileAccess.WRITE);file.store_string(JSON.stringify(receipt,"\t"));file.close();print("MATRIX1001 ",JSON.stringify(receipt))
	net.close(false);net.queue_free();get_tree().quit(0 if failures.is_empty() else 1)
