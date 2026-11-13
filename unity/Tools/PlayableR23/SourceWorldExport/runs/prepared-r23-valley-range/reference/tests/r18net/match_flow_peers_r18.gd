extends "res://tests/r18net/peer_base_r18.gd"
## R18 host + two real client Mains over loopback ENet (run with tools/r18net/run-peers.ps1,
## roles host,p1,p2). Shared in-map warmup, non-admin privilege rejection, warmup map switch
## (old map/loot retired), warmup->match->rematch->warmup with spawn revisions and stale-pose
## rejection, single death accounting, team totals, weapon-named compact feed, team-only
## ghost visibility/movement/respawn, and host kick enforcement. XR off, Dummy audio.
const SECOND_MAP:="archive:classic-bigbox-vr"
const STYLE_BY_ROLE:={"host":1,"p1":2,"p2":3}
const BASE_RULES:={"mode":"respawn","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"respawn_delay":3.0}
var disconnect_reasons:Array=[]
var shot_events:Dictionary={} # shooter id -> count (all peers)
func run():
	game.front_menu.gallery.style=int(STYLE_BY_ROLE.get(role,0))
	net.disconnected.connect(func(reason:String):disconnect_reasons.append(reason))
	net.event_received.connect(func(kind:String,data:Dictionary):
		if kind=="shot":shot_events[int(data.player)]=int(shot_events.get(int(data.player),0))+1)
	host_or_join(role,"r18flow")
	if role=="host":
		var rules:Dictionary=BASE_RULES.duplicate();rules.map="range";net.request("rules",rules)
	# ---------- 1. shared in-map warmup ----------
	check(await wait_for(loaded_warmup,60),"three peers loaded the same shared in-map warmup")
	check(not game.front_end_active and map_id()=="range","warmup is the in-map world, not the title/main menu")
	await frames(10)
	check(bridge.avatars.size()==peer_count-1,"each peer has an avatar for every other peer in warmup")
	check(await barrier(1,{"epoch":net.epoch,"map":map_id(),"style":int(net.players[net.me()].get("style",-1))}),"barrier 1")
	if role=="host":
		var epochs:Array=[];var maps:Array=[]
		for id in ids():epochs.append(sync.fact(id,1).get("epoch",-1));maps.append(sync.fact(id,1).get("map",""))
		check(epochs.count(epochs[0])==epochs.size() and maps.count("range")==maps.size(),"all peers agree on warmup epoch and map "+str(epochs)+str(maps))
	await wait_for(func():return _styles_synced(),10)
	check(_styles_synced(),"each peer's selected skin replicated "+str(_styles()))
	# ---------- 2. non-admin privilege checks ----------
	var revision_before:int=int(net.revision)
	if role=="p1":
		var victim:int=peer_named("p2")
		net.request("start");net.request("rules",{"map":SECOND_MAP});net.request("kick",{"id":victim});net.request("home");net.request("restart");net.request("balance");net.request("team",{"id":victim,"team":1-int(net.players[victim].team)});net.request("cancel")
	await seconds(1.2)
	check(await barrier(2),"barrier 2")
	if role=="host":
		check(net.stage=="lobby" and str(net.rules.map)=="range" and int(net.revision)==revision_before,"non-admin start/rules/home/restart rejected")
		check(net.players.size()==3 and disconnect_reasons.is_empty(),"non-admin kick rejected")
		var teams:Array=[];for id in ids():teams.append(int(net.players[id].team))
		check(teams.count(0)>=1 and teams.count(1)>=1,"non-admin team/balance did not collapse teams "+str(teams))
	# ---------- 3. warmup map switch: same selected map, old map + loot retired ----------
	var old_map:WeakRef=weakref(game.active_sandbox_map);var old_props:Array=[]
	for prop in game.weapon_inventory.pickups.values():old_props.append(weakref(prop))
	for box in game.ammo_pickups.boxes:old_props.append(weakref(box))
	var old_epoch:int=int(net.epoch);var old_revision:int=int(net.players[net.me()].spawn_revision)
	check(await barrier(3),"barrier 3")
	if role=="host":
		var rules:Dictionary=BASE_RULES.duplicate();rules.map=SECOND_MAP;net.request("rules",rules)
	check(await wait_for(func():return int(net.epoch)>old_epoch and loaded_warmup() and map_id()==SECOND_MAP,90),"all peers reload the host-selected second map in warmup")
	await frames(6)
	check(old_map.get_ref()==null,"previous map node freed")
	var retained:=0
	for ref in old_props:
		var prop=ref.get_ref()
		if prop!=null and is_instance_valid(prop) and prop.is_visible_in_tree():retained+=1
	check(retained==0,"no visible previous-map loot retained ("+str(retained)+")")
	check(int(net.players[net.me()].spawn_revision)>old_revision,"map switch issues a new spawn revision")
	await wait_for(func():return game.player.global_position.distance_to(net.players[net.me()].spawn)<1.5,5)
	check(_flat(game.player.global_position,net.players[net.me()].spawn)<1.0,"local body moved to the new warmup spawn")
	var audit:Dictionary=await _loot_audit()
	check(int(audit.orphans)==0 and int(audit.misplaced)==0 and int(audit.stale)==0,"loot props all map to current-epoch authority "+str(audit))
	facts.second_map_loot=audit
	check(await barrier(4,{"epoch":net.epoch,"map":map_id(),"loot":audit}),"barrier 4")
	if role=="host":
		var epochs:Array=[];var counts:Array=[]
		for id in ids():epochs.append(sync.fact(id,4).get("epoch",-1));counts.append(sync.fact(id,4).get("loot",{}).get("weapons",-1))
		check(epochs.count(epochs[0])==3,"all peers on the same second-map epoch "+str(epochs))
		check(counts.count(counts[0])==3,"all peers see the same number of ground weapons "+str(counts))
		facts.ground_weapons=counts
	# ---------- 5. warmup -> match ----------
	var warm_at:Vector3=game.player.global_position;var warm_revision:int=int(net.players[net.me()].spawn_revision);old_epoch=int(net.epoch)
	if role=="host":net.request("start")
	check(await wait_for(func():return int(net.epoch)>old_epoch and loaded_match(),60),"countdown advances every peer into the match")
	await wait_for(func():return int(net.players[net.me()].spawn_revision)>warm_revision and game.player.global_position.distance_to(net.players[net.me()].spawn)<1.5,8)
	check(int(net.players[net.me()].spawn_revision)>warm_revision,"match spawn revision supersedes warmup")
	check(_flat(game.player.global_position,net.players[net.me()].spawn)<1.0,"local body moved to the authoritative match spawn")
	facts.warmup_to_match_metres=warm_at.distance_to(net.players[net.me()].spawn)
	check(game.player.input_enabled,"controls enabled in match")
	check(await barrier(5),"barrier 5")
	await seconds(1.5)
	# Stale protection: a correctly-sequenced packet from a superseded spawn revision or round
	# must not move anyone; then real traffic must already sit at the new spawns.
	for id in ids():
		if id==net.me() or not net.poses.has(id):continue
		var before:Vector3=net.poses[id].root.origin
		var stale:Dictionary=net.poses[id].duplicate(true)
		for key in ["root","head","left","right"]:stale[key].origin+=Vector3(0,0,6)
		if net.server:
			stale.seq=1<<29;stale.net_epoch=net.epoch;stale.session_id=net._session_nonce;stale.epoch=net.epoch;stale.spawn_revision=int(net.players[id].spawn_revision)-1
			net._accept_pose(id,stale)
			check(net.poses[id].root.origin.is_equal_approx(before),"server rejects pose from superseded spawn revision for "+str(id))
			stale.spawn_revision=int(net.players[id].spawn_revision);stale.epoch=net.epoch-1
			net._accept_pose(id,stale)
			check(net.poses[id].root.origin.is_equal_approx(before),"server rejects pose from previous round for "+str(id))
		else:
			stale.time=net.now()+100.0;stale.spawn_revision=int(net.players[id].spawn_revision)-1
			net._remote_pose(id,stale,net.epoch,net._session_nonce)
			check(net.poses[id].root.origin.is_equal_approx(before),"client ignores relayed pose from superseded spawn revision for "+str(id))
			net._remote_pose(id,stale.merged({"spawn_revision":int(net.players[id].spawn_revision)},true),net.epoch-1,net._session_nonce)
			check(net.poses[id].root.origin.is_equal_approx(before),"client ignores relayed pose from previous round for "+str(id))
		check(_flat(net.poses[id].root.origin,net.players[id].spawn)<3.0,"live pose of "+str(id)+" is at its match spawn, not warmup")
		if bridge.avatars.has(id):check(_flat(bridge.avatars[id].global_position,net.players[id].spawn)<3.0,"avatar of "+str(id)+" rendered at its match spawn")
	audit=await _loot_audit()
	check(int(audit.orphans)==0 and int(audit.misplaced)==0 and int(audit.stale)==0,"match loot props all map to current-epoch authority "+str(audit))
	check(await barrier(6),"barrier 6")
	# ---------- 7. single death accounting, compact weapon-named feed, team totals ----------
	var ally:int=0;var enemy:int=0
	for id in ids():
		if id==1:continue
		if net.allied(1,id):ally=id
		else:enemy=id
	check(ally!=0 and enemy!=0,"roster has one host ally and one opponent")
	facts.ally=str(net.players.get(ally,{}).get("name",""));facts.enemy=str(net.players.get(enemy,{}).get("name",""))
	if role=="host":
		net.hurt(enemy,1000,1,false,true,11);net.hurt(enemy,1000,1,false,true,11);net._die_r13(enemy,1,11);net._publish()
		check(int(net.players[enemy].deaths)==1 and int(net.players[1].kills)==1,"repeated lethal damage counts one death and one kill")
		check(int(net.match_scores.get(int(net.players[1].team),0))==1 and net.combat_feed.size()==1,"one team point and one feed entry")
	check(await wait_for(func():return _feed().size()==1 and int(net.players[enemy].deaths)==1),"single knockdown replicated")
	await frames(4)
	var hud:Node=game.combat_hud.get_node("CompactMatchHUDR3")
	var line:String=hud.feeds[0].text
	check(line.contains("eliminated") and line.contains(str(net.players[enemy].name)) and line.contains(game.weapon.weapon_names[11]) and line.contains("R18 Host"),"compact feed names attacker, victim and weapon: "+line)
	check(hud.feeds[1].text=="","only one feed line for one death")
	facts.feed_line=line
	check(await barrier(7),"barrier 7")
	check(await wait_for(func():return net.players[enemy].life=="alive",10),"opponent respawns after delay")
	if role=="host":
		net.players[enemy].protection_until=0;net.hurt(enemy,1000,ally,false,true,0)
		check(int(net.match_scores.get(int(net.players[1].team),0))==2 and int(net.players[ally].kills)==1 and int(net.players[1].kills)==1,"team total sums two teammates' kills")
	check(await wait_for(func():return _feed().size()==2),"second knockdown replicated")
	await frames(4)
	var own_team:int=int(net.players[net.me()].team);var host_team:int=int(net.players[1].team)
	var expect_left:String="2" if own_team==host_team else "0";var expect_right:String="0" if own_team==host_team else "2"
	check(hud.left_text.text==expect_left and hud.right_text.text==expect_right,"team score bars own-first "+hud.left_text.text+":"+hud.right_text.text)
	check(await barrier(8),"barrier 8")
	# ---------- 9. teammate-only ghost, through-wall tag, movement, living-replica safety ----------
	check(await wait_for(func():return net.players[enemy].life=="alive",10),"opponent alive again before ghost phase")
	var death_at:Vector3=Vector3.ZERO
	if role=="host":
		net.rules.respawn_delay=10.0;net.players[ally].protection_until=0;net.hurt(ally,1000,enemy,false,true,2)
	check(await wait_for(func():return net.players[ally].life=="eliminated"),"ally ghost state replicated")
	death_at=net._position(ally)
	await frames(6)
	var ghost_view:Dictionary=_ghost_view(ally)
	if net.me()==ally:
		check(bridge._local_ghost_r13.active and game.player.collision_layer==0 and not game.weapon.can_switch(),"ghost peer: own ghost body, no collision, no equipment")
	elif net.me()==1:
		check(ghost_view.visible and ghost_view.ghost and ghost_view.shader and ghost_view.tag,"ally sees translucent ghost body and tag")
		check(ghost_view.title==str(net.players[ally].name) and ghost_view.health<.01 and ghost_view.style==int(net.players[ally].get("style",-1)) and ghost_view.style==int(STYLE_BY_ROLE.get(str(net.players[ally].name),-1)),"ally ghost keeps current name/skin with empty health "+str(ghost_view))
		check(ghost_view.collision==0 and not ghost_view.gun,"ally ghost has no hitbox or gun")
		check(_living_visible(enemy),"living opponent replica remains visible while a ghost is suppressed elsewhere")
	else:
		check(not ghost_view.visible and not ghost_view.tag and ghost_view.collision==0,"opponent cannot see enemy ghost or its tag")
		check(_living_visible(1),"opponent still sees the living host replica")
	facts.ghost_view=ghost_view
	check(await barrier(9),"barrier 9")
	if net.me()==ally:
		var press:=InputEventKey.new();press.physical_keycode=KEY_W;press.pressed=true;Input.parse_input_event(press)
		await seconds(.9)
		var release:=InputEventKey.new();release.physical_keycode=KEY_W;release.pressed=false;Input.parse_input_event(release)
		var eid:String=str(net.players[ally].inventory[0]) if not net.players[ally].inventory.is_empty() else ""
		net.request("shot",{"id":eid,"origin":game.player.global_position+Vector3.UP*1.4,"directions":[Vector3.FORWARD],"time":net.now(),"side":0})
	check(await wait_for(func():return net.poses.has(ally) and net.poses[ally].root.origin.distance_to(death_at)>1.0,8),"ghost walking accepted by authority and replicated")
	await seconds(.6)
	facts.ghost_walk_metres=net._position(ally).distance_to(death_at)
	check(int(shot_events.get(ally,0))==0,"ghost cannot fire")
	if net.me()==1:
		var avatar:Node=bridge.avatars[ally]
		check(avatar.is_visible_in_tree() and avatar.global_position.distance_to(death_at)>.7,"ally sees ghost move")
	elif net.me()==enemy:
		check(not bridge.avatars[ally].is_visible_in_tree(),"moving ghost stays hidden from opponent")
	check(await barrier(10),"barrier 10")
	check(await wait_for(func():return net.players[ally].life=="alive",16),"ghost respawns on timer")
	await frames(6)
	if net.me()!=ally:
		var avatar:Node=bridge.avatars[ally]
		check(avatar.is_visible_in_tree() and not avatar.ghost_r13.active and avatar.collision_layer==4,"respawned ally restored for every viewer")
	else:check(not bridge._local_ghost_r13.active and game.player.collision_layer!=0,"respawned peer restores own body")
	var match_revision:int=int(net.players[net.me()].spawn_revision)
	var old_ids:Array=net.entities.keys()
	check(await barrier(11,{"epoch":int(net.epoch)}),"barrier 11")
	# ---------- 12. rematch ----------
	if role=="p2" and "--r19-delay" in OS.get_cmdline_user_args():await seconds(2.0)
	old_epoch=int(sync.fact(1,11).epoch)
	if role=="host":net.request("restart")
	check(await wait_for(func():return int(net.epoch)>old_epoch and loaded_match(),60),"rematch reloads every peer into a fresh match")
	await frames(6)
	var clean:=true
	for id in ids():clean=clean and int(net.players[id].kills)==0 and int(net.players[id].deaths)==0
	check(clean and net.match_scores.is_empty() and _feed().is_empty(),"rematch clears kills/deaths/team totals/feed")
	var leaked:=0
	for eid in old_ids:
		if net.entities.has(eid):leaked+=1
	check(leaked==0,"rematch has no entities from the previous round ("+str(leaked)+")")
	check(int(net.players[net.me()].spawn_revision)>match_revision,"rematch issues a new spawn revision")
	audit=await _loot_audit()
	check(int(audit.orphans)==0 and int(audit.stale)==0,"rematch loot props map to current authority "+str(audit))
	check(await barrier(12,{"epoch":int(net.epoch)}),"barrier 12")
	# ---------- 13. back to shared warmup ----------
	if role=="p2" and "--r19-delay" in OS.get_cmdline_user_args():await seconds(2.0)
	old_epoch=int(sync.fact(1,12).epoch)
	if role=="host":net.request("home")
	check(await wait_for(func():return int(net.epoch)>old_epoch and loaded_warmup() and map_id()==SECOND_MAP,60),"host returns everyone to the same shared warmup")
	await frames(6)
	check(game.player.input_enabled and not bridge._local_ghost_r13.active,"warmup restores controls without ghost state")
	check(await barrier(13),"barrier 13")
	# ---------- 14. kick enforcement and lock ----------
	var target:int=peer_named("p2");var keeper:int=peer_named("p1")
	if role=="host":net.request("rules",{"locked":true})
	check(await wait_for(func():return bool(net.rules.locked),5),"host locks lobby")
	var saved_challenge:String=net._join_challenge
	var healthy_events:=[0]
	var healthy_event_counter:=func(kind:String,_data:Dictionary):
		if kind=="r19_healthy_delivery":healthy_events[0]+=1
	net.event_received.connect(healthy_event_counter)
	var healthy_serial:int=int(net._received_serial)
	if role=="p2":net._join_challenge="" # Behave like a modified client that ignores the denial RPC.
	check(await barrier(14),"barrier 14")
	if role=="host":
		var reserved_token:String=str(net.players[target].token)
		var owned:Array=net.players[target].inventory.duplicate()
		var pose_seq:int=int(net._last_pose.get(target,{}).get("seq",-1));var ready_before:bool=bool(net.players[target].ready)
		var revision_kick:int=int(net.revision);var voice_rejected:int=int(bridge.voice.rejected_packets)
		var grace_events:=[0]
		var count_grace:=func(kind:String,data:Dictionary):
			if int(data.get("player",0))==target and kind in ["mark","shot","pickup","reload","consume","build"]:grace_events[0]+=1
		net.event_received.connect(count_grace)
		net.request("kick",{"id":target})
		check(net.players.has(target) and net.players[target].get("kicked_r18",false),"kick marks the target immediately")
		# During the denial grace the (non-cooperating) target keeps sending real ENet traffic.
		await seconds(.8)
		if net.players.has(target):
			check(grace_events[0]==0,"kicked peer's commands produce no events during grace ("+str(grace_events[0])+")")
			check(int(net._last_pose.get(target,{}).get("seq",-1))==pose_seq,"kicked peer's poses are not accepted during grace")
			check(bool(net.players[target].ready)==ready_before and int(net.revision)==revision_kick and net.players.size()==3,"kicked peer cannot ready, change rules or kick during grace")
			check(int(bridge.voice.rejected_packets)>voice_rejected,"kicked peer's voice is rejected during grace")
		else:check(false,"kicked peer still present at 0.8 s to observe the grace")
		facts.grace_voice_rejected=int(bridge.voice.rejected_packets)-voice_rejected
		var forced:bool=await wait_for(func():return not net.players.has(target),3)
		net.event_received.disconnect(count_grace)
		var resolved:=true
		for eid in owned:resolved=resolved and not net.entities.has(eid)
		check(resolved,"kicked player's warmup inventory is retired, not reserved")
		facts.kick_server_enforced=forced
		check(forced,"host kick removes a client that ignores the denial RPC")
		sync.report(15,{"restore":true})
		if not forced:
			await seconds(.4);net.request("kick",{"id":target})
			check(await wait_for(func():return not net.players.has(target),6),"cooperative kicked client leaves")
		check(not net._reconnect.has(reserved_token),"kicked player keeps no reconnect reservation")
		# Real guarded ping with an older stamp makes RTT positive even on fast loopback.
		var pong_samples:Array=[]
		for sample in 8:
			net.players[keeper].ping=-1
			net._broadcast_live(net._ping,[net.now()-.25],2)
			check(await wait_for(func():return int(net.players[keeper].ping)>=200,0.5),"healthy client returns real guarded pong after kick sample "+str(sample))
			pong_samples.append(int(net.players[keeper].ping))
			net._event("r19_healthy_delivery",{"sample":sample})
			net._publish()
			await seconds(.1)
		facts.healthy_pong_ms=pong_samples
		await seconds(5.0)
		var back:int=peer_named("p2")
		check(back==0,"kicked player cannot rejoin a locked lobby via reconnect reservation")
		if back!=0:facts.kicked_rejoin_restored=net.players[back].get("rejoining",false);net.request("kick",{"id":back})
		net.request("rules",{"locked":false})
	elif role=="p2":
		var restored:=false;var tick:=0
		var weapon_eid:String=str(net.players[net.me()].inventory[0]) if not net.players[net.me()].inventory.is_empty() else ""
		while net.active:
			await process_frame
			tick+=1
			if not restored and sync.fact(1,15).get("restore",false):restored=true;net._join_challenge=saved_challenge
			if tick%3==0 and net.active and net.players.has(net.me()):
				# Real ENet commands a modified client might keep sending after its kick.
				net.request("ready");net.request("mark",{"point":game.player.global_position,"normal":Vector3.UP,"color":2})
				net.request("shot",{"id":weapon_eid,"origin":game.player.global_position+Vector3.UP*1.4,"directions":[Vector3.FORWARD],"time":net.now()-.06,"side":0})
				net.request("rules",{"locked":false,"walk":5.0});net.request("kick",{"id":keeper});net.request("team",{"team":0})
				bridge.voice.transmit(_tone_r18(tick))
		facts.kick_reasons=disconnect_reasons.duplicate()
		await seconds(1.0)
		net.join("127.0.0.1",role,port,"r18flow")
		await wait_for(func():return not net.active or (net.players.has(net.me()) and net.stage!="connecting"),8)
		facts.rejoin_admitted=net.active and net.players.has(net.me())
		facts.rejoin_reasons=disconnect_reasons.duplicate()
		check(not facts.rejoin_admitted,"kicked client is refused by the locked lobby")
		await finish("MATCH_FLOW_R18",SCOPE);return
	await wait_for(func():return not net.players.has(target),12)
	if role=="p1":
		check(await wait_for(func():return healthy_events[0]>=8,12),"healthy client receives eight channel2 events after kick")
		check(int(net._received_serial)>healthy_serial+8,"healthy client receives channel3 state progression after kick")
		facts.healthy_channel2_events=healthy_events[0]
		facts.healthy_channel3_serial_before=healthy_serial
		facts.healthy_channel3_serial_after=int(net._received_serial)
		var periodic_serial:int=int(net._received_serial)
		check(await wait_for(func():return int(net._received_serial)>periodic_serial+1,4),"healthy client receives continuing periodic heartbeat state")
		facts.healthy_periodic_state_serial=int(net._received_serial)
	net.event_received.disconnect(healthy_event_counter)
	sync.report(16)
	check(await wait_for(func():return sync.all_reached([1,keeper],16),20),"remaining peers finish together")
	if role=="host":await seconds(1.5)
	await finish("MATCH_FLOW_R18",SCOPE)
const SCOPE:="Three actual Main processes over loopback ENet (listen host + two clients), XR off, Dummy audio, desktop flags. Controlled authority damage calls and synthetic keyboard ghost walking; no headset, microphone, Internet/NAT or physical controller proof."
func _tone_r18(phase:int)->PackedByteArray:
	var p:=PackedByteArray();p.resize(bridge.voice.BYTES)
	for i in bridge.voice.FRAMES:p.encode_s16(i*2,roundi(sin(float(i+phase*320)*.2)*.3*32767))
	return p
func _flat(a:Vector3,b:Vector3)->float:return Vector2(a.x-b.x,a.z-b.z).length()
func _styles()->Dictionary:
	var out:={}
	for id in ids():out[str(net.players[id].name)]=int(net.players[id].get("style",-1))
	return out
func _styles_synced()->bool:
	var s:=_styles()
	return int(s.get("R18 Host",-1))==1 and int(s.get("p1",-1))==2 and int(s.get("p2",-1))==3
func _feed()->Array:return net.combat_feed.filter(func(e):return int(e.epoch)==int(net.epoch))
func _living_visible(id:int)->bool:
	if not bridge.avatars.has(id) or net.players[id].life!="alive":return false
	var a:Node=bridge.avatars[id];return a.is_visible_in_tree() and a.collision_layer==4 and not a.ghost_r13.active
func _ghost_view(id:int)->Dictionary:
	if not bridge.avatars.has(id):return {"visible":false,"tag":false,"collision":-1}
	var a:Node=bridge.avatars[id];var shader:=false
	for view in a.suit.find_children("*","MeshInstance3D",true,false):
		if view.mesh!=null and view.mesh.get_surface_count()>0 and view.is_visible_in_tree():
			var m=view.get_surface_override_material(0)
			if m is ShaderMaterial and m.shader==preload("res://assets/ghost_r13.gdshader"):shader=true;break
	return {"visible":a.is_visible_in_tree(),"ghost":a.ghost_r13.active,"shader":shader,"tag":a.tag.is_visible_in_tree() and a.title.no_depth_test,"title":a.title.text,"health":a.health_bar.scale.x,"style":a._style,"collision":a.collision_layer,"gun":is_instance_valid(a.gun) and a.gun.is_visible_in_tree()}
## Every visible loot prop must be backed by a current-epoch unowned authority entity at its position.
func _loot_audit()->Dictionary:
	await frames(3)
	var out:={"weapons":0,"supplies":0,"heals":0,"orphans":0,"misplaced":0,"stale":0,"orphan_kinds":[]}
	var props:Array=[]
	for prop in game.weapon_inventory.pickups.values():props.append(["weapon",prop])
	for box in game.ammo_pickups.boxes:props.append(["supply",box])
	for prop in game.field_consumables.pickups:props.append(["heal",prop])
	for row in props:
		var prop=row[1]
		if not is_instance_valid(prop) or not prop.is_visible_in_tree():continue
		if row[0]=="heal" and prop.get_meta("taken",false):continue
		out[{"weapon":"weapons","supply":"supplies","heal":"heals"}[row[0]]]+=1
		var eid:String=str(prop.get_meta("network_id1000",""))
		if eid.is_empty() or not net.entities.has(eid):out.orphans+=1;out.orphan_kinds.append(row[0]+":"+eid);continue
		var e:Dictionary=net.entities[eid]
		if int(e.owner)!=0:out.stale+=1;continue
		var parts:=eid.split("_")
		if parts.size()>=3 and parts[0] in ["ground","supply","heal","archive"] and int(parts[1])!=int(net.epoch):out.stale+=1
		if prop.global_position.distance_to(e.position)>.35:out.misplaced+=1
	return out
