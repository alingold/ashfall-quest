extends Node
const Session=preload("res://scripts/net_session1000.gd")
var net:Node
var role:String
var output:String
var port:int
var clients:int
var minimum_seconds=0.0
var minimum_cycles=8
var done=false
var checks=0
var failures:Array=[]
var acks:Dictionary={}
var command_serial=0
var auto_loaded=true
var loaded_key=""
var pose_serial=0
var pose_clock=0.0
var wants_charge=false
var started=0.0
var cycles=0
var resync_completed=0
var disconnects=0
var memory_start=0
var memory_peak=0
var samples:Array=[]
var kick_expected=false
var host_loss_expected=false
func stamp()->float:return Time.get_ticks_msec()*.001
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",role," ",label)
func peer(name:String)->int:
	for id in net.players:
		if net.players[id].name==name:return int(id)
	return 0
func wait_for(condition:Callable,label:String,seconds:float=12)->bool:
	var until=stamp()+seconds
	while stamp()<until:
		if condition.call():return true
		await get_tree().create_timer(.03).timeout
	check(false,label+" timeout stage="+net.stage);return false
func run()->void:
	var args=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);clients=int(args[3]);minimum_cycles=int(args[4]) if args.size()>4 else 8;minimum_seconds=float(args[5]) if args.size()>5 else 0;started=stamp();Engine.max_fps=40
	net=Session.new();get_tree().root.add_child(net);net.validator=func(_kind:String,_id:int,_data:Dictionary)->bool:return true;net.spawn_provider=func(id:int,_players:Dictionary,_rules:Dictionary)->Vector3:return Vector3((id%4)*.25,.2,0)
	memory_start=OS.get_static_memory_usage();memory_peak=memory_start
	net.disconnected.connect(func(reason:String):
		if role!="server":
			disconnects+=1;wants_charge=false;print("EXPECTED_OR_UNEXPECTED_DISCONNECT ",role," ",reason)
			if host_loss_expected:check(not net.active and net.players.is_empty(),"actual host loss clears session");finish()
			elif kick_expected:check("Removed by the host" in reason,"kick explanation delivered before disconnect");kick_expected=false;return_after_kick.call_deferred())
	if role=="server":check(net.host("Endurance",port,"1004",true)==OK,"dedicated test host opens");await sequence()
	else:
		await get_tree().create_timer(.2+float(role.substr(1))*.05).timeout;check(net.join("127.0.0.1",role,port,"1004")==OK,"initial join")
		await wait_for(func():return done,"server completion",minimum_seconds+240)
		if not done:finish()
func _process(delta:float)->void:
	if net==null or not net.active:return
	memory_peak=maxi(memory_peak,OS.get_static_memory_usage())
	if role=="server":
		if net.stage=="loading" and not net.world_ready:net.register_world([])
		return
	if not net.players.has(net.me()):return
	var key=str(net.me())+":"+str(net.epoch)
	if auto_loaded and net.stage in ["match","loading"] and not net.players[net.me()].loaded and loaded_key!=key:loaded_key=key;net.request("loaded",{"epoch":net.epoch})
	if net.stage!="match" or not net.players[net.me()].loaded:return
	pose_clock+=delta
	if pose_clock<.05:return
	pose_clock=0;pose_serial+=1;var at:Vector3=net.players[net.me()].get("spawn",Vector3(0,.2,0))
	var left=Basis.IDENTITY;var right=Basis(Vector3.UP,PI) if wants_charge else Basis.IDENTITY
	net.send_pose({"root":Transform3D(Basis.IDENTITY,at),"head":Transform3D(Basis.IDENTITY,at+Vector3.UP*1.7),"left":Transform3D(left,at+Vector3(-.3,1.2,0)),"right":Transform3D(right,at+Vector3(.3,1.2,0)),"weapon":-1,"holstered":true,"paddles":wants_charge,"desktop":true,"seq":pose_serial})
func all(kind:String,data:Dictionary={})->void:
	command_serial+=1;var serial=command_serial;acks.clear();action.rpc(serial,kind,data)
	await wait_for(func():return acks.size()==clients and acks.values().all(func(v:int)->bool:return v==serial),"control ack "+kind)
