extends SceneTree
const Session=preload("res://scripts/net_session1000.gd")
var failures:Array=[]
var facts:Dictionary={}
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);print("FAIL ",label)
func run()->void:
	var args=OS.get_cmdline_user_args();var factory=load(args[1]) if args.size()>1 else Session
	var source=factory.new();root.add_child(source);var replica=factory.new();root.add_child(replica);replica.active=true
	var modern="_session_nonce" in replica
	var packet=source.snapshot();packet.stage="match";packet.epoch=0;packet.state_seq=1;packet.session_id="HOST-B";packet.entities={"kept":{"id":"kept","owner":1,"ammo":3,"kind":"weapon"}}
	if modern:replica._join_challenge="JOIN-B"
	replica._state(packet.duplicate(true))
	check(replica.stage!="match","state arriving before a matching welcome is not applied")
	if modern:replica._welcome("TOKEN-B","HOST-B","JOIN-B")
	else:replica._welcome("TOKEN-B")
	replica._state(packet.duplicate(true));check(replica.entities.has("kept"),"welcomed session full state applies")
	var abandoned=packet.duplicate(true);abandoned.epoch=9;abandoned.state_seq=900;abandoned.session_id="HOST-A";abandoned.entities={"resurrected":{"id":"resurrected"}}
	replica._state(abandoned.duplicate(true));facts.epoch_after_old_host_state=replica.epoch
	check(replica.epoch==0 and replica.entities.has("kept") and not replica.entities.has("resurrected"),"old host snapshot cannot overwrite a fresh host session")
	# Same-epoch ordering is distinct from epoch fencing.
	if not modern:replica.close(false);replica.active=true
	var latest=packet.duplicate(true);latest.state_seq=30;latest.entities.kept.ammo=1;replica._state(latest.duplicate(true))
	var stale=packet.duplicate(true);stale.state_seq=29;stale.entities.kept.ammo=8;replica._state(stale.duplicate(true))
	check(int(replica.entities.kept.ammo)==1,"same-epoch lower-serial full state cannot restore ammunition")
	var gap=latest.duplicate(true);gap.erase("entities");gap.erase("players");gap.erase("builds");gap.erase("marks");gap.state_seq=33;gap.base_seq=32;gap.sets={"entities":{"incomplete":{"id":"incomplete"}},"players":{},"builds":{},"marks":{}};gap.removed={"entities":["kept"],"players":[],"builds":[],"marks":[]}
	replica._delta(gap.duplicate(true))
	check(replica.entities.has("kept") and not replica.entities.has("incomplete"),"missing-base delta does not partially mutate replica")
	latest.state_seq=34;latest.entities.kept.ammo=2;replica._state(latest.duplicate(true));check(int(replica.entities.kept.ammo)==2,"complete state recovers after omitted delta base")
	var events:Array=[];replica.event_received.connect(func(kind:String,_data:Dictionary):events.append(kind));replica.stage="match";replica.epoch=2
	replica._event_rpc("shot",{"net_epoch":2,"session_id":"HOST-A"});replica._flight({"id":42,"net_epoch":2,"session_id":"HOST-A"})
	check(events.is_empty(),"old-session combat and projectile poses cannot enter same-numbered new round")
	replica.players[2]={"name":"present"}
	if modern:replica._remote_pose(2,{"time":123,"root":Transform3D.IDENTITY},2,"HOST-A")
	else:replica._remote_pose(2,{"time":123,"root":Transform3D.IDENTITY},2)
	check(replica.poses.is_empty(),"old-session remote player pose is rejected")
	if modern:replica._welcome("TOKEN-A","HOST-A","JOIN-A")
	else:replica._welcome("TOKEN-A")
	check(replica.reconnect_token=="TOKEN-B","old welcome cannot authorize an abandoned join challenge")
	if modern:replica._deny("old rejection","JOIN-A")
	else:replica._deny("old rejection")
	check(replica.active,"old denial cannot close a new connection attempt")
	replica.epoch=9;replica.close(false);replica.active=true
	if modern:replica._join_challenge="JOIN-NEW";replica._welcome("TOKEN-NEW","HOST-NEW","JOIN-NEW")
	else:replica._welcome("TOKEN-NEW")
	var fresh=packet.duplicate(true);fresh.session_id="HOST-NEW";fresh.epoch=0;fresh.state_seq=1;replica._state(fresh)
	facts.epoch_after_fresh_host=replica.epoch
	check(replica.epoch==0 and replica.entities.has("kept"),"close/rejoin resets prior host epoch so a fresh lobby can load")
	var commands=factory.new();root.add_child(commands);commands.active=true;commands.server=true;commands.stage="match";commands.epoch=2;commands._add_player(1,"Current","");commands.players[1].loaded=true
	if modern:commands._session_nonce="HOST-B"
	commands._accept(1,"grip",{"serial":1,"net_epoch":2,"session_id":"HOST-A"},500);var seen_after_foreign=int(commands._seen.get(1,0))
	commands._accept(1,"grip",{"serial":1,"net_epoch":2,"session_id":"HOST-B"},1)
	facts.foreign_command_ack=seen_after_foreign;facts.current_command_grip=commands._strokes.get(1,0)
	check(seen_after_foreign==0 and int(commands._strokes.get(1,0))==1,"foreign-session command cannot poison current ACK sequence")
	commands.close(false)
	if modern:
		var reasons:Array=[];replica.disconnected.connect(func(reason:String):reasons.append(reason));replica.active=true;replica.server=false;replica._welcome_deadline=Time.get_ticks_msec()-1;replica._process(0)
		check(not replica.active and reasons.size()==1 and "same ASHFALL build" in reasons[0],"missing compatible welcome has bounded explicit handshake timeout")
	source.close(false);replica.close(false)
	var result={"passed":failures.is_empty(),"failures":failures,"facts":facts,"scope":"controlled application-level replay against real session methods; host/session and epoch/serial fencing, not evidence of ENet leaking across connections"};var f=FileAccess.open(args[0].path_join("session_fence1004.json"),FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("SESSION_FENCE1004 "+JSON.stringify(result));quit(0 if failures.is_empty() else 1)
