extends Node
## The same authoritative match state serves listen hosts, headless servers and clients.
signal changed
signal poses_changed
signal event_received(kind:String,data:Dictionary)
signal disconnected(reason:String)
const Rules=preload("res://scripts/net_rules1000.gd")
const PROTOCOL:=1000
const CONTENT:="ashfall-cyber-bird-r6"
const RarityR6=preload("res://scripts/weapon_rarity_r6.gd")
const ZoneR3=preload("res://scripts/square_zone_r3.gd")
var zone_engine=ZoneR3.new()
var zone:Dictionary={}
var match_scores:Dictionary={}
var combat_feed:Array=[]
var archive_broken_r8:Dictionary={}
const DEFAULT_PORT:=24760
const NETWORK_REVISION:="standalone-audit1004-ordered-state-v1-2026-10-29"
const MATCH_EVENTS:=["shot","reload","hit","target_hit","revive","paddles_ready","projectile","projectile_end","blast","build","blocked","consume","mark","match_start"]
var active =false
var server =false
var dedicated =false
var admin =1
var stage ="offline"
var status =""
var rules:Dictionary=Rules.DEFAULTS.duplicate(true)
var revision =0
var round_index =0
var epoch =0
var players:Dictionary={}
var entities:Dictionary={}
var builds:Dictionary={}
var poses:Dictionary={}
var marks:Dictionary={}
var room_name ="ASHFALL FRIENDS"
var password =""
var local_name ="Player"
var reconnect_token =""
var local_spectator =false
var match_started =0.0
var countdown_end =0.0
var server_offset =0.0
var loading_deadline =0.0
var validator:Callable
var spawn_provider:Callable
var _serial =0
var _seq =0
var _seen:Dictionary={}
var _budget:Dictionary={}
var _reconnect:Dictionary={}
var _last_pose:Dictionary={}
var _shot_clock:Dictionary={}
var _history:Dictionary={}
var _last_shield:Dictionary={}
var _last_grip:Dictionary={}
var _strokes:Dictionary={}
var _last_reload:Dictionary={}
var _ready_until:Dictionary={}
var _charge:Dictionary={}
var _pose_clock =0.0
var _last_tick =0.0
var _closing =false
var _digest =""
var _pending_events:Array=[]
var pending:Array=[]
var acknowledged =0
var world_ready=false
var loading_target="match"
var last_result=""
var warmup_spawn_provider:Callable
var pod_provider_r12:Callable
var pods_r12:Dictionary={}
var ghost_recovery_provider_r13:Callable
var pose_stats_r14:Dictionary={"accepted":0,"rejected":0,"sanitized_hands":0,"corrected":0}
var _pose_faults_r14:Dictionary={}
var health_pad_provider_r14:Callable
var station_contact_r18:Callable
var station_ready_r18:Dictionary={}
var _heal_clock_r14=0.0
var _published:Dictionary={}
var projectile_poses:Dictionary={}
var training:Dictionary={}
var _heartbeat:=0.0
var _state_serial=0
var _received_serial=-1
var _resync_waiting=false
var _resync_deadline=0
var _resync_last:Dictionary={}
var _session_nonce=""
var _join_challenge=""
var _peer_challenges:Dictionary={}
var resync_requests=0
var resync_denied=0
var resync_applied=0
var _welcome_deadline=0

func _ready()->void:
	name="MultiplayerSession1000"
	if multiplayer is SceneMultiplayer:multiplayer.server_relay=false;multiplayer.allow_object_decoding=false
	multiplayer.peer_connected.connect(_peer_connected)
	multiplayer.peer_disconnected.connect(_peer_left)
	multiplayer.connected_to_server.connect(_connected)
	multiplayer.connection_failed.connect(func():_lost("Connection failed. Check address, port and host availability."))
	multiplayer.server_disconnected.connect(func():_lost("The host closed the session."))
func now()->float:return Time.get_ticks_msec()*.001+server_offset
func content_id()->String:
	var path="res://assets/data/network_content1000.json"
	var base_id:String=CONTENT
	if FileAccess.file_exists(path):
		var manifest=JSON.parse_string(FileAccess.get_file_as_string(path))
		if manifest is Dictionary:base_id=str(manifest.get("content_id",CONTENT))
	# Preserve the public asset manifest while separating this protocol fix from released clients.
	return (NETWORK_REVISION+":"+base_id+":"+preload("res://scripts/archive_catalog1003.gd").library_id()).sha256_text()
func me()->int:return multiplayer.get_unique_id() if active else 0
func is_host()->bool:return active and me()==admin
func host(display_name:String,port:int=DEFAULT_PORT,secret:String="",headless:bool=false)->Error:
	close(false)
	var peer =ENetMultiplayerPeer.new();var err =peer.create_server(port,8,5)
	if err!=OK:status="Cannot host on UDP port %d (%s)"%[port,error_string(err)];changed.emit();return err
	multiplayer.multiplayer_peer=peer;active=true;server=true;dedicated=headless;admin=0 if headless else 1
	_session_nonce=Crypto.new().generate_random_bytes(16).hex_encode()
	password=secret.left(64);local_name=clean_name(display_name);stage="lobby";status="Hosting on UDP %d"%port
	if not headless:_add_player(1,local_name,"")
	_publish();return OK
func join(address:String,display_name:String,port:int=DEFAULT_PORT,secret:String="",spectate:bool=false)->Error:
	close(false)
	var peer =ENetMultiplayerPeer.new();var err =peer.create_client(address.strip_edges(),port,5)
	if err!=OK:status="Cannot connect: "+error_string(err);changed.emit();return err
	local_name=clean_name(display_name);password=secret.left(64);local_spectator=spectate
	_join_challenge=Crypto.new().generate_random_bytes(16).hex_encode()
	_welcome_deadline=Time.get_ticks_msec()+10000
	multiplayer.multiplayer_peer=peer;active=true;server=false;stage="connecting";status="Connecting…";changed.emit();return OK
func close(notify:bool=true)->void:
	_closing=true
	if active and multiplayer.multiplayer_peer:multiplayer.multiplayer_peer.close()
	multiplayer.multiplayer_peer=OfflineMultiplayerPeer.new()
	active=false;server=false;dedicated=false;stage="offline";players.clear();poses.clear();entities.clear();builds.clear();marks.clear()
	pods_r12.clear();station_ready_r18.clear()
	_pose_faults_r14.clear();pose_stats_r14={"accepted":0,"rejected":0,"sanitized_hands":0,"corrected":0}
	epoch=0;revision=0;round_index=0;admin=0;world_ready=false;loading_deadline=0
	_seen.clear();_budget.clear();_reconnect.clear();_last_pose.clear();_shot_clock.clear();_history.clear();_last_grip.clear();_strokes.clear();_last_reload.clear();_ready_until.clear();_charge.clear();_closing=false
	pending.clear();acknowledged=0;_seq=0;server_offset=0;_published.clear();projectile_poses.clear();_pending_events.clear();training.clear()
	_state_serial=0;_received_serial=-1;_resync_waiting=false;_resync_deadline=0;_resync_last.clear();_last_shield.clear()
	_session_nonce="";_join_challenge="";_peer_challenges.clear()
	resync_requests=0;resync_denied=0;resync_applied=0
	_welcome_deadline=0
	last_result="";zone.clear();match_scores.clear();combat_feed.clear()
	status="Choose a map and READY for solo exploration, or HOST / JOIN."
	if notify:changed.emit()
func _lost(reason:String)->void:
	if _closing:return
	print("NET1000 DISCONNECT ",reason)
	close(false);status=reason;disconnected.emit(reason);changed.emit()
func _connected()->void:
	print("NET1000 CONNECTED id=",me())
	_introduce.rpc_id(1,PROTOCOL,content_id(),local_name,password,reconnect_token,local_spectator,_join_challenge)
func _peer_connected(_id:int)->void:print("NET1000 PEER ",_id)
@rpc("any_peer","call_remote","reliable",0)
func _introduce(protocol:int,content:String,display_name:String,secret:String,token:String,spectate:bool,challenge:String="")->void:
	if not server:return
	var id =multiplayer.get_remote_sender_id()
	print("NET1000 HELLO ",id," name=",clean_name(display_name))
	if players.has(id):return
	var reason =""
	if protocol!=PROTOCOL or content!=content_id():reason="Different ASHFALL build. Install the same client and map version as the host."
	elif secret!=password:reason="Incorrect lobby password."
	elif rules.locked and not _reconnect.has(token):reason="Lobby is locked."
	elif players.size()>=int(rules.max_players):reason="Lobby is full."
	elif spectate and not rules.spectators:reason="Spectators are disabled."
	elif stage!="lobby" and not rules.late_join and not _reconnect.has(token):reason="Join the next round; late joining is disabled."
	elif challenge.length()!=32:reason="Join handshake is incomplete. Reconnect with the same ASHFALL client version."
	if not reason.is_empty():print("NET1000 DENY ",id," ",reason);_deny.rpc_id(id,reason,challenge);return
	_peer_challenges[id]=challenge
	_add_player(id,display_name,token)
	if not players[id].get("rejoining",false):
		players[id].waiting_round=not spectate and stage in ["match","loading","results"] and rules.mode=="revive"
		players[id].spectator=spectate or players[id].waiting_round
	if players[id].spectator:players[id].life="spectator"
	if dedicated and admin==0:admin=id
	_welcome.rpc_id(id,str(players[id].token),_session_nonce,challenge)
	_state.rpc_id(id,snapshot())
	_publish()