@rpc("authority","call_remote","reliable",0)
func action(serial:int,kind:String,data:Dictionary)->void:
	match kind:
		"configure":
			if net.is_host():net.request("rules",{"map":"range","mode":"respawn","teams":1,"protection":0.0,"score_limit":0,"time_limit":0,"respawn_delay":1.0})
		"ready":net.request("ready")
		"pickup":net.request("pickup",{"id":data.id})
		"reload_part":
			if role=="p2":net.request("grip",{"serial":4});net.request("reload",{"id":data.id,"state":"needs_feed","xr":true,"grip_serial":4})
		"reload_finish":
			if role=="p2":
				for i in 3:
					net.request("grip",{"serial":i+1});net.request("reload",{"id":data.id,"state":["box_seated","needs_charge","ready"][i],"xr":true,"grip_serial":i+1})
		"reconnect":
			if role=="p2":
				wants_charge=false;var token=net.reconnect_token;net.close(false);await get_tree().create_timer(.2).timeout;net.join("127.0.0.1",role,port,"1004")
				await wait_for(func():return net.players.has(net.me()) and net.stage=="match" and net.players[net.me()].loaded,"client rejoined")
				check(net.reconnect_token==token,"reconnect identity retained")
		"charge":
			if role=="p2":wants_charge=bool(data.get("on",true))
		"revive":
			if role=="p2":net.request("revive",{"id":data.id})
		"hold_loader":
			if role=="p2":auto_loaded=false
		"restart":
			if net.is_host():net.request("restart")
		"leave_loading":
			if role=="p2":
				var token=net.reconnect_token;net.close(false);await get_tree().create_timer(.35).timeout;auto_loaded=true;net.join("127.0.0.1",role,port,"1004");await wait_for(func():return net.players.has(net.me()) and net.stage=="match" and net.players[net.me()].loaded,"loading departure/rejoin");check(net.reconnect_token==token,"loading reconnect token")
		"gap":
			if role=="p1":
				# Application-level omission of two accepted state serials, not OS packet loss.
				net._received_serial-=2;net.entities.erase(str(data.id));net._resync_waiting=false
				await wait_for(func():return net.entities.has(str(data.id)) and not net._resync_waiting,"retryable full-state convergence")
		"home":
			if net.is_host():net.request("home")
		"arm_kick":
			if role=="p2":kick_expected=true
		"kick":
			if net.is_host():net.request("kick",{"id":data.id})
		"arm_host_loss":host_loss_expected=true
		"observe":check(net.epoch==int(data.epoch) and net.stage==str(data.stage),"replicated round and stage")
	await get_tree().create_timer(.10).timeout
	if net.active:ack.rpc_id(1,role,serial)
func return_after_kick()->void:
	await get_tree().create_timer(.25).timeout;net.join("127.0.0.1",role,port,"1004")
@rpc("any_peer","call_remote","reliable",0)
func ack(label:String,serial:int)->void:
	if role=="server" and peer(label)==multiplayer.get_remote_sender_id():acks[label]=serial
