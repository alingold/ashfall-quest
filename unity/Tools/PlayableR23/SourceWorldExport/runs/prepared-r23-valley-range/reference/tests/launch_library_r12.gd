extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var archive=preload("res://scripts/archive_catalog1003.gd");var net:Node=game.network_bridge.net;var launch:Node=game.network_bridge.launch_r12
	for row in archive.rows():
		net.active=false;launch.clear();game.front_menu.selected_map=row.id
		if game.front_end_active:game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,row.id)
		for i in 5:await physics_frame;await process_frame
		freeze(game);var map:Node=game.active_sandbox_map;check(map.map_ready and map.load_error.is_empty(),row.id+" READY")
		net.active=true;net.server=true;net.stage="match";net.epoch+=1;net.rules.map=row.id;net.rules.mode="revive";net.rules.teams=2;net.zone.clear();net.players={1:{"team":0,"deaths":0,"name":"One"},2:{"team":0,"deaths":0,"name":"Two"},3:{"team":1,"deaths":0,"name":"Three"}};net.poses.clear();net.pods_r12.clear()
		check(launch.ensure_layout(),row.id+" team launch layout available")
		for i in 3:await physics_frame
		var spawns:Array=[]
		for id in net.players:
			var at:Vector3=game.network_bridge._spawn_point(id,net.players,net.rules);spawns.append(at);check(map.safe_spawn_r8(at),row.id+" sky-deck floor/headroom/bounds "+str(id));check(at.y>launch.top+60,row.id+" elevated above map "+str(id));net.poses[id]={"root":Transform3D(Basis.IDENTITY,launch.pod_pose(id).origin+Vector3.UP*.12)}
		check(spawns[0].distance_to(spawns[1])>3 and spawns[0].distance_to(spawns[1])<10,row.id+" teammates have distinct nearby starting sites")
		check(spawns[0].distance_to(spawns[2])>20,row.id+" opposing teams use separate platforms")
		for id in net.players:
			var plan:Dictionary=launch.plan(id,{"pod":net.players[id].pod_key_r12});check(not plan.is_empty(),row.id+" grounded pod destination "+str(id))
		facts.append({"map":row.id,"platforms":launch.stations.size(),"height_above_highest":spawns[0].y-launch.top,"recorded_arena_sites":map.spawn_points.size(),"source_binding":"new reconstruction; no typed team/pod binding supplied"})
		net.rules.mode="respawn";launch.ensure_layout();check(not is_instance_valid(launch.structure),row.id+" deathmatch removes pod structures")
		net.active=false;net.players.clear();var weak:WeakRef=weakref(map);game._return_home0992();for i in 5:await physics_frame;await process_frame
		freeze(game);check(weak.get_ref()==null,row.id+" unload releases scenery and launch structures");print("LAUNCH_LIBRARY_R12 ",row.id," checks=",checks," failures=",failures.size())
	game._quitting=true;game.queue_free();await process_frame
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/LAUNCH-LIBRARY-R12.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"maps":facts,"scope":"all imported maps in actual Main, controlled team roster and live-mode flags with real physics queries; offline, not live transport/headset"},"\t"));quit(0 if failures.is_empty() else 1)