@rpc("authority","call_remote","reliable",0)
func _deny(reason:String,challenge:String="")->void:
	if challenge==_join_challenge:_lost(reason)
@rpc("authority","call_remote","reliable",3)
func _welcome(token:String,nonce:String="",challenge:String="")->void:
	if not active or server or challenge!=_join_challenge or nonce.is_empty() or (not _session_nonce.is_empty() and nonce!=_session_nonce):return
	_session_nonce=nonce;reconnect_token=token;_welcome_deadline=0
static func clean_name(value:String)->String:
	var out =""
	for c in value.strip_edges():
		if c.unicode_at(0)>=32 and c not in ["[","]","<",">"]:out+=c
	return out.left(24) if not out.is_empty() else "Player"
func _add_player(id:int,display_name:String,token:String)->void:
	var row:Dictionary={}
	if not token.is_empty() and _reconnect.has(token) and float(_reconnect[token].until)>now():
		var reservation:Dictionary=_reconnect[token]
		row=reservation.player.duplicate(true);_reconnect.erase(token);row.rejoining=true
		if int(reservation.get("epoch",epoch))!=epoch:
			# Identity survives a brief disconnect; old round entities and spawn locations do not.
			row.inventory=[];row.reserves={};row.needs_spawn=true;row.spawn_revision=0;row.erase("spawn")
			row.health=float(rules.starting_health);row.shield=float(rules.starting_shield);row.kills=0;row.deaths=0;row.score=0;row.soda_left=0.0;row.downed_at=0.0;row.respawn_at=0.0
			row.team=clampi(int(row.team),0,int(rules.teams)-1) if int(rules.teams)>0 else -1
			if row.get("waiting_round",false):row.spectator=false;row.waiting_round=false
			row.life="spectator" if row.spectator else "alive"
	else:
		row={"name":clean_name(display_name),"team":_balanced_team(),"ready":false,"loaded":false,"health":100.0,"shield":100.0,"life":"alive","kills":0,"deaths":0,"score":0,"inventory":[],"reserves":{},"builds":int(rules.starting_builds),"capacity":6,"spectator":false,"token":Crypto.new().generate_random_bytes(24).hex_encode(),"spawn_revision":0,"protection_until":0.0,"respawn_at":0.0,"downed_at":0.0,"soda_left":0.0,"ping":0}
	row.id=id;row.ready=false;row.loaded=false;players[id]=row
	for eid in row.inventory:
		if entities.has(eid):entities[eid].owner=id
	if row.inventory.is_empty():_starting_inventory(id)
func _balanced_team()->int:
	if int(rules.teams)==0:return -1
	var counts:Array=[];counts.resize(int(rules.teams));counts.fill(0)
	for p in players.values():
		if int(p.team)>=0 and int(p.team)<counts.size():counts[int(p.team)]+=1
	return counts.find(counts.min())
func _peer_left(id:int)->void:
	if _closing or not server:return
	if not players.has(id):return
	var p:Dictionary=players[id]
	if poses.has(id):p.spawn=poses[id].root.origin;p.spawn_revision+=1
	# R18-NET: a kicked player gets no identity reservation (it bypassed lock and restored state);
	# an already-expired entry reuses the ordinary release path for its owned entities.
	var kicked:bool=bool(p.get("kicked_r18",false))
	_reconnect[p.token]={"until":now()-1.0 if kicked else now()+30.0,"player":p.duplicate(true),"epoch":epoch}
	for eid in p.inventory:
		if entities.has(eid):entities[eid].owner=-id # reserved for reconnect, then released
	players.erase(id);poses.erase(id);_history.erase(id);marks.erase(id)
	for table in [_seen,_budget,_last_pose,_strokes,_charge,_ready_until,_last_grip,_last_shield,_resync_last,_peer_challenges]:table.erase(id)
	_budget.erase(-id)
	if kicked:_expire_reconnects()
	if dedicated and admin==id:admin=int(players.keys()[0]) if not players.is_empty() else 0
	if stage=="countdown":stage="lobby"
	if stage=="loading":
		if players.is_empty():stage="lobby";world_ready=false
		else:_try_start()
	_publish.call_deferred()
func request(action:String,data:Dictionary={})->int:
	if not active:return 0
	if not server and (not _transport_ready() or not players.has(me())):return 0
	data=data.duplicate(true);data.net_epoch=epoch;data.session_id=_session_nonce
	_seq+=1
	pending.append({"seq":_seq,"action":action,"data":data.duplicate(true)})
	if server:
		_accept(me(),action,data,_seq);acknowledged=_seq;pending.clear();_publish()
	else:_command.rpc_id(1,action,data,_seq)
	return _seq
@rpc("any_peer","call_remote","reliable",0)
func _command(action:String,data:Dictionary,sequence:int)->void:
	data=data.duplicate(true);data.net_epoch=int(data.get("net_epoch",-1));data.session_id=str(data.get("session_id",""))
	if server:_accept(multiplayer.get_remote_sender_id(),action,data,sequence);_publish()
func _rate(id:int,limit:int=120)->bool:
	var b:Dictionary=_budget.get(id,{"time":now(),"count":0})
	if now()-float(b.time)>1.0:b={"time":now(),"count":0}
	b.count+=1;_budget[id]=b;return int(b.count)<=limit
func _accept(id:int,action:String,data:Dictionary,sequence:int)->void:
	if not players.has(id) or str(data.get("session_id",_session_nonce))!=_session_nonce:return
	if players[id].get("kicked_r18",false):return # R18-NET: no commands during the kick grace.
	if not _rate(id) or sequence<=int(_seen.get(id,0)):return
	_seen[id]=sequence
	if int(data.get("net_epoch",epoch))!=epoch:return
	var p:Dictionary=players[id]
	match action:
		"ready":
			if stage not in ["lobby","countdown"] or p.spectator:return
			p.ready=not p.ready
			if stage=="countdown":stage="lobby"
			_check_ready()
		"start":
			if id!=admin or stage!="lobby" or not can_start():return
			stage="countdown";countdown_end=now()+3.0
		"rules":
			if id!=admin:return
			if stage=="match":
				var live_rules:Dictionary=rules.duplicate(true)
				for key in data:
					if key in Rules.LIVE_R5:live_rules[key]=data[key]
				rules=Rules.validate(live_rules);revision+=1;_publish();changed.emit();return
			if stage not in ["lobby","countdown"]:return
			var previous_map:String=str(rules.map)
			var previous_teams:int=int(rules.teams)
			var next:Dictionary=rules.duplicate(true)
			if data.has("map") and str(data.map)!=previous_map:next=preload("res://scripts/map_rules_r14.gd").resolve(str(data.map),{},data)
			else:next.merge(data,true)
			rules=Rules.validate(next);revision+=1;stage="lobby"
			var team_index:=0
			for row in players.values():
				row.ready=false
				row.team=(team_index%int(rules.teams) if previous_teams!=int(rules.teams) else clampi(int(row.team),0,int(rules.teams)-1)) if int(rules.teams)>0 else -1
				team_index+=1
			if previous_map!=str(rules.map) and world_ready:_begin_loading("lobby")
		"team":
			if stage!="lobby":return
			var target =int(data.get("id",id));var team =int(data.get("team",-1))
			if (id!=admin and target!=id) or not players.has(target) or (int(rules.teams)>0 and (team<0 or team>=int(rules.teams))):return
			players[target].team=team if int(rules.teams)>0 else -1;players[target].ready=false
		"character":
			if stage not in ["lobby","loading","countdown","match"]:return
			var style:int=clampi(int(data.get("style",0)),0,preload("res://scripts/gemini_player_models_099.gd").STYLES.size()-1)
			if stage in ["countdown","match"] and p.has("style") and int(p.style)!=style:return
			p.style=style
		"balance":
			if id!=admin or stage!="lobby":return
			var n =0
			for row in players.values():row.team=n%int(rules.teams) if int(rules.teams)>0 else -1;row.ready=false;n+=1
		"cancel":
			if id!=admin or stage!="countdown":return
			stage="lobby";p.ready=false
		"kick":
			var target =int(data.get("id",0))
			if id!=admin or target<=1 or target==id or not players.has(target):return
			players[target].kicked_r18=true
			_deny.rpc_id(target,"Removed by the host.",str(_peer_challenges.get(target,"")))
			_enforce_kick_r18(target)
		"home":
			if id!=admin:return
			_begin_loading("lobby")
		"restart":
			if id!=admin or stage not in ["match","results"]:return
			round_index=0;_begin_loading()
		"loaded":
			if int(data.get("epoch",-1))!=epoch or p.loaded:return
			if str(rules.map).begins_with("archive:") and str(data.get("map_cache",""))!=preload("res://scripts/archive_catalog1003.gd").fingerprint(str(rules.map)):
				status="Map cache differs from host. Reinstall the same local archive cache."
				# The reliable denial closes the receiving client. Disconnecting here
				# drops that explanation and leaves _command publishing to a dead peer.
				if id!=1:_deny.rpc_id(id,status,str(_peer_challenges.get(id,"")))
				return
			p.loaded=true
			if stage=="loading":_try_start()
			elif stage in ["match","lobby","countdown"] and not p.spectator and (not p.get("rejoining",false) or p.get("needs_spawn",false)):_spawn(id)
		"pickup":
			if not _living(id) or not _pickup(id,str(data.get("id","")),data):return
		"pod_launch":
			if not _pod_launch_r12(id,data):return
		"pod_eject":
			if stage!="match" or not pods_r12.has(id) or pods_r12[id].get("state","")!="flying":return
			_pod_eject_r12(id)
		"drop":
			if not _living(id) or not _drop(id,data):return
		"reload":
			if not _living(id) or not _reload(id,data):return
		"grip":
			var serial=int(data.get("serial",0))
			if serial>int(_strokes.get(id,0)) and serial<=int(_strokes.get(id,0))+2:_strokes[id]=serial
		"consume":
			if not _living(id) or not _consume(id,data):return
		"catalog":
			if not _living(id) or not rules.catalog or not _catalog_item(id,data):return
		"drop_reserve":
			if not _living(id) or not _drop_reserve(id,data):return
		"build":
			if not _living(id) or not rules.builds or not _build(id,data):return
		"shot":
			if not _living(id) or not _shoot(id,data):return
		"melee":
			if not _living(id) or not validator.is_valid():return
			validator.call("melee",id,data)
		"explosive":
			if not _living(id) or not _launch(id,data):return
		"mark":
			if not _living(id) or not _valid_vector(data.get("point")) or not _valid_vector(data.get("normal")):return
			var mark:Dictionary={"player":id,"team":p.team,"point":data.point,"normal":data.normal.normalized(),"color":clampi(int(data.get("color",2)),0,8),"catalog":clampi(int(data.get("catalog",-1)),-1,29),"until":now()+30.0}
			marks[id]=mark;_event("mark",mark)
		"revive":
			if not _living(id) or now()>float(_ready_until.get(id,0)) or not validator.is_valid():return
			var target =int(data.get("id",0))
			if not players.has(target) or players[target].life!="downed" or not allied(id,target):return
			if not validator.call("revive",id,{"target":target}):return
			players[target].life="alive";players[target].health=50.0;players[target].shield=0.0;players[target].protection_until=now()+float(rules.protection);players[target].spawn_revision+=1
			players[target].spawn=_position(target)
			_event("revive",{"player":target,"by":id,"position":_position(target)})
		_ :return
