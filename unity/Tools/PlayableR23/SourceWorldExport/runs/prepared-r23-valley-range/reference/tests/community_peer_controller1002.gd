extends Node
var game:Node
var net:Node
var role:String
var output:String
var port:int
var checks=0
var failures:Array=[]
var proofs:Dictionary={}
var readied=false
var done=false
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",role," ",label)
func wait_for(condition:Callable,label:String,seconds:float=35)->bool:
	var end=Time.get_ticks_msec()+seconds*1000
	while Time.get_ticks_msec()<end:
		if condition.call():return true
		await get_tree().create_timer(.03).timeout
	check(false,label+" timeout");return false
func run()->void:
	var args=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);Engine.max_fps=40
	game=preload("res://scripts/main.gd").new();game.name="AshfallGame";get_tree().root.add_child(game)
	for i in 5:await get_tree().process_frame
	net=game.network_bridge.net
	if role=="server":
		check(net.host("MapHost",port,"map1002")==OK,"host started")
		net.request("rules",{"map":"community_local","mode":"respawn","teams":0,"time_limit":0,"score_limit":0})
		await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing,"first community match")
		await inspect(1)
		inspect.rpc(1)
		await wait_for(func():return proofs.size()==2,"both first-map client proofs")
		var count=net.entities.size();check(count>90,"authoritative map loot exists")
		for p in proofs.values():check(p.content==net.content_id() and p.scene==preload("res://scripts/community_catalog1002.gd").fingerprint(),"identical client map and content fingerprint");check(p.entities==count,"replicated loot count equal")
		var remote_id=proofs.keys()[0];var old_revision=int(net.players[remote_id].spawn_revision)
		net.poses[remote_id].root.origin=game.active_sandbox_map.global_position+Vector3(700,40,0)
		await wait_for(func():return int(net.players[remote_id].spawn_revision)>old_revision,"authority recovers outside player")
		check(not game.active_sandbox_map._outside(net.players[remote_id].spawn),"authoritative bounds recovery uses valid map spawn")
		proofs.clear();net.request("restart")
		await wait_for(func():return net.stage=="match" and net.epoch>=2 and not game.network_bridge._preparing,"community rematch")
		await get_tree().create_timer(.3).timeout;check(net.entities.size()==count,"rematch loot reset stable");inspect.rpc(2)
		await wait_for(func():return proofs.size()==2,"both rematch client proofs")
		for p in proofs.values():check(p.entities==count and p.map=="community_local","client rematch state agrees")
		proofs.clear();net.request("home")
		await get_tree().create_timer(.3).timeout
		net.request("rules",{"map":"range"});readied=false;reset_ready.rpc()
		await wait_for(func():return net.stage=="match" and net.epoch>=3 and not game.network_bridge._preparing,"range transition")
		check(game.active_sandbox_map.name=="StandaloneSandboxRange091","original range still loads")
		inspect.rpc(3);await wait_for(func():return proofs.size()==2,"both range client proofs")
		for p in proofs.values():check(p.map=="range" and p.community_nodes==0,"clients release local community geometry")
		finish_remote.rpc();await get_tree().create_timer(1).timeout;finish()
	else:
		await get_tree().create_timer(.6).timeout
		check(net.join("127.0.0.1",role,port,"map1002")==OK,"client joined")
		await wait_for(func():return done,"client finish",100)
		if not done:finish()
func _process(_delta:float)->void:
	if net!=null and net.active and net.players.size()==3 and net.stage=="lobby" and not readied:readied=true;net.request("ready")
@rpc("authority","call_remote","reliable",0)
func reset_ready()->void:readied=false
@rpc("authority","call_remote","reliable",0)
func inspect(round_number:int)->void:
	await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing,"inspect world settled")
	var map:Node=game.active_sandbox_map
	check(str(net.rules.map)==("range" if round_number==3 else "community_local"),"selected map identical")
	check(net.players.size()==3 and game.network_bridge.avatars.size()==2,"both remote avatars exist")
	var community=game.find_children("LocalCommunityMap","Node3D",true,false).size()
	if round_number<3:
		check(map.map_ready and map.spawn_points.size()==36,"client has validated map collision/spawns")
		var point:Vector3=game.player.global_position;check(map._floor(point+Vector3.UP,3).is_finite(),"network spawn on actual map floor")
		var positions=0
		for eid in game.network_bridge.world_props:
			var prop:Node3D=game.network_bridge.world_props[eid]
			if is_instance_valid(prop) and net.entities.has(eid) and prop.global_position.distance_to(net.entities[eid].position)<.1:positions+=1
		check(positions>60,"actual replicated loot nodes at authoritative positions")
	if role!="server":proof.rpc_id(1,{"map":str(net.rules.map),"entities":net.entities.size(),"content":net.content_id(),"scene":preload("res://scripts/community_catalog1002.gd").fingerprint(),"community_nodes":community})
@rpc("any_peer","call_remote","reliable",0)
func proof(data:Dictionary)->void:proofs[multiplayer.get_remote_sender_id()]=data
@rpc("authority","call_remote","reliable",0)
func finish_remote()->void:finish()
func finish()->void:
	if done:return
	done=true;var report={"role":role,"checks":checks,"failures":failures,"passed":failures.is_empty(),"content":net.content_id(),"scope":"real host and two full-game clients over Windows loopback; scripted ready/rematch/map-change commands; XR off"}
	var f=FileAccess.open(output,FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"));f.close();print("COMMUNITY_WIRE "+JSON.stringify(report));net.close(false);get_tree().quit(0 if failures.is_empty() else 1)
