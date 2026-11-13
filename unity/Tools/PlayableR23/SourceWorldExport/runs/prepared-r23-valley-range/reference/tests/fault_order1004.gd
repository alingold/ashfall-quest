extends SceneTree
const Session=preload("res://scripts/net_session1000.gd")
var failures:Array=[]
var facts:Dictionary={}
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);print("FAIL ",label)
func run()->void:
	var factory=load(OS.get_cmdline_user_args()[1]) if OS.get_cmdline_user_args().size()>1 else Session
	var source=factory.new();root.add_child(source)
	source.stage="match";source.epoch=4;source.revision=2
	var old=source.snapshot();old["state_seq"]=10;old.entities={"lost":{"id":"lost","owner":2,"ammo":0,"kind":"weapon"}}
	var newer=old.duplicate(true);newer.epoch=5;newer["state_seq"]=20;newer.entities={};newer.stage="lobby"
	var replica=factory.new();root.add_child(replica);replica.active=true;replica._state(newer.duplicate(true));replica._state(old.duplicate(true))
	facts.snapshot_epoch_after_replay=replica.epoch;facts.snapshot_entities_after_replay=replica.entities.keys()
	check(replica.epoch==5 and replica.stage=="lobby" and replica.entities.is_empty(),"delayed full snapshot cannot roll back round or resurrect inventory")
	replica.close(false);replica.active=true;replica._state(newer.duplicate(true))
	var patch=old.duplicate(true);patch.erase("entities");patch.erase("players");patch.erase("builds");patch.erase("marks");patch["base_seq"]=9;patch.sets={"entities":old.entities,"players":{},"builds":{},"marks":{}};patch.removed={"entities":[],"players":[],"builds":[],"marks":[]}
	replica._delta(patch)
	facts.delta_epoch_after_replay=replica.epoch;facts.delta_entities_after_replay=replica.entities.keys()
	check(replica.epoch==5 and replica.entities.is_empty(),"stale delta is rejected before mutating live dictionaries")
	replica.close(false);replica.active=true;replica.epoch=5;replica.stage="match";replica.players={}
	replica._remote_pose(222,{"time":123,"root":Transform3D.IDENTITY},5)
	facts.orphan_poses=replica.poses.size();check(replica.poses.is_empty(),"late pose for a departed player cannot create an orphan pose")
	# Real session implementation, synthetic departed identities; no sockets or OS time changes.
	source.active=true;source.server=true;source.stage="lobby"
	for i in 40:
		var id=2000+i;source._add_player(id,"Departed","");source._seen[id]=8;source._budget[id]={};source._budget[-id]={};source._last_pose[id]={};source._strokes[id]=4;source._charge[id]=.8;source._ready_until[id]=10
		source._peer_left(id)
	for reservation in source._reconnect.values():reservation.until=source.now()-1
	source._process(.11)
	facts.expired_lobby_reservations=source._reconnect.size();facts.departed_seen=source._seen.size();facts.departed_budgets=source._budget.size();facts.departed_charge=source._charge.size();facts.expired_lobby_entities=source.entities.size()
	check(source._reconnect.is_empty(),"reconnect reservations expire during an idle lobby")
	check(source.entities.is_empty(),"anonymous departed lobby loadouts do not accumulate unused ground entities")
	check(source._seen.is_empty() and source._budget.is_empty() and source._last_pose.is_empty() and source._strokes.is_empty() and source._charge.is_empty() and source._ready_until.is_empty(),"departed peer bookkeeping remains bounded after join/leave churn")
	source.close(false);source.active=true;source.server=true;source.stage="match";source.epoch=5;source._add_player(1,"Present","");source.players[1].loaded=true
	var old_pose={"net_epoch":4,"seq":999,"root":Transform3D.IDENTITY,"head":Transform3D(Basis.IDENTITY,Vector3.UP*1.7),"left":Transform3D(Basis.IDENTITY,Vector3(-.3,1.2,0)),"right":Transform3D(Basis.IDENTITY,Vector3(.3,1.2,0))}
	source._accept_pose(1,old_pose);source._accept(1,"grip",{"serial":1,"net_epoch":4},1)
	facts.old_round_accepted_poses=source.poses.size();facts.old_round_grip_serial=source._strokes.get(1,0)
	check(source.poses.is_empty() and not source._strokes.has(1),"old-round client inputs cannot become new-round movement or reload strokes")
	source.close(false);source.active=true;source.server=true;source.stage="loading";source.epoch=6;source.world_ready=true;source._add_player(1,"Loaded","");source._add_player(2,"Missing","");source.players[1].loaded=true
	source._peer_left(2);facts.stage_after_missing_loader_leaves=source.stage
	check(source.stage=="match","last missing loader departing releases remaining loaded participants")
	source.close(false);source.active=true;source.server=true;source.stage="match";source.epoch=8;source._add_player(1,"Loaded","");source._add_player(2,"Missing","");source.players[2].health=37;source.players[2].shield=12
	source._begin_loading();source.world_ready=true;source.players[1].loaded=true;var token=source.players[2].token;source._peer_left(2);source._add_player(22,"Missing",token);source._accept(22,"loaded",{"epoch":source.epoch},1)
	facts.loading_rejoin_health=source.players[22].health;facts.loading_rejoin_shield=source.players[22].shield
	check(source.players[22].health==100 and source.players[22].shield==100,"unspawned loading reconnect receives new-round health instead of stale prior-round injury")
	source.close(false);replica.close(false)
	var result={"passed":failures.is_empty(),"failures":failures,"facts":facts,"scope":"actual session methods with controlled delayed/reordered authenticated payloads and synthetic identity churn; no Internet/network emulation"}
	var f=FileAccess.open(OS.get_cmdline_user_args()[0].path_join("fault_order1004.json"),FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("FAULT1004 "+JSON.stringify(result));quit(0 if failures.is_empty() else 1)