func allied(a:int,b:int)->bool:
	return a==b or (players.has(a) and players.has(b) and int(rules.teams)>0 and int(players[a].team)==int(players[b].team))
func _living(id:int)->bool:return stage in ["match","lobby","countdown"] and players.has(id) and players[id].loaded and players[id].life=="alive"
func can_start()->bool:
	if not world_ready:return false
	var count:=0
	for p in players.values():
		if p.spectator:continue
		count+=1
		if not p.loaded:return false
	return count>=1
func _check_ready()->void:
	status="In-map warmup · Host starts when ready (solo supported)"
func _begin_loading(target:String="match")->void:
	_pose_faults_r14.clear()
	pods_r12.clear();station_ready_r18.clear()
	archive_broken_r8.clear()
	loading_target=target
	projectile_poses.clear();_pending_events.clear();training.clear();zone.clear();match_scores.clear();combat_feed.clear();_last_tick=0.0
	stage="loading";epoch+=1;world_ready=false;builds.clear();marks.clear();poses.clear();entities.clear();_history.clear();_ready_until.clear();_charge.clear();_shot_clock.clear();_last_reload.clear();_strokes.clear();_last_pose.clear();_last_grip.clear()
	loading_deadline=now()+60.0
	for id in players:
		if players[id].get("waiting_round",false):players[id].spectator=false;players[id].waiting_round=false
		players[id].inventory=[];players[id].loaded=false;players[id].ready=false;players[id].needs_spawn=true;players[id].life="spectator" if players[id].spectator else "alive";players[id].kills=0;players[id].deaths=0;players[id].score=0
		_starting_inventory(id)
func _try_start()->void:
	if stage!="loading" or not world_ready:return
	for p in players.values():
		if not p.loaded:return
	stage=loading_target;match_started=now()
	if stage=="match":round_index+=1;zone=zone_engine.reset(rules,match_started);last_result=""
	else:zone.clear();status="In-map warmup · Explore together · Host starts from pause menu"
	for id in players:
		if not players[id].spectator:_spawn(id)
	_event("match_start" if stage=="match" else "warmup",{"round":round_index})
func _spawn(id:int)->void:
	_pose_faults_r14.erase(id)
	var p:Dictionary=players[id]
	p.deployment_r12="";p.erase("pod_key_r12");p.erase("launch_team_r12");p.erase("launch_slot_r12");pods_r12.erase(id)
	p.needs_spawn=false
	p.life="alive";p.health=float(rules.starting_health);p.shield=float(rules.starting_shield);p.soda_left=0.0;p.spawn_revision+=1;p.protection_until=now()+float(rules.protection);p.respawn_at=0.0;p.downed_at=0.0
	var at:Vector3=spawn_provider.call(id,players,rules) if spawn_provider.is_valid() else Vector3((id%4)*3,1,0)
	if stage in ["lobby","countdown"] and warmup_spawn_provider.is_valid():at=warmup_spawn_provider.call(id,players,rules)
	p.spawn=at;poses[id]={"root":Transform3D(Basis.IDENTITY,at),"head":Transform3D(Basis.IDENTITY,at+Vector3.UP*1.7),"left":Transform3D(Basis.IDENTITY,at+Vector3(-.3,1.2,0)),"right":Transform3D(Basis.IDENTITY,at+Vector3(.3,1.2,0)),"weapon":-1,"holstered":true,"seq":0,"time":now()}
	poses[id].epoch=epoch;poses[id].spawn_revision=int(p.spawn_revision)
	_last_pose.erase(id);_history.erase(id)
	if not rules.retain_inventory and p.deaths>0:_starting_inventory(id)
func _starting_inventory(id:int)->void:
	var p:Dictionary=players[id]
	for old in p.inventory:entities.erase(old)
	p.inventory=[];p.reserves={"rifle":2400,"pistol":1200,"shells":480,"smg":1200,"sniper":2400,"frag":12,"zone":12};p.builds=int(rules.starting_builds)
	for cat in Rules.starting_catalog(rules):
		var eid ="p%d_%d_%d"%[id,epoch,cat];var profile =preload("res://scripts/arsenal_data.gd").profile(cat)
		var capacity =int(profile.get("magazine_per_hand",0))*(2 if cat in [11,12] else 1)
		entities[eid]={"id":eid,"kind":"weapon","catalog":cat,"owner":id,"ammo":capacity,"capacity":capacity,"reload":"ready","seated":0,"position":Vector3.ZERO,"basis":Basis.IDENTITY,"rocket_reserve":2 if cat==27 else 0};p.inventory.append(eid)
		_normalize_weapon(entities[eid])
func _normalize_weapon(e:Dictionary)->void:
	if e.kind!="weapon":return
	e.rarity=clampi(int(e.get("rarity",1)),1,5) if RarityR6.tiered(int(e.catalog)) else 1
	e.capacity=RarityR6.capacity(int(e.catalog),int(e.rarity))
	e.ammo=clampi(int(e.ammo),0,int(e.capacity))
	if e.reload=="ready":e.seated=e.ammo
	if int(e.catalog) in [11,12]:
		if not e.has("hands"):e.hands=[ceili(float(e.ammo)*.5),floori(float(e.ammo)*.5)]
		if not e.has("hand_states"):e.hand_states=[e.reload,e.reload]
func register_world(world:Array)->void:
	if not server:return
	for item in world:
		if item is Dictionary and not entities.has(str(item.id)):
			entities[str(item.id)]=item.duplicate(true)
			_normalize_weapon(entities[str(item.id)])
	world_ready=true;_try_start();_publish()
func _valid_vector(v:Variant)->bool:return v is Vector3 and v.is_finite() and v.length()<100000.0
func _position(id:int)->Vector3:
	return poses[id].root.origin if poses.has(id) else players.get(id,{}).get("spawn",Vector3.ZERO)
func _pickup(id:int,eid:String,data:Dictionary)->bool:
	if not entities.has(eid):return false
	var item:Dictionary=entities[eid];var p:Dictionary=players[id]
	if int(item.owner)!=0 or not rules.loot or _position(id).distance_to(item.position)>4.5:return false
	if validator.is_valid() and not validator.call("pickup",id,item):return false
	if item.kind=="ammo":p.reserves[item.family]=int(p.reserves.get(item.family,0))+int(item.amount);entities.erase(eid)
	elif item.kind=="builds":p.builds+=int(item.amount);entities.erase(eid)
	elif item.kind=="backpack":p.capacity=mini(6,int(p.capacity)+1);entities.erase(eid)
	else:
		if _slots(id)>=int(p.capacity):
			var outgoing =str(data.get("replace",""))
			if not p.inventory.has(outgoing) or not _drop(id,{"id":outgoing,"position":data.get("drop",_position(id))}):return false
		item.owner=id;p.inventory.append(eid)
	_event("pickup",{"player":id,"id":eid,"kind":item.kind,"family":item.get("family",""),"position":item.position})
	return true
