extends SceneTree
var checks:=0
var failures:Array=[]
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func run()->void:
	var script_path:String=OS.get_cmdline_user_args()[1] if OS.get_cmdline_user_args().size()>1 else "res://scripts/net_session1000.gd"
	var net:Node=load(script_path).new();root.add_child(net)
	var events:Array=[];net.event_received.connect(func(kind:String,_data:Dictionary):events.append(kind))
	net.active=true;net.server=false;net.epoch=7;net.stage="lobby"
	net._flight({"id":77,"net_epoch":7});check(events.is_empty(),"late projectile pose must not recreate a projectile in lobby")
	events.clear();net._event_rpc("projectile",{"id":77,"net_epoch":7});check(events.is_empty(),"late reliable projectile event ignored after returning home")
	events.clear();net.active=false;net.stage="match";net._flight({"id":77,"net_epoch":7});check(events.is_empty(),"disconnected session rejects old projectile poses")
	events.clear();net.active=true;net.stage="loading";net._event_rpc("shot",{"net_epoch":7});check(events.is_empty(),"combat event waits for match snapshot during loading")
	check(net._pending_events.size()==1,"valid loading event is queued rather than lost")
	events.clear()
	var snapshot:Dictionary=net.snapshot();snapshot.stage="match";net._state(snapshot)
	check(events==["shot"],"matching match snapshot releases queued event")
	var delivered_count:int=events.size();net._state(snapshot);check(events.size()==delivered_count,"repeated snapshot does not replay event twice")
	events.clear();net._event_rpc("shot",{"net_epoch":6});check(events.is_empty(),"previous epoch remains rejected")
	net.close(false);check(net.host("Fixture",24905)==OK,"bounded isolated host starts")
	net.stage="match";net.epoch=7;net.builds.wall={"id":"wall"};net.projectile_poses[77]={"id":77};net.request("home")
	check(net.projectile_poses.is_empty(),"home clears live projectile snapshot")
	check(net.builds.is_empty(),"home clears placed-build snapshot")
	net.stage="match";net.players[1].loaded=false;net.request("loaded",{"epoch":7})
	net.players[1].health=37;var spawn_revision:int=net.players[1].spawn_revision;net.request("loaded",{"epoch":7})
	check(net.players[1].health==37 and net.players[1].spawn_revision==spawn_revision,"duplicate loaded acknowledgment is idempotent")
	var token:String=net.players[1].token;net._peer_left(1);net._begin_loading();net._add_player(2,"Restored",token)
	check(net.players[2].inventory.size()==3 and net.players[2].inventory.all(func(eid:String)->bool:return net.entities.has(eid) and int(net.entities[eid].owner)==2),"cross-round reservation restores existing current-round inventory")
	net.close(false);check(net.join("127.0.0.1","Pending",24999)==OK,"join can enter connecting state")
	var sequence:int=net._seq;net.send_pose({});check(net.request("ready")==0 and net._seq==sequence and net.pending.is_empty(),"pending connection cannot send poses or enqueue commands")
	var report:Dictionary={"checks":checks,"failures":failures,"passed":failures.is_empty(),"input":"deterministic delayed authenticated-packet handler calls; no Internet emulation"}
	var path:String=OS.get_cmdline_user_args()[0];var file:=FileAccess.open(path,FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close();print("REPRO1001 ",JSON.stringify(report));net.close(false);net.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
