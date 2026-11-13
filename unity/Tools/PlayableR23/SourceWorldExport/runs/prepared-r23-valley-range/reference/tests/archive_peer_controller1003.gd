extends Node
const Catalog=preload("res://scripts/archive_catalog1003.gd")
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
var expected_rejection=false
var maps:Array=["archive:5-levels-zyro","archive:melis-cafe-gods-pitdevil","archive:mushy-gods-putangfreyja"]
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",role," ",label)
func wait_for(condition:Callable,label:String,seconds:float=65)->bool:
	var until=Time.get_ticks_msec()+seconds*1000
	while Time.get_ticks_msec()<until:
		if condition.call():return true
		await get_tree().create_timer(.03).timeout
	check(false,label+" timeout");return false
func run()->void:
	var args=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);Engine.max_fps=30
	game=preload("res://scripts/main.gd").new();game.name="AshfallGame";get_tree().root.add_child(game)
	for i in 5:await get_tree().process_frame
	net=game.network_bridge.net;net.disconnected.connect(_lost)
	if role=="server":
		check(net.host("ArchiveHost",port,"library")==OK,"host started")
		net.request("rules",{"map":maps[0],"mode":"respawn","teams":0,"time_limit":0,"score_limit":0})
		var epoch=0
		for id in maps:
			if epoch>0:
				net.request("home");await get_tree().create_timer(.4).timeout;net.request("rules",{"map":id});readied=false;reset_ready.rpc()
			if not await wait_for(func():return net.stage=="match" and net.epoch>epoch and not game.network_bridge._preparing,"map ready "+id):finish();return
			epoch=net.epoch;proofs.clear();inspect.rpc(id);await inspect(id)
			await wait_for(func():return proofs.size()==2,"two clients loaded "+id)
			var count=net.entities.size();check(count>80,"map has authoritative ASHFALL loot")
			for p in proofs.values():check(p.id==id and p.fp==Catalog.fingerprint(id) and p.content==net.content_id(),"identical map/source/cache identity");check(p.entities==count,"replicated loot agrees")
			proofs.clear();net.request("restart")
			await wait_for(func():return net.stage=="match" and net.epoch>epoch and not game.network_bridge._preparing,"rematch "+id);epoch=net.epoch;inspect.rpc(id);await inspect(id);await wait_for(func():return proofs.size()==2,"two rematch clients "+id)
			check(net.entities.size()==count,"rematch loot resets without accumulation")
		# Exercise rejection of a deliberately mismatched selected-map cache claim.
		var targets:Array=proofs.keys();var target=int(targets[targets.size()-1]);net.players[target].loaded=false;bad_cache.rpc_id(target)
		await wait_for(func():return not net.players.has(target),"wrong map-cache peer rejected",10)
		check(net.stage=="match","remaining match survives rejected peer")
		finish_remote.rpc();await get_tree().create_timer(1).timeout;finish()
	else:
		await get_tree().create_timer(.6).timeout;check(net.join("127.0.0.1",role,port,"library")==OK,"client joined")
		await wait_for(func():return done,"network fixture finish",240)
		if not done:finish()
func _process(_delta:float)->void:
	if net!=null and net.active and net.players.size()==3 and net.stage=="lobby" and not readied:readied=true;net.request("ready")
@rpc("authority","call_remote","reliable",0)
func reset_ready()->void:readied=false
@rpc("authority","call_remote","reliable",0)
func inspect(id:String)->void:
	await wait_for(func():return net.stage=="match" and not game.network_bridge._preparing,"world settled")
	check(game.active_sandbox_map.map_ready and game.active_sandbox_map.map_id==id,"real selected archive scene")
	check(net.players.size()==3 and game.network_bridge.avatars.size()==2,"two remote avatars")
	check(Catalog.verify(id),"local scene and all material/texture caches verified")
	check(game.active_sandbox_map._floor(game.player.global_position+Vector3.UP,4).is_finite(),"network spawn above map floor")
	var nearest=INF
	for point in game.active_sandbox_map.spawn_points:nearest=minf(nearest,point.distance_to(net.players[net.me()].spawn))
	check(nearest<.01,"authority chooses a validated spawn without an overhead recast")
	if role!="server":proof.rpc_id(1,{"id":id,"fp":Catalog.fingerprint(id),"content":net.content_id(),"entities":net.entities.size()})
@rpc("any_peer","call_remote","reliable",0)
func proof(data:Dictionary)->void:proofs[multiplayer.get_remote_sender_id()]=data
@rpc("authority","call_remote","reliable",0)
func bad_cache()->void:
	expected_rejection=true;net.request("loaded",{"epoch":net.epoch,"map_cache":"intentional-wrong-map-cache-proof"})
func _lost(reason:String)->void:
	if expected_rejection:check("Map cache differs" in reason,"explicit mismatched-cache diagnostic");finish.call_deferred()
@rpc("authority","call_remote","reliable",0)
func finish_remote()->void:finish()
func finish()->void:
	if done:return
	done=true;var row={"role":role,"passed":failures.is_empty(),"checks":checks,"failures":failures,"maps":maps,"scope":"one full-game host and two full-game clients, loopback XR off, scripted small/large/unusual map changes and rematches; intentionally wrong selected-map cache claim"};var f=FileAccess.open(output,FileAccess.WRITE);f.store_string(JSON.stringify(row,"\t"));f.close();print("ARCHIVE_NETWORK "+JSON.stringify(row));net.close(false);get_tree().quit(0 if failures.is_empty() else 1)