func _catalog_item(id:int,data:Dictionary)->bool:
	var kind=str(data.get("kind",""));var point=data.get("position")
	if not _valid_vector(point) or _position(id).distance_to(point)>4.5:return false
	_serial+=1;var eid="catalog_%d_%d"%[epoch,_serial]
	var item:Dictionary={"id":eid,"owner":0,"position":point,"basis":Basis.IDENTITY}
	if kind=="weapon":
		var cat=int(data.get("catalog",-1))
		if cat not in preload("res://scripts/weapon_rack.gd").ACTIVE_WEAPON_INDICES:return false
		if not RarityR6.valid(data.get("rarity",1)):return false
		if str(rules.map) in ["archive:magnum-chaosvr","archive:magnum-3-chaosvr"] and cat!=7:return false
		var stars:int=int(data.get("rarity",1)) if RarityR6.tiered(cat) else 1;var capacity:int=RarityR6.capacity(cat,stars)
		item.rarity=stars
		item.merge({"kind":"weapon","catalog":cat,"ammo":capacity,"capacity":capacity,"reload":"ready","seated":capacity,"rocket_reserve":2 if cat==27 else 0})
	elif kind=="supply":
		var family=str(data.get("family",""));var amounts=preload("res://scripts/supply_catalog_088.gd").AMOUNTS
		if not amounts.has(family):return false
		item.merge({"kind":"builds" if family=="builds" else "ammo","family":family,"amount":int(amounts[family])})
	elif kind=="consumable" and str(data.get("item","")) in ["banana","soda","shaker","backpack"]:item.kind=str(data.item)
	else:return false
	if kind in ["weapon","consumable"] and bool(data.get("equip",false)):
		var p:Dictionary=players[id]
		if item.kind=="backpack":
			p.capacity=mini(6,int(p.capacity)+1);_event("pickup",{"player":id,"id":eid,"kind":"backpack","position":point});return true
		if _slots(id)>=int(p.capacity):
			var outgoing:=str(data.get("replace",""))
			if not p.inventory.has(outgoing) or not _drop(id,{"id":outgoing,"position":point}):return false
		item.owner=id;p.inventory.append(eid)
	_normalize_weapon(item);entities[eid]=item
	if int(item.owner)==id:_event("pickup",{"player":id,"id":eid,"kind":item.kind,"position":point})
	return true
func _drop_reserve(id:int,data:Dictionary)->bool:
	var family=str(data.get("family",""));var amounts=preload("res://scripts/supply_catalog_088.gd").AMOUNTS;var p:Dictionary=players[id];var point=data.get("position")
	if not amounts.has(family) or not _valid_vector(point) or _position(id).distance_to(point)>4.5:return false
	var available=int(p.builds) if family=="builds" else int(p.reserves.get(family,0));var amount=mini(available,int(amounts[family]))
	if amount<=0:return false
	_serial+=1;var eid="drop_supply_%d_%d"%[epoch,_serial]
	entities[eid]={"id":eid,"kind":"builds" if family=="builds" else "ammo","family":family,"amount":amount,"owner":0,"position":point,"basis":Basis.IDENTITY}
	if family=="builds":p.builds-=amount
	else:p.reserves[family]=available-amount
	return true
func _slots(id:int)->int:
	var n =0;var counts:Dictionary={}
	for eid in players[id].inventory:
		var e:Dictionary=entities.get(eid,{})
		var key =str(e.get("kind",""))
		if key in ["shaker","soda"] or int(e.get("catalog",-1))==26:counts[key]=int(counts.get(key,0))+1
		else:n+=1
	for count in counts.values():n+=ceili(float(count)/2.0)
	return n
func _drop(id:int,data:Dictionary)->bool:
	var eid =str(data.get("id",""));var point=data.get("position")
	if not entities.has(eid) or not players[id].inventory.has(eid) or not _valid_vector(point) or _position(id).distance_to(point)>4.5:return false
	if validator.is_valid() and not validator.call("drop",id,{"position":point}):return false
	var e:Dictionary=entities[eid];e.owner=0;e.position=point;e.basis=Basis.IDENTITY;players[id].inventory.erase(eid)
	return true
func _reload(id:int,data:Dictionary)->bool:
	var eid =str(data.get("id",""));var state =str(data.get("state",""))
	if not entities.has(eid) or int(entities[eid].owner)!=id or state.length()>32:return false
	var e:Dictionary=entities[eid];var side=clampi(int(data.get("side",-1)),-1,1);var paired=int(e.catalog) in [11,12]
	_normalize_weapon(e)
	var old =str(e.hand_states[maxi(side,0)]) if paired else str(e.reload)
	if state==old and not (paired and state=="ready"):return false
	var allowed:Dictionary={"ready":["needs_mag","open","needs_bolt","needs_cover","needs_rack","needs_shells","empty","eject","needs_flip","needs_ammo"],"needs_mag":["held_mag","needs_rack","ready","open"],"held_mag":["needs_mag","needs_rack","needs_bolt","ready"],"needs_rack":["ready"],"needs_bolt":["ready"],"needs_cover":["cover_open","open"],"cover_open":["needs_belt","belt_held","held_belt"],"needs_belt":["belt_held","held_belt","belt_ready"],"belt_held":["belt_ready","belt_seated"],"held_belt":["belt_ready","belt_seated"],"belt_ready":["needs_close","needs_charge","needs_rack","cover_closed"],"belt_seated":["needs_close","needs_charge","needs_rack"],"needs_close":["needs_charge","needs_rack","ready"],"needs_charge":["ready"],"cover_closed":["needs_charge","needs_rack","ready"],"open":["held_shells","loaded","ready","needs_shells"],"held_shells":["loaded","ready","open"],"loaded":["ready"],"needs_shells":["held_shells","loaded","ready"],"empty":["needs_mag","open","ready"],"needs_flip":["ready"],"eject":["ready"],"needs_ammo":["ready","held_mag"]}
	allowed.ready.append_array(["cover_open","cylinder_open","open_empty","holding_shells"])
	if int(e.catalog)==8:allowed.ready.append("box_removed")
	allowed["cover_open"]=["box_removed"]
	allowed["box_removed"]=["holding_box","needs_feed"]
	allowed["holding_box"]=["box_removed","needs_feed"]
	allowed["needs_feed"]=["box_seated"]
	allowed["box_seated"]=["needs_charge"]
	allowed["cylinder_open"]=["cylinder_loaded"]
	allowed["cylinder_loaded"]=["ready"]
	allowed["open_empty"]=["holding_shells","open_loaded"]
	allowed["holding_shells"]=["open_empty","open_loaded"]
	allowed["open_loaded"]=["ready"]
	allowed.needs_bolt.append("needs_mag")
	if state not in allowed.get(old,[]) and not (paired and state=="ready"):return false
	var cat =int(e.catalog)
	var permitted:Array=["ready","needs_mag","held_mag","needs_rack"]
	if cat==8:permitted=["ready","box_removed","holding_box","needs_feed","box_seated","needs_charge","cover_open"]
	elif cat==1:permitted=["ready","open_empty","holding_shells","open_loaded"]
	elif cat==7:permitted=["ready","cylinder_open","cylinder_loaded"]
	elif cat==5:permitted=["ready","needs_mag","held_mag","needs_bolt"]
	elif cat in [11,12]:permitted=["ready","needs_mag"]
	if state not in permitted:return false
	if cat in [26,28] or (cat==27 and int(e.rocket_reserve)<=0):return false
	if old=="ready" and state=="needs_bolt" and cat==5:e.cycle_only=true
	if state in ["held_mag","holding_box","open_empty","cylinder_open"]:e.cycle_only=false
	if int(e.get("stroke_owner",0))!=id:e.stroke_owner=id;e.last_stroke=-1
	if bool(data.get("xr",false)) and ((cat==8 and state in ["needs_feed","box_seated","needs_charge","ready"]) or (cat not in [1,7,11,12] and state in ["needs_rack","ready"]) or (cat==5 and state=="needs_bolt" and not e.get("cycle_only",false))):
		var serial=int(data.get("grip_serial",0))
		if serial<=int(e.get("last_stroke",-1)) or serial>int(_strokes.get(id,0)):return false
		e.last_stroke=serial
	# Inserted rounds belong to the weapon, including while it is dropped or
	# transferred before the charging/closing stroke. Allocate them exactly once.
	if not paired and cat!=27 and (state in ["needs_mag","box_removed"] or (cat==1 and state=="open_empty")):
		# Real magazine/box removal returns its remaining rounds to the holder.
		# Mirror that one-time refund before synchronizing the actual gun/reserve.
		var returned=int(e.get("staged_ammo",e.ammo));var family=str(preload("res://scripts/weapon_rack.gd").AMMO_FAMILIES[cat])
		if not rules.infinite_ammo:players[id].reserves[family]=int(players[id].reserves.get(family,0))+returned
		e.ammo=0;e.seated=0;e.erase("staged_ammo")
	var inserting=state in ["needs_rack","needs_feed","open_loaded","cylinder_loaded"] or (cat==5 and state=="needs_bolt" and not e.get("cycle_only",false))
	if inserting and not paired and cat!=27 and not e.has("staged_ammo"):
		var family=str(preload("res://scripts/weapon_rack.gd").AMMO_FAMILIES[cat]);var reserve=int(players[id].reserves.get(family,0));var current=int(e.ammo);var wanted=maxi(0,int(e.capacity)-current)
		var take=wanted if rules.infinite_ammo else mini(wanted,reserve)
		if current+take<=0:return false
		e.staged_ammo=current+take;e.seated=e.staged_ammo
		if not rules.infinite_ammo:players[id].reserves[family]=reserve-take
	if state=="ready" and not e.get("cycle_only",false):
		var reload_key=eid+":"+str(side)
		var last =float(_last_reload.get(reload_key,-100.0))
		if now()-last<.08:return false
		var family =str(preload("res://scripts/weapon_rack.gd").AMMO_FAMILIES[cat]);var reserve =int(players[id].reserves.get(family,0))
		if cat==27:e.ammo=1;e.rocket_reserve-=1
		elif e.has("staged_ammo"):e.ammo=int(e.staged_ammo);e.erase("staged_ammo")
		else:
			var current=int(e.hands[maxi(0,side)]) if paired else int(e.ammo)
			var capacity=int(e.capacity)/2 if paired else int(e.capacity)
			var wanted =maxi(0,int(capacity)-current);var take =wanted if rules.infinite_ammo else mini(wanted,reserve)
			if take<=0 and int(e.ammo)<=0:return false
			if paired:e.hands[maxi(0,side)]+=take;e.ammo=int(e.hands[0])+int(e.hands[1])
			else:e.ammo+=take
			if not rules.infinite_ammo:players[id].reserves[family]=reserve-take
		_last_reload[reload_key]=now()
	if paired:e.hand_states[maxi(0,side)]=state;e.reload="ready" if e.hand_states.has("ready") else state
	else:e.reload=state
	if state=="ready":e.seated=e.ammo
	elif e.has("staged_ammo"):e.seated=int(e.staged_ammo)
	_event("reload",{"player":id,"catalog":cat,"state":state,"position":_position(id)})
	return true
