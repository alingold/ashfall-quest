extends Node
const Session=preload("res://scripts/net_session1000.gd")
var net:Node
var game:Node
var role:String
var output:String
var port:int
var checks=0
var failures:Array=[]
var observed:Dictionary={}
var acknowledgement=""
var auto_rejoin=false
var done=false
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",role," ",label)
func wait_for(condition:Callable,label:String,seconds:float=15)->bool:
	var end=Time.get_ticks_msec()+int(seconds*1000)
	while Time.get_ticks_msec()<end:
		if condition.call():return true
		await get_tree().create_timer(.04).timeout
	check(false,label+" timeout");return false
func guest()->int:return int(net.players.keys()[0]) if not net.players.is_empty() else 0
func run()->void:
	var args=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);Engine.max_fps=30
	if role=="server":
		game=Node3D.new();game.name="AshfallGame";get_tree().root.add_child(game);net=Session.new();game.add_child(net)
		net.validator=func(_kind:String,_id:int,_data:Dictionary)->bool:return true
		net.spawn_provider=func(_id:int,_players:Dictionary,_rules:Dictionary)->Vector3:return Vector3(0,.2,608)
		check(net.host("Hybrid host",port,"1004",true)==OK,"light authority starts");net.rules.map="range";net.rules.catalog=true;net.rules.mode="respawn";net.rules.teams=0;net.rules.protection=0.0;net.rules.time_limit=0;net.rules.score_limit=0;net.rules.infinite_ammo=false
		await sequence()
	else:
		game=preload("res://scripts/main.gd").new();game.name="AshfallGame";get_tree().root.add_child(game)
		for i in 4:await get_tree().process_frame
		net=game.network_bridge.net;net.disconnected.connect(func(_reason:String):
			if auto_rejoin and not done:rejoin.call_deferred())
		check(net.join("127.0.0.1","MainClient",port,"1004")==OK,"actual Main client joins")
		await wait_for(func():return done,"hybrid completion",100)
		if not done:finish()
func _process(_dt:float)->void:
	if net==null or not net.active:return
	if role=="server":
		if net.stage=="loading" and not net.world_ready:net.register_world([])
	elif net.stage=="lobby" and not observed.get("readied",false):observed.readied=true;net.request("ready")
func execute(kind:String)->void:
	acknowledgement="";command.rpc_id(guest(),kind)
	await wait_for(func():return acknowledgement==kind,"actual client action "+kind)
@rpc("authority","call_remote","reliable",0)
func command(kind:String)->void:
	var bridge=game.network_bridge
	if kind in ["insert","remove","finish","drop","pickup"]:
		await wait_for(func():return bridge.indices.has("finite_awp"),"real local AWP instance")
		for i in 3:await get_tree().process_frame
		var index=int(bridge.indices.get("finite_awp",5));var gun:Node=game.weapon.weapons[index]
		bridge._bind_reload_events()
		match kind:
			"insert":
				gun.grab_magazine();check(gun.reload_state=="held_mag","real Main AWP magazine grab");gun.insert_magazine();check(gun.reload_state=="needs_bolt" and gun.ammo==2,"real Main partial AWP insertion")
			"remove":gun.eject_magazine();gun._finish_cycle();check(gun.reload_state=="needs_mag" and gun.ammo==0,"real Main bolt removal")
			"finish":gun._finish_cycle();check(gun.reload_state=="ready" and gun.ammo==2,"real Main chambering")
			"drop":game.weapon.selected=index;game.weapon._apply_selection();check(game.weapon_inventory.drop_weapon(index),"actual inventory drop gateway")
			"pickup":check(game.weapon_inventory.pickup_weapon(index),"actual inventory pickup gateway")
	elif kind=="arm_reconnect":auto_rejoin=true
	elif kind=="inspect":
		await wait_for(func():return net.stage=="match" and not bridge._preparing,"actual Main match prepared")
		check(not game.front_end_active and game.player.input_enabled,"actual map/input state restored")
		check(game.weapon.weapons.size()<=int(observed.get("rack_limit",game.weapon.weapons.size()))+3,"real rack remains bounded through rematches")
		observed.rack_limit=game.weapon.weapons.size();observed.epochs=net.epoch
	elif kind=="home":
		await wait_for(func():return net.stage=="lobby" and game.front_end_active,"actual Main returns to pedestal environment")
	ack.rpc_id(1,kind)
