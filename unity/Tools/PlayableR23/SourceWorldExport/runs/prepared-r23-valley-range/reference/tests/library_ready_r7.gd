extends SceneTree
const Archive=preload("res://scripts/archive_catalog1003.gd")
var failures:Array=[]
var facts:Array=[]
var game:Node
func _initialize():call_deferred("run")
func settle():
	for i in 5:await physics_frame;await process_frame
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);await settle();game.player.set_physics_process(false)
	var maps:Array=Archive.rows();var start=0;var end=maps.size()
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--map-range="):
			var bounds=arg.trim_prefix("--map-range=").split(":");start=int(bounds[0]);end=mini(int(bounds[1]),maps.size())
	for index in range(start,end):
		var id:String=maps[index].id;var front:Node=game.front_menu;var selected:int=front._map_ids1003.find(id)
		front.page="maps";front._map_page1003=selected/4;front.refresh_page();front.activate(front._map_slots1003[selected%4]);front.activate(0);await settle()
		var map:Node=game.active_sandbox_map
		var ok:bool=is_instance_valid(map) and map.get("map_id")==id and map.get("map_ready")==true and str(map.get("load_error")).is_empty() and game.player.global_position.is_finite() and not game.front_end_active
		if not ok:failures.append(id)
		var ref:WeakRef=weakref(map) if is_instance_valid(map) else null
		var count:int=game.weapon_inventory.pickups.size();var ammo:int=game.ammo_pickups.boxes.size()
		game._return_home0992();await settle()
		var freed:bool=ref==null or ref.get_ref()==null
		if not freed:failures.append(id+" retained")
		facts.append({"id":id,"ready":ok,"real_weapon_pickups":count,"ammo_boxes":ammo,"map_freed":freed,"menu_nodes":get_node_count(),"static_bytes":OS.get_static_memory_usage()})
		var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/READY-R7-%03d-%03d.json"%[start,end],FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":facts.size()*2,"failures":failures,"maps":facts,"xr":"off","headset_tested":false},"\t"));file.close()
		print("MAP_READY_R7 ",index+1,"/",maps.size()," ",id," ready=",ok," freed=",freed)
	game.queue_free();await process_frame;print("LIBRARY_READY_R7 checks=",facts.size()*2," failures=",failures.size());quit(0 if failures.is_empty() else 1)