func _consume(id:int,data:Dictionary)->bool:
	var eid =str(data.get("id",""))
	if not entities.has(eid) or int(entities[eid].owner)!=id:return false
	var e:Dictionary=entities[eid];var p:Dictionary=players[id]
	if e.kind not in ["banana","soda","shaker"]:return false
	if validator.is_valid() and not validator.call("consume",id,data):return false
	if e.kind=="banana":p.health=minf(100,float(p.health)+100.0)
	elif e.kind=="shaker":p.shield=minf(100,float(p.shield)+50.0)
	else:p.soda_left+=50.0
	p.inventory.erase(eid);entities.erase(eid);_event("consume",{"player":id,"kind":e.kind});return true
func _build(id:int,data:Dictionary)->bool:
	var entries=data.get("entries",[])
	if not entries is Array or entries.is_empty() or entries.size()>2 or builds.size()+entries.size()>256:return false
	if not rules.infinite_builds and int(players[id].builds)<entries.size():return false
	if not validator.is_valid() or not validator.call("build",id,data):return false
	for entry in entries:
		_serial+=1;var bid ="build_%d_%d"%[epoch,_serial]
		builds[bid]={"id":bid,"kind":entry.kind,"at":entry.at,"key":entry.key,"support":entry.get("support",false),"health":180.0 if entry.kind=="wall" else 140.0,"owner":id}
	if not rules.infinite_builds:players[id].builds-=entries.size()
	_event("build",{"player":id,"position":entries[0].at.origin});return true
func hurt_build(bid:String,damage:float)->void:
	if not server or not builds.has(bid) or not is_finite(damage) or damage<=0:return
	builds[bid].health=maxf(0,float(builds[bid].health)-damage*float(rules.build_damage))
	if builds[bid].health<=0:builds.erase(bid)
	_publish()
func _shoot(id:int,data:Dictionary)->bool:
	var eid =str(data.get("id",""));var origin=data.get("origin");var directions=data.get("directions",[])
	if not entities.has(eid) or int(entities[eid].owner)!=id or not _valid_vector(origin) or not directions is Array or directions.is_empty() or directions.size()>8:return false
	if _position(id).distance_to(origin)>5.0:return false
	var e:Dictionary=entities[eid];var cat =int(e.catalog)
	if cat in [14,15,16,26,27,28,29] or int(e.ammo)<=0 or e.reload!="ready":return false
	var profile =preload("res://scripts/arsenal_data.gd").profile(cat)
	var interval =1.0/float(profile.rounds_per_second)
	var shot_key=eid+":"+str(clampi(int(data.get("side",0)),0,1)) if cat in [11,12] else eid
	# R18-NET shot timing. R17 gated on packet ARRIVAL, so correctly spaced rounds delivered
	# together (jitter, slow host frame) were refused and then refunded/re-fired by the client.
	# Policy (per shot_key = gun, or gun+hand for paired guns; state retired with _shot_clock):
	#  1. stamp must be finite and within [now-.75, now+.05] -> otherwise REJECT (never clamped);
	#  2. stamps per key never go backwards (ENet reliable channel 0 is ordered);
	#  3. cadence on the shooter's timeline: credit<=stamp+gap, gap=.65*interval (R17 tolerance),
	#     credit=max(credit,stamp)+gap -> one same-stamp catch-up round from a long frame;
	#  4. host wall-clock budget independent of stamps: <=ceil(1/interval)+2 arrivals per 1 s and
	#     <=ceil(.25/interval)+2 per .25 s (Uzi hand: 15 and 6).
	var shot_time:float=float(data.get("time",NAN))
	if not is_finite(shot_time) or shot_time>now()+.05 or shot_time<now()-.75:return false
	var gap:float=interval*.65
	var timing:Dictionary=_shot_clock.get(shot_key,{}) if _shot_clock.get(shot_key) is Dictionary else {}
	if shot_time<float(timing.get("last",-INF)) or float(timing.get("credit",-INF))>shot_time+gap+.000001:return false
	var arrivals:Array=timing.get("arrivals",[]).filter(func(at:float)->bool:return now()-at<1.0)
	if arrivals.size()>=ceili(1.0/interval)+2 or arrivals.filter(func(at:float)->bool:return now()-at<.25).size()>=ceili(.25/interval)+2:return false
	for d in directions:
		if not _valid_vector(d) or d.length()<.9 or d.length()>1.1:return false
	var pellets =int(preload("res://scripts/one_star0991.gd").data().get(str(profile.id),{}).get("pellets",1))
	if directions.size()>pellets:return false
	if cat in [11,12]:
		_normalize_weapon(e);var side=clampi(int(data.get("side",0)),0,1)
		if int(e.hands[side])<=0 or e.hand_states[side]!="ready":return false
		e.hands[side]-=1
	arrivals.append(now());_shot_clock[shot_key]={"credit":maxf(float(timing.get("credit",-INF)),shot_time)+gap,"last":shot_time,"arrivals":arrivals}
	e.ammo-=1;data=data.duplicate(true);data.catalog=cat;data.rarity=int(e.get("rarity",1));data.player=id;data.time=clampf(shot_time,now()-.20,now());data.head_ratio=float(rules.headshots)
	if validator.is_valid():validator.call("shot",id,data)
	_event("shot",data);return true
func _launch(id:int,data:Dictionary)->bool:
	var eid =str(data.get("id",""))
	if not entities.has(eid) or int(entities[eid].owner)!=id:return false
	var e:Dictionary=entities[eid];var cat =int(e.get("catalog",-1))
	if cat not in [26,27,28] or int(e.ammo)<=0:return false
	if not validator.is_valid() or not validator.call("explosive",id,data):return false
	e.ammo-=1
	if cat in [26,28] or (cat==27 and int(e.rocket_reserve)<=0):players[id].inventory.erase(eid);entities.erase(eid)
	else:e.reload="needs_mag"
	return true
func hurt(id:int,amount:float,by:int=0,headshot:bool=false,direct:bool=false,weapon_catalog:int=-1,cause:String="")->void:
	if stage!="match" or not server or not _living(id) or not is_finite(amount) or amount<=0:return
	if by!=0 and by!=id and allied(by,id) and not rules.friendly_fire:return
	var p:Dictionary=players[id]
	if now()<float(p.protection_until):return
	if cause!="zone":amount*=float(rules.damage)
	var absorbed =0.0 if direct else minf(float(p.shield),amount);p.shield-=absorbed;p.health=maxf(0,float(p.health)-(amount-absorbed))
	_event("hit",{"player":id,"by":by,"amount":amount,"shield":absorbed>0,"headshot":headshot,"position":poses.get(id,{}).get("head",Transform3D.IDENTITY).origin})
	if p.health<=0:
		_die_r13(id,by,weapon_catalog,cause)
	_publish()
