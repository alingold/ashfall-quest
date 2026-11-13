extends SceneTree
var game:Node
var failures:Array=[]
var rows:Array=[]
func _initialize():call_deferred("run")
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game)
	for cycle in 2:
		for id in ["archive:alfa-aglae-souza","archive:phoenix-bigbox-vr","cyber_bird"]:
			game.front_menu.selected_map=id;game._start_from_title()
			for i in 8:await physics_frame
			var map:Node=game.active_sandbox_map
			if not is_instance_valid(map) or ("map_ready" in map and not map.map_ready):failures.append(id+" failed to load");continue
			var tracked:Array=[map.get_instance_id()]
			for node in map.find_children("*","Node",true,false):tracked.append(node.get_instance_id())
			var loaded:int=get_node_count();game._return_home0992()
			for i in 8:await process_frame
			var alive:Array=[]
			for instance in tracked:
				if is_instance_id_valid(instance):alive.append(instance)
			if not alive.is_empty():failures.append(id+" retained "+str(alive.size())+" map nodes")
			rows.append({"cycle":cycle,"map":id,"tracked_nodes":tracked.size(),"retained_nodes":alive.size(),"loaded_tree_nodes":loaded,"retired_tree_nodes":get_node_count(),"retired_static_bytes":OS.get_static_memory_usage()})
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/LIFETIME-R14.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"failures":failures,"rows":rows,"scope":"actual Main READY/menu loop repeated twice across Alpha, Phoenix and Cyber Bird; every captured map node checked invalid after unload; static memory recorded, no headset frame-time claim"},"\t"));print("LIFETIME_R14 ",JSON.stringify(rows)," failures=",failures);game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
