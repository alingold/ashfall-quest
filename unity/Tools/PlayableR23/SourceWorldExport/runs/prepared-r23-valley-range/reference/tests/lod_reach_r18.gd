extends SceneTree
## Godot picks a mesh LOD from the closest point of the instance AABB (docs: Mesh
## level of detail). Quantify how many in-frustum triangles of the loaded map sit in
## instances whose AABB the start-view camera is inside/near, i.e. that can never
## reduce, and how large those instances are. Headless-safe geometry analysis only.
var game:Node
func _initialize():call_deferred("run")
func run():
	var out:String=OS.get_cmdline_user_args()[0];var slug:="alfa-aglae-souza"
	for value in OS.get_cmdline_user_args():if value.begins_with("--r18-map="):slug=value.trim_prefix("--r18-map=")
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:"+slug;game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	var eye:Vector3=game.player.global_position+Vector3.UP*1.7
	var bands:Dictionary={};var all_tris=0;var instances:Array=[]
	for view in map._geometry.find_children("*","MeshInstance3D",true,false):
		if view.mesh==null or view.layers==0 or not view.is_visible_in_tree():continue
		var box:AABB=view.global_transform*view.mesh.get_aabb()
		var closest:Vector3=eye.clamp(box.position,box.end);var d:float=eye.distance_to(closest)
		var tris=0
		for s in view.mesh.get_surface_count():tris+=maxi(view.mesh.surface_get_array_index_len(s),view.mesh.surface_get_array_len(s))/3
		all_tris+=tris
		var band:String="inside" if d<.01 else ("<10m" if d<10 else ("<40m" if d<40 else ("<120m" if d<120 else ">=120m")))
		if not bands.has(band):bands[band]={"instances":0,"triangles":0,"max_extent_m":0.0}
		bands[band].instances+=1;bands[band].triangles+=tris;bands[band].max_extent_m=maxf(bands[band].max_extent_m,box.get_longest_axis_size())
		instances.append({"name":str(view.name),"d":snappedf(d,.1),"extent":snappedf(box.get_longest_axis_size(),.1),"triangles":tris})
	instances.sort_custom(func(a,b):return a.triangles>b.triangles)
	var extents:Array=instances.map(func(r):return r.extent);extents.sort()
	var report={"map":slug,"eye":str(eye-map.global_position),"map_triangles":all_tris,"bands":bands,"median_extent_m":extents[extents.size()/2],"largest":instances.slice(0,25)}
	var f=FileAccess.open(out+"/R18-LOD-REACH-"+slug+".json",FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"));f.close()
	print("R18_LOD_REACH ",JSON.stringify(bands)," median_extent=",report.median_extent_m)
	game._quitting=true;game.queue_free();await process_frame;quit(0)