func _die_r13(id:int,by:int=0,catalog:int=-1,cause:String="",revivable:bool=true)->void:
	if not server or stage!="match" or not players.has(id) or players[id].life!="alive":return
	var p:Dictionary=players[id];p.health=0.0;p.shield=0.0;p.soda_left=0.0;p.deaths+=1
	if by!=id and players.has(by) and not allied(by,id):
		players[by].kills+=1;players[by].score+=1
		var team_key:int=int(players[by].team) if int(rules.teams)>0 else by
		match_scores[team_key]=int(match_scores.get(team_key,0))+1
	if revivable and rules.mode=="revive" and int(rules.teams)>0:p.life="downed";p.downed_at=now()
	else:p.life="eliminated";p.respawn_at=now()+float(rules.respawn_delay) if rules.mode=="respawn" else 0.0
	_record_knockdown(id,by,catalog,cause);_check_end()
func _check_end()->void:
	if stage!="match":return
	if int(rules.score_limit)>0:
		var scores:Dictionary={}
		for id in players:
			var key =int(players[id].team) if int(rules.teams)>0 else int(id);scores[key]=int(scores.get(key,0))+int(players[id].score)
		for key in match_scores:scores[key]=match_scores[key]
		for score in scores.values():
			if int(score)>=int(rules.score_limit):stage="results"
	if rules.mode=="revive" and players.size()>1:
		var alive:Dictionary={};var all:Dictionary={}
		for id in players:
			if players[id].spectator:continue
			var key =int(players[id].team) if int(rules.teams)>0 else int(id);all[key]=true
			if players[id].life=="alive":alive[key]=true
		if all.size()>1 and alive.size()<=1:stage="results"
func _event(kind:String,data:Dictionary)->void:
	data=data.duplicate(true);data.net_epoch=epoch;data.session_id=_session_nonce
	event_received.emit(kind,data)
	if server:_broadcast_live(_event_rpc,[kind,data],2)
@rpc("authority","call_remote","reliable",2)
func _event_rpc(kind:String,data:Dictionary)->void:
	if not active or server or str(data.get("session_id",""))!=_session_nonce:return
	var event_epoch:int=int(data.get("net_epoch",epoch))
	if event_epoch<epoch:return
	if event_epoch>epoch:
		if _pending_events.size()<256:_pending_events.append({"kind":kind,"data":data})
		return
	if kind in MATCH_EVENTS and stage!="match":
		if stage=="loading" and _pending_events.size()<256:_pending_events.append({"kind":kind,"data":data})
		return
	event_received.emit(kind,data)
@rpc("authority","call_remote","unreliable_ordered",1)
func _flight(data:Dictionary)->void:
	if active and not server and stage=="match" and int(data.get("net_epoch",-1))==epoch and str(data.get("session_id",""))==_session_nonce:event_received.emit("projectile_pose",data)
func snapshot()->Dictionary:
	var safe_players =players.duplicate(true)
	for row in safe_players.values():row.erase("token")
	var state:Dictionary={"stage":stage,"rules":rules,"revision":revision,"epoch":epoch,"players":safe_players,"entities":entities,"builds":builds,"marks":marks,"admin":admin,"room":room_name,"started":match_started,"countdown":countdown_end,"round":round_index,"time":now(),"ack":_seen}
	state.projectiles=projectile_poses
	state.training=training
	state.state_seq=_state_serial
	state.session_id=_session_nonce
	state.zone=zone;state.match_scores=match_scores;state.combat_feed=combat_feed;state.loading_target=loading_target;state.last_result=last_result
	state.world_ready=world_ready;state.archive_broken_r8=archive_broken_r8
	state.pods_r12=pods_r12;state.station_ready_r18=station_ready_r18
	return state.duplicate(true)
func _publish()->void:
	if not server:return
	_state_serial+=1
	var state:Dictionary=snapshot()
	if _published.is_empty():_broadcast_live(_state,[state],3)
	else:
		var patch:Dictionary=state.duplicate();patch.sets={};patch.removed={};patch.base_seq=int(_published.state_seq)
		for key in ["players","entities","builds","marks"]:
			patch.erase(key);var added:Dictionary={};var removed:Array=[]
			for id in state[key]:
				if not _published.get(key,{}).has(id) or _published[key][id]!=state[key][id]:added[id]=state[key][id]
			for id in _published.get(key,{}):
				if not state[key].has(id):removed.append(id)
			patch.sets[key]=added;patch.removed[key]=removed
		_broadcast_live(_delta,[patch],3)
	_published=state;changed.emit()
@rpc("authority","call_remote","reliable",3)
func _delta(patch:Dictionary)->void:
	if not active or server or str(patch.get("session_id",""))!=_session_nonce or int(patch.epoch)<epoch or (int(patch.epoch)==epoch and int(patch.get("state_seq",-1))<=_received_serial):return
	if int(patch.get("base_seq",-2))!=_received_serial:
		if not _resync_waiting:_request_full_state()
		return
	var dictionaries:Dictionary={"players":players.duplicate(),"entities":entities.duplicate(),"builds":builds.duplicate(),"marks":marks.duplicate()}
	for key in dictionaries:
		for id in patch.removed[key]:dictionaries[key].erase(id)
		dictionaries[key].merge(patch.sets[key],true)
	var state:Dictionary=patch.duplicate();state.merge(dictionaries,true);_state(state)
@rpc("authority","call_remote","reliable",3)
func _state(s:Dictionary)->void:
	if not active or server or str(s.get("session_id",""))!=_session_nonce or int(s.epoch)<epoch:return
	var serial=int(s.get("state_seq",-1))
	if int(s.epoch)==epoch and serial<=_received_serial and not (_resync_waiting and serial==_received_serial):return
	if _resync_waiting:resync_applied+=1
	_received_serial=serial;_resync_waiting=false
	if int(s.epoch)!=epoch:poses.clear()
	stage=s.stage;rules=s.rules;revision=int(s.revision);epoch=int(s.epoch);players=s.players;entities=s.entities;builds=s.builds;marks=s.marks;admin=int(s.admin);room_name=s.room;match_started=float(s.started);countdown_end=float(s.countdown);round_index=int(s.round)
	projectile_poses=s.get("projectiles",{})
	training=s.get("training",{})
	for id in poses.keys():
		if not players.has(id):poses.erase(id)
	zone=s.get("zone",{});match_scores=s.get("match_scores",{});combat_feed=s.get("combat_feed",[]);loading_target=str(s.get("loading_target","match"));last_result=str(s.get("last_result",""))
	world_ready=bool(s.get("world_ready",false));archive_broken_r8=s.get("archive_broken_r8",{})
	pods_r12=s.get("pods_r12",{});station_ready_r18=s.get("station_ready_r18",{})
	var half_trip:float=float(players.get(me(),{}).get("ping",0))*.0005
	server_offset=float(s.time)+half_trip-Time.get_ticks_msec()*.001
	acknowledged=int(s.get("ack",{}).get(me(),0));pending=pending.filter(func(v:Dictionary)->bool:return int(v.seq)>acknowledged);changed.emit()
	var queued:Array=_pending_events;_pending_events=[]
	for event in queued:_event_rpc(event.kind,event.data)
@rpc("any_peer","call_remote","reliable",0)
func _resync()->void:
	var id=multiplayer.get_remote_sender_id()
	if not server or not players.has(id) or players[id].get("kicked_r18",false):return
	if now()-float(_resync_last.get(id,-1000))<.25:resync_denied+=1;return
	_resync_last[id]=now();_state.rpc_id(id,snapshot())
func _request_full_state()->void:
	if not server and active and multiplayer.multiplayer_peer is ENetMultiplayerPeer and _transport_ready():
		_resync_waiting=true;_resync_deadline=Time.get_ticks_msec()+1000;resync_requests+=1;_resync.rpc_id(1)
func send_pose(packet:Dictionary)->void:
	if not active:return
	if not server and (not _transport_ready() or not players.has(me())):return
	packet=packet.duplicate(true);packet.net_epoch=epoch;packet.session_id=_session_nonce
	if server:_accept_pose(1,packet)
	else:_pose.rpc_id(1,packet)
func _transport_ready()->bool:
	return multiplayer.multiplayer_peer!=null and multiplayer.multiplayer_peer.get_connection_status()==MultiplayerPeer.CONNECTION_CONNECTED
@rpc("any_peer","call_remote","unreliable_ordered",1)
func _pose(packet:Dictionary)->void:
	if server and int(packet.get("net_epoch",-1))==epoch and str(packet.get("session_id",""))==_session_nonce:_accept_pose(multiplayer.get_remote_sender_id(),packet)