func sequence()->void:
	if not await wait_for(func():return net.players.size()==clients,"all clients joined"):finish();return
	await all("configure");await all("ready")
	if not await wait_for(func():return net.stage=="match","match start"):finish();return
	var charge_start=stamp();await all("charge");await wait_for(func():return float(net._ready_until.get(peer("p2"),0))>net.now(),"real desktop-pose paddle charge",9);facts_sample("initial_charge",{"realSeconds":stamp()-charge_start});await all("charge",{"on":false})
	while cycles<minimum_cycles or stamp()-started<minimum_seconds:
		cycles+=1;var guest=peer("p2");var eid="staged_m60_"+str(cycles)
		net.register_world([{"id":eid,"kind":"weapon","catalog":8,"owner":guest,"ammo":0,"capacity":75,"reload":"holding_box","seated":0,"position":Vector3(0,.7,0),"basis":Basis.IDENTITY}]);net.players[guest].inventory.append(eid);net._strokes[guest]=3;net._publish()
		await all("reload_part",{"id":eid});check(net.entities[eid].reload=="needs_feed","reload advanced before disconnect")
		var token=net.players[guest].token;await all("reconnect");guest=peer("p2");check(net.players[guest].token==token and int(net.entities[eid].owner)==guest and net.entities[eid].reload=="needs_feed" and net.entities[eid].ammo==0,"staged empty M60 survives reconnect")
		await all("reload_finish",{"id":eid});check(net.entities[eid].ammo==75 and net.entities[eid].reload=="ready","fresh strokes resume reload under new transport ID")
		if cycles==1:
			await all("charge",{"on":true});await wait_for(func():return float(net._charge.get(peer("p2"),0))>.08,"real pose charge before midcharge disconnect")
			facts_sample("real_midcharge_disconnect",{"charge":net._charge.get(peer("p2"),0)})
		else:net._charge[guest]=.75;net._ready_until[guest]=net.now()+3.5
		await all("reconnect");guest=peer("p2");check(float(net._charge.get(guest,0))==0 and not net._ready_until.has(guest),"disconnect during paddle charging cannot transfer stale ready state")
		var target=peer("p1");net.rules.mode="revive";net.players[target].protection_until=0;net.hurt(target,250,0);net._ready_until[guest]=net.now()+3.5;net._publish();var ready=net._ready_until[guest]
		await all("revive",{"id":target});check(net.players[target].life=="alive" and net.players[target].health==50 and net._ready_until[guest]==ready,"revive retains bounded charged window")
		net.rules.mode="respawn";net.players[guest].health=37;net.players[guest].shield=12;var epoch=net.epoch;await all("hold_loader");await all("restart");await wait_for(func():return net.stage=="loading" and net.epoch>epoch,"missing loader barrier")
		await all("leave_loading");await wait_for(func():return net.stage=="match" and net.players.size()==clients,"departure releases loader barrier without timeout")
		check(net.players[peer("p2")].health==100 and net.players[peer("p2")].shield==100,"loading reconnect receives new-round health")
		await all("observe",{"epoch":net.epoch,"stage":"match"})
		if cycles%4==0:
			var first=peer("p1");var key=str(net.players[first].inventory[0]);var denied_before=net.resync_denied;net._resync_last[first]=net.now()+1.2;var gap_start=stamp();await all("gap",{"id":key});check(net.resync_denied>denied_before,"ignored initial resync was retried successfully");facts_sample("resync_retry",{"realSeconds":stamp()-gap_start,"denied":net.resync_denied-denied_before});resync_completed+=1
		check(net.entities.size()==clients*3,"round reset has bounded loadout entities");check(net._seen.size()<=clients and net._budget.size()<=clients*2,"peer bookkeeping bounded after churn")
		if cycles%4==0:checkpoint()
		if not failures.is_empty():break
		await get_tree().create_timer(.15).timeout
	await all("arm_kick");var kicked=peer("p2");command_serial+=1;action.rpc_id(peer("p1"),command_serial,"kick",{"id":kicked});await wait_for(func():return peer("p2")>0 and peer("p2")!=kicked and net.players[peer("p2")].loaded,"kicked client received reason and rejoined")
	await all("home");await wait_for(func():return net.stage=="lobby","final lobby");await all("observe",{"epoch":net.epoch,"stage":"lobby"});await all("arm_host_loss");net.close(false);await get_tree().create_timer(1).timeout;finish()
func facts_sample(kind:String,data:Dictionary)->void:
	samples.append({"kind":kind,"data":data,"realSeconds":stamp()-started})
	if samples.size()>128:samples.pop_front()
func checkpoint()->void:
	var row=receipt();var f=FileAccess.open(output+".checkpoint",FileAccess.WRITE);f.store_string(JSON.stringify(row,"\t"));f.close();print("ENDURANCE_PROGRESS cycles=",cycles," seconds=",stamp()-started)
func receipt()->Dictionary:return {"passed":failures.is_empty(),"failures":failures,"role":role,"checks":checks,"cycles":cycles,"seconds":stamp()-started,"minimumSeconds":minimum_seconds,"resyncConvergences":resync_completed,"memoryStaticStart":memory_start,"memoryStaticPeak":memory_peak,"samples":samples,"scope":"real local UDP session processes; scripted poses/commands; one actual timed desktop-pose charge, later explicitly seeded ready windows; staged reload and loader disconnects; replica serial omission and initially ignored resync requests; permissive geometry validator, no full game/VR/WAN proof"}
@rpc("authority","call_remote","reliable",0)
func finish_remote()->void:finish()
func finish()->void:
	if done:return
	done=true;var f=FileAccess.open(output,FileAccess.WRITE);f.store_string(JSON.stringify(receipt(),"\t"));f.close();print("ENDURANCE1004 "+JSON.stringify(receipt()));net.close(false);get_tree().quit(0 if failures.is_empty() else 1)