@rpc("any_peer","call_remote","reliable",0)
func ack(kind:String)->void:
	if role=="server" and multiplayer.get_remote_sender_id()==guest():acknowledgement=kind
func rejoin()->void:
	auto_rejoin=false;observed.transport_loss=true;await get_tree().create_timer(.25).timeout;net.join("127.0.0.1","MainClient",port,"1004")
func sequence()->void:
	if not await wait_for(func():return net.stage=="match" and net.players.size()==1 and net.players[guest()].loaded,"actual Main reaches loaded barrier",30):finish();return
	await execute("inspect");var id=guest();net.players[id].capacity=6;net.players[id].reserves.sniper=2
	net.entities.finite_awp={"id":"finite_awp","kind":"weapon","catalog":5,"owner":id,"ammo":0,"capacity":3,"seated":0,"reload":"needs_mag","position":Vector3(0,.7,0),"basis":Basis.IDENTITY};net.players[id].inventory.append("finite_awp");net._publish()
	await execute("insert");check(net.entities.finite_awp.reload=="needs_bolt" and net.entities.finite_awp.seated==2 and net.players[id].reserves.sniper==0,"real bridge insertion reaches finite authority once")
	await execute("remove");check(net.entities.finite_awp.reload=="needs_mag" and net.entities.finite_awp.ammo==0 and net.players[id].reserves.sniper==2,"real bridge removal preserves refund")
	await execute("insert");await execute("finish");check(net.entities.finite_awp.ammo==2 and net.players[id].reserves.sniper==0,"real Main reload conserves two rounds end to end")
	await execute("drop");check(net.entities.finite_awp.owner==0 and net.entities.finite_awp.ammo==2,"actual Main drop reaches authority without ammo loss")
	await execute("pickup");check(net.entities.finite_awp.owner==id and net.entities.finite_awp.ammo==2,"actual Main repick preserves ammo")
	await execute("arm_reconnect");var token=net.players[id].token;net.multiplayer.multiplayer_peer.disconnect_peer(id)
	await wait_for(func():return guest()!=0 and guest()!=id and net.players[guest()].loaded,"actual Main reconnect",25)
	check(net.players[guest()].token==token and net.entities.finite_awp.owner==guest(),"actual Main reconnect reclaims same owned finite gun");await execute("inspect")
	for round in 3:
		net._begin_loading();net._publish();await wait_for(func():return net.stage=="match" and net.players[guest()].loaded,"actual Main rematch",25);await execute("inspect")
	net.stage="lobby";net._publish();await execute("home");finish_remote.rpc_id(guest());await get_tree().create_timer(.4).timeout;finish()
@rpc("authority","call_remote","reliable",0)
func finish_remote()->void:
	check(observed.get("transport_loss",false),"real client observed actual disconnect");finish()
func finish()->void:
	if done:return
	done=true;var result={"passed":failures.is_empty(),"role":role,"checks":checks,"failures":failures,"observed":observed,"scope":"one actual headless Main/map/client and its real NetworkBridge + gun methods, controlled lightweight authoritative UDP host with permissive geometry validation; finite AWP insert/remove/chamber, real inventory gateways, reconnect and four match epochs. Not two full game clients, physical VR, actual rendered video or WAN proof."};var f=FileAccess.open(output,FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("HYBRID1004 "+JSON.stringify(result));net.close(false);get_tree().quit(0 if failures.is_empty() else 1)