func _accept_pose(id:int,p:Dictionary)->void:
	if stage not in ["match","lobby","countdown"] or not players.has(id) or not players[id].loaded or players[id].get("kicked_r18",false) or int(p.get("net_epoch",epoch))!=epoch or str(p.get("session_id",_session_nonce))!=_session_nonce or not _rate(-id,45):return
	if int(p.get("epoch",-1))!=epoch or int(p.get("spawn_revision",-1))!=int(players[id].spawn_revision):return
	for key in ["root","head"]:
		if not p.get(key) is Transform3D or not p[key].is_finite() or absf(p[key].basis.determinant()-1.0)>.10:_reject_pose_r14(id,"body_transform");return
	if p.head.origin.distance_to(p.root.origin)>5.0:_reject_pose_r14(id,"head_offset");return
	if int(p.get("seq",0))<=int(_last_pose.get(id,{}).get("seq",-1)):return
	# A lost/stuck controller must not freeze valid body position, zone damage or
	# build/contact reach. Neutralize that hand and its interactions independently.
	for key in ["left","right"]:
		var hand=p.get(key)
		if hand is Transform3D and hand.is_finite() and absf(hand.basis.determinant()-1.0)<=.10 and hand.origin.distance_to(p.root.origin)<=5.0:continue
		p=p.duplicate(true);p[key]=Transform3D(p.root.basis,p.head.origin+p.root.basis*Vector3(-.3 if key=="left" else .3,-.35,-.25));p["grip_"+key]=0.0;p.paddles=false;p.shield=false
		pose_stats_r14.sanitized_hands+=1
	var carried_r12:bool=pods_r12.has(id) and pods_r12[id].get("state","")=="flying"
	if carried_r12:
		p=p.duplicate(true);var correction:Vector3=pod_position_r12(pods_r12[id])-p.root.origin
		for key in ["root","head","left","right","gun_pose","shield_plane","pair0","pair1"]:
			if p.get(key) is Transform3D:p[key].origin+=correction
	var previous:Dictionary=_last_pose.get(id,{})
	if not previous.is_empty() and stage=="match" and not carried_r12:
		var elapsed =clampf(now()-float(previous.time),.001,.5)
		var allowance =maxf(3.0,55.0*elapsed*maxf(1.0,maxf(float(rules.fling),maxf(float(rules.flight),maxf(float(rules.walk),float(rules.run))))))
		var travel:Vector3=p.root.origin-previous.root.origin
		# Horizontal tuning is not a terminal-fall limit. Long sky drops and dive
		# legitimately exceed 55 m/s; the old combined gate latched on forever.
		if Vector2(travel.x,travel.z).length()>allowance or travel.y>maxf(allowance,120*elapsed) or -travel.y>maxf(3.0,360*elapsed):_reject_pose_r14(id,"speed");return
	var packet:Dictionary={"root":p.root,"head":p.head,"left":p.left,"right":p.right,"weapon":clampi(int(p.get("weapon",-1)),-1,29),"holstered":bool(p.get("holstered",true)),"grip_left":clampf(float(p.get("grip_left",0)),0,1),"grip_right":clampf(float(p.get("grip_right",0)),0,1),"paddles":bool(p.get("paddles",false)),"shield":bool(p.get("shield",false)),"seq":int(p.get("seq",0)),"time":now()}
	packet.epoch=epoch;packet.spawn_revision=int(players[id].spawn_revision)
	packet.rarity=1
	for eid in players[id].inventory:
		var item:Dictionary=entities.get(eid,{})
		if item.get("kind","")=="weapon" and int(item.catalog)==int(packet.weapon) and int(item.get("rarity",1))==int(p.get("rarity",1)):
			packet.rarity=int(item.get("rarity",1));break
	for key in ["gun_pose","shield_plane","pair0","pair1"]:
		if p.get(key) is Transform3D and p[key].is_finite() and p[key].origin.distance_to(p.root.origin)<5.0:packet[key]=p[key]
	packet.shield=packet.shield and int(packet.weapon) in [14,16] and packet.has("shield_plane") and not packet.holstered
	packet.desktop=bool(p.get("desktop",false));packet.motion=str(p.get("motion","ground")).left(16)
	packet.hand="left" if p.get("hand","right")=="left" else "right"
	# A server-generated carrier path is authoritative; sweeping the passenger against the
	# moving cabin itself would reject valid poses on a low-frame-rate client.
	if validator.is_valid() and stage=="match" and not carried_r12 and not validator.call("movement",id,packet):_reject_pose_r14(id,"world_sweep");return
	poses[id]=packet;_last_pose[id]=packet
	_pose_faults_r14.erase(id);pose_stats_r14.accepted+=1
	var history:Array=_history.get(id,[]);history.append(packet)
	while history.size()>20:history.pop_front()
	_history[id]=history
	_step_charge(id,packet,previous)
	poses_changed.emit()
func _reject_pose_r14(id:int,reason:String):
	pose_stats_r14.rejected+=1
	if not server or stage!="match" or not players.has(id) or not poses.has(id):return
	var fault:Dictionary=_pose_faults_r14.get(id,{"first":now(),"reason":reason});_pose_faults_r14[id]=fault
	if now()-float(fault.first)<.25:return
	var at:Vector3=poses[id].root.origin
	_relocate_r12(id,at);_pose_faults_r14.erase(id);pose_stats_r14.corrected+=1
	print("POSE_RECOVERY_R14 peer=",id," reason=",reason," revision=",players[id].spawn_revision)
	_publish()
@rpc("authority","call_remote","unreliable_ordered",1)
func _remote_pose(id:int,p:Dictionary,round_epoch:int,nonce:String="")->void:
	if not active or server or stage not in ["match","lobby","countdown"] or round_epoch!=epoch or nonce!=_session_nonce or not players.has(id):return
	if int(p.get("spawn_revision",-1))!=int(players[id].spawn_revision):return
	if not poses.has(id) or float(p.time)>=float(poses[id].time):poses[id]=p
	poses_changed.emit()
func historical_pose(id:int,time:float)->Dictionary:
	var history:Array=_history.get(id,[]);var best:Dictionary=poses.get(id,{})
	var distance =INF
	for p in history:
		var d =absf(float(p.time)-time)
		if d<distance:distance=d;best=p
	return best
func _step_charge(id:int,p:Dictionary,previous:Dictionary)->void:
	if not _living(id) or not p.paddles:_charge[id]=0.0;return
	if now()<float(_ready_until.get(id,0)):return
	var dt =clampf(now()-float(previous.get("time",now())),0,.10)
	var facing =(-p.left.basis.z).dot(-p.right.basis.z)<=-.20
	var value =float(_charge.get(id,0.0));var speed =0.0
	if not previous.is_empty() and dt>0:
		var left:Vector3=p.root.affine_inverse()*p.left.origin;var old_left:Vector3=previous.root.affine_inverse()*previous.left.origin
		var right:Vector3=p.root.affine_inverse()*p.right.origin;var old_right:Vector3=previous.root.affine_inverse()*previous.right.origin
		speed=maxf(left.distance_to(old_left),right.distance_to(old_right))/dt
	if p.get("desktop",false) and facing:speed=.5
	if facing and speed<=8.0:value+=dt/lerpf(10.0,5.5,clampf(speed/.5,0,1))
	elif not facing:value-=dt/2.5
	_charge[id]=clampf(value,0,1)
	if value>=1:_ready_until[id]=now()+3.5;_charge[id]=0.0;_event("paddles_ready",{"player":id,"until":_ready_until[id]})
@rpc("authority","call_remote","unreliable",2)
func _ping(stamp:float)->void:
	if not server:_pong.rpc_id(1,stamp)
@rpc("any_peer","call_remote","unreliable",2)
func _pong(stamp:float)->void:
	if not server:return
	var id=multiplayer.get_remote_sender_id()
	if players.has(id) and stamp<=now() and now()-stamp<10:players[id].ping=roundi((now()-stamp)*1000)
# SceneMultiplayer can retain a peer while ENet has already retired its channels.
# Check the transport at each send, including peers closing after a denial/kick.
func _broadcast_live(method:Callable,args:Array,channel:int)->void:
	var transport=multiplayer.multiplayer_peer
	if not transport is ENetMultiplayerPeer:return
	for id in multiplayer.get_peers():
		var peer:ENetPacketPeer=transport.get_peer(id)
		if peer!=null and peer.get_state()==ENetPacketPeer.STATE_CONNECTED and peer.get_channels()>channel:
			method.rpc_id.callv([id]+args)

func _process(delta:float)->void:
	if not active:return
	if not server:
		if _welcome_deadline>0 and Time.get_ticks_msec()>=_welcome_deadline:_lost("Handshake timed out. Host and clients must use the same ASHFALL build.");return
		if _resync_waiting and Time.get_ticks_msec()>=_resync_deadline:_request_full_state()
		return
	_step_pods_r12()
	if _expire_reconnects():_publish()
	_pose_clock+=delta
	if _pose_clock>=.04:
		_pose_clock=0
		for id in poses:_broadcast_live(_remote_pose,[int(id),poses[id],epoch,_session_nonce],1)
	_heartbeat+=delta
	if _heartbeat>=1.0:
		_heartbeat=0;_broadcast_live(_ping,[now()],2);_publish()
	if stage=="countdown" and now()>=countdown_end:_begin_loading();_publish()
	if stage=="loading" and now()>loading_deadline:
		stage="lobby";status="Loading timed out; check the missing player's map/build.";_publish()
	if stage=="results":
		last_result="ROUND COMPLETE · " + str(match_scores)
		_begin_loading("lobby");_publish();return
	if stage in ["lobby","countdown"]:
		for id in players:
			if players[id].loaded and poses.has(id) and poses[id].root.origin.y<-25:_spawn(id)
	if stage in ["lobby","countdown","match"] and station_contact_r18.is_valid():
		_heal_clock_r14+=delta
		if _heal_clock_r14>=.1:
			_heal_clock_r14=0.0;var healed=false
			for id in players:
				if not _living(id) or (float(players[id].health)>=100 and float(players[id].shield)>=100):continue
				var station:Dictionary=station_contact_r18.call(id)
				if station.is_empty():continue
				var key:String=str(station.get("key",""))
				if key.is_empty() or key.length()>64 or now()<float(station_ready_r18.get(key,-INF)):continue
				players[id].health=100.0;players[id].shield=100.0;station_ready_r18[key]=now()+15.0;healed=true
				_event("station_restore_r18",{"id":id,"key":key})
			if healed:_publish()
	if stage!="match":return
	_last_tick+=delta
	if _last_tick<.10:return
	var dt =_last_tick;_last_tick=0;var dirty =false
	var advanced:Dictionary=zone_engine.advance(zone,rules,now())
	if advanced!=zone:zone=advanced;dirty=true
	if not zone.is_empty():
		var rect:Rect2=ZoneR3.current(zone,now());var origin:Vector3=preload("res://scripts/map_catalog1000.gd").info(str(rules.map)).origin
		for id in players:
			if _living(id) and not ZoneR3.contains(rect,_position(id),origin):hurt(id,float(rules.zone_damage)*dt,0,false,true,-1,"zone")
	for id in players:
		var p:Dictionary=players[id]
		if p.life=="alive" and poses.has(id) and poses[id].root.origin.y<-25:
			_die_r13(id,0,-1,"fall",false);dirty=true
		if p.life=="eliminated" and rules.mode=="respawn" and now()>=float(p.respawn_at):_spawn(id);dirty=true
		if p.life=="downed" and now()-float(p.downed_at)>=float(rules.downed_seconds):p.life="eliminated";dirty=true
		if p.life in ["downed","eliminated"] and ghost_recovery_provider_r13.is_valid():
			var recovered:Vector3=ghost_recovery_provider_r13.call(id)
			if recovered.is_finite():_relocate_r12(id,recovered);dirty=true
		if p.life=="alive" and float(p.soda_left)>0:
			var portion =minf(float(p.soda_left),dt*5);p.soda_left-=portion;p.shield=minf(100,float(p.shield)+portion);dirty=true
	if int(rules.time_limit)>0 and now()-match_started>=float(rules.time_limit):stage="results";dirty=true
	if stage=="results" and round_index<int(rules.rounds):_begin_loading();dirty=true
	_check_end()
	if dirty:_publish()
## R18-NET: the denial RPC lets a cooperative client show its reason first; after a short grace
## the host drops the transport itself so a modified client cannot ignore a kick.
func _enforce_kick_r18(target:int)->void:
	var session:String=_session_nonce
	await get_tree().create_timer(1.0).timeout
	if not is_instance_valid(self) or not is_inside_tree() or session!=_session_nonce:return
	if active and server and players.has(target) and players[target].get("kicked_r18",false) and multiplayer.multiplayer_peer is ENetMultiplayerPeer:
		multiplayer.multiplayer_peer.disconnect_peer(target)
func _expire_reconnects()->bool:
	var dirty=false
	for token in _reconnect.keys():
		if now()<float(_reconnect[token].until):continue
		var old:Dictionary=_reconnect[token].player
		for eid in old.inventory:
			if entities.has(eid) and int(entities[eid].owner)==-int(old.id):
				if stage=="match":entities[eid].owner=0;entities[eid].position=old.get("spawn",Vector3.ZERO)+Vector3.UP*.5
				else:
					entities.erase(eid);_shot_clock.erase(eid)
					for key in _shot_clock.keys():
						if str(key).begins_with(str(eid)+":"):_shot_clock.erase(key)
					for key in _last_reload.keys():
						if str(key).begins_with(str(eid)+":"):_last_reload.erase(key)
		_reconnect.erase(token);dirty=true
	return dirty

func _record_knockdown(victim:int,attacker:int,catalog:int,cause:String)->void:
	var entry:Dictionary={"victim":victim,"attacker":attacker,"victim_name":players[victim].name,"attacker_name":players.get(attacker,{}).get("name","ZONE" if cause=="zone" else "WORLD"),"catalog":catalog,"downed":players[victim].life=="downed","until":now()+5.0,"epoch":epoch,"team":int(players.get(attacker,{}).get("team",-1))}
	combat_feed.push_front(entry)
	if combat_feed.size()>4:combat_feed.resize(4)
	_event("knockdown",entry)


func pod_position_r12(ride:Dictionary,stamp:float=NAN)->Vector3:
	if ride.is_empty():return Vector3.ZERO
	var when:float=now() if is_nan(stamp) else stamp
	var clear:Vector3=ride.get("clear",ride.start);var first:float=(ride.start as Vector3).distance_to(clear);var second:float=clear.distance_to(ride.end)
	var cruise:Vector3=ride.get("cruise",ride.end);second=clear.distance_to(cruise);var third:float=cruise.distance_to(ride.end)
	var distance:float=(first+second+third)*clampf((when-float(ride.time))/maxf(.01,float(ride.duration)),0,1)
	if distance<first:return (ride.start as Vector3).lerp(clear,distance/maxf(.001,first))
	if distance<first+second:return clear.lerp(cruise,clampf((distance-first)/maxf(.001,second),0,1))
	return cruise.lerp(ride.end,clampf((distance-first-second)/maxf(.001,third),0,1))
func _pod_launch_r12(id:int,data:Dictionary)->bool:
	if not preload("res://scripts/team_launch_r12.gd").uses_sky_platform(str(rules.map)):return false
	if stage!="match" or rules.mode!="revive" or int(rules.teams)<=0 or not _living(id) or not pod_provider_r12.is_valid():return false
	if players[id].get("deployment_r12","")!="deck" or pods_r12.has(id):return false
	var plan:Dictionary=pod_provider_r12.call(id,data)
	if plan.is_empty() or not _valid_vector(plan.get("start")) or not _valid_vector(plan.get("clear")) or not _valid_vector(plan.get("end")) or not _valid_vector(plan.get("ground_target")):return false
	if int(plan.get("team",-1))!=int(players[id].team) or int(plan.get("slot",-1)) not in range(12):return false
	if not plan.get("basis") is Basis or absf(plan.basis.determinant()-1)>.01:return false
	var duration:float=float(plan.get("duration",0))
	if not is_finite(duration) or duration<1.5 or duration>120 or (plan.start.distance_to(plan.clear)+plan.clear.distance_to(plan.get("cruise",plan.end))+(plan.get("cruise",plan.end) as Vector3).distance_to(plan.end))/duration>35.01:return false
	for old in pods_r12.values():
		if old.pod==plan.pod:return false
	pods_r12[id]=plan.duplicate(true);pods_r12[id].time=now();pods_r12[id].state="flying";pods_r12[id].epoch=epoch
	players[id].deployment_r12="flying";players[id].pod_key_r12=plan.pod;players[id].launch_slot_r12=int(plan.slot);_relocate_r12(id,plan.start)
	return true
func _relocate_r12(id:int,at:Vector3):
	_pose_faults_r14.erase(id)
	var p:Dictionary=players[id];p.spawn=at;p.spawn_revision+=1
	if poses.has(id):
		var correction:Vector3=at-poses[id].root.origin
		for key in ["root","head","left","right","gun_pose","shield_plane","pair0","pair1"]:
			if poses[id].get(key) is Transform3D:poses[id][key].origin+=correction
		poses[id].spawn_revision=int(p.spawn_revision);poses[id].epoch=epoch
	_last_pose.erase(id);_history.erase(id)
func _pod_eject_r12(id:int):
	if not pods_r12.has(id) or not players.has(id):return
	var ride:Dictionary=pods_r12[id]
	if ride.state!="flying":return
	# Rear opening is unobstructed; eject behind the visible cabin, never inside its collision.
	var at:Vector3=pod_position_r12(ride)+ride.basis*Vector3(0,0,2.0)
	ride.state="released";ride.ejected_at=now();players[id].deployment_r12="released";_relocate_r12(id,at)
func _step_pods_r12():
	if stage!="match":return
	var dirty=false
	for id in pods_r12:
		var ride:Dictionary=pods_r12[id]
		if ride.state!="flying":continue
		if not players.has(id):ride.state="released";dirty=true;continue
		if players[id].life!="alive":_pod_eject_r12(id);dirty=true
		elif now()>=float(ride.time)+float(ride.duration):
			ride.state="landed";players[id].deployment_r12="released";_relocate_r12(id,ride.get("exit_target",ride.end+Vector3.UP*.3));dirty=true
	if dirty:_publish()
