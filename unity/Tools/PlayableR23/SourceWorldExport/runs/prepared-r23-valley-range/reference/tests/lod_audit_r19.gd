extends SceneTree
## CPU geometry inventory, not a frustum or GPU-selected LOD measurement.
## Quantify triangles in loaded instances whose AABB contains the start eye.
## Large instance extents can limit LOD reach; selection still needs rendering QA.
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
		var box:AABB=view.global_transform*preload("res://scripts/postcut_lods_r19.gd").render_bounds(view.mesh)
		var closest:Vector3=eye.clamp(box.position,box.end);var d:float=eye.distance_to(closest)
		var tris=0
		for s in view.mesh.get_surface_count():
			var count:int=view.mesh.surface_get_array_index_len(s)
			tris+=(count if count>0 else view.mesh.surface_get_array_len(s))/3
		all_tris+=tris
		var band:String="inside" if d<.01 else ("<10m" if d<10 else ("<40m" if d<40 else ("<120m" if d<120 else ">=120m")))
		if not bands.has(band):bands[band]={"instances":0,"triangles":0,"max_extent_m":0.0}
		bands[band].instances+=1;bands[band].triangles+=tris;bands[band].max_extent_m=maxf(bands[band].max_extent_m,box.get_longest_axis_size())
		instances.append({"name":str(view.name),"d":snappedf(d,.1),"extent":snappedf(box.get_longest_axis_size(),.1),"triangles":tris})
	instances.sort_custom(func(a,b):return a.triangles>b.triangles)
	var extents:Array=instances.map(func(r):return r.extent);extents.sort()
	var report={"lod_audit":audit_lods(map),"root_lod_threshold":root.mesh_lod_threshold,"scope_viewports":scope_settings(game),"map":slug,"eye":str(eye-map.global_position),"map_triangles":all_tris,"bands":bands,"median_extent_m":extents[extents.size()/2],"largest":instances.slice(0,25)}
	var f=FileAccess.open(out+"/R19-LOD-"+slug+("-baseline" if OS.get_cmdline_user_args().has("--no-postcut-lods-r19") else "-postcut")+("-tightbounds" if OS.get_cmdline_user_args().has("--postcut-tight-bounds-r19") else "")+".json",FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"));f.close()
	print("R18_LOD_REACH ",JSON.stringify(bands)," median_extent=",report.median_extent_m)
	game._quitting=true;game.queue_free();await process_frame;quit(0)

func scope_settings(node:Node)->Array:
	var result:Array=[]
	for view in node.find_children("*","SubViewport",true,false):
		result.append({"path":str(node.get_path_to(view)),"lod_threshold":view.mesh_lod_threshold,"size":str(view.size),"update_mode":view.render_target_update_mode})
	return result

func audit_lods(map:Node)->Dictionary:
	var index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_lod_index_r16.json"))
	var slug=preload("res://scripts/archive_catalog1003.gd").slug(map.map_id)
	var record:Dictionary=index.maps.get(slug,{})
	var data:Resource=load(record.path) if not record.is_empty() else null
	var records:Dictionary=data.get_meta("meshes",{}) if data!=null else {}
	var bands:Dictionary={};var details:Array=[];var retained_total:int=0;var base_total:int=0;var retained_coarse:int=0
	for view in map._geometry.find_children("*","MeshInstance3D",true,false):
		if view.mesh==null:continue
		var actual_lods:Array=[];var actual_triangles:int=0;var actual_coarse:int=0;var actual_covered:int=0
		for surface in view.mesh.get_surface_count():
			var raw:Dictionary=RenderingServer.mesh_get_surface(view.mesh.get_rid(),surface)
			var base:int=view.mesh.surface_get_array_index_len(surface)
			if base==0:base=view.mesh.surface_get_array_len(surface)
			var stride:int=RenderingServer.mesh_surface_get_format_index_stride(raw.format,raw.vertex_count)
			var minimum:int=base;var levels:Array=[]
			for lod in raw.get("lods",[]):
				levels.append({"edge_length":lod.get("edge_length",0),"indices":lod.get("index_data",PackedByteArray()).size()/stride})
				minimum=mini(minimum,lod.get("index_data",PackedByteArray()).size()/stride)
			actual_lods.append(levels);actual_triangles+=base/3;actual_coarse+=minimum/3
			if minimum<base:actual_covered+=base/3
		retained_total+=actual_covered;base_total+=actual_triangles;retained_coarse+=actual_coarse
		var key:String=str(map._geometry.get_path_to(view));var rows:Array=records.get(key,[])
		var reason:String="matching_record";var triangles:int=0;var eligible:int=0;var coarse:int=0
		if rows.is_empty():reason="no_precomputed_record"
		elif rows.size()!=view.mesh.get_surface_count():reason="surface_count_changed"
		for s in view.mesh.get_surface_count():
			var count:int=view.mesh.surface_get_array_index_len(s)
			if count==0:count=view.mesh.surface_get_array_len(s)
			triangles+=count/3
			if reason=="matching_record" and (int(rows[s].vertices)!=view.mesh.surface_get_array_len(s) or not expected_indices(view.mesh,s,int(rows[s].indices))):reason="vertex_or_index_count_changed"
		if reason=="matching_record":
			for row in rows:
				var base:int=int(row.indices) if int(row.indices)>0 else int(row.vertices)
				var minimum:int=base
				for ids in row.lods.values():minimum=mini(minimum,ids.size())
				if minimum<base:eligible+=base/3
				coarse+=minimum/3
		if not bands.has(reason):bands[reason]={"instances":0,"triangles":0,"eligible_triangles":0,"coarsest_triangles":0}
		bands[reason].instances+=1;bands[reason].triangles+=triangles;bands[reason].eligible_triangles+=eligible;bands[reason].coarsest_triangles+=coarse
		var box:AABB=view.global_transform*preload("res://scripts/postcut_lods_r19.gd").render_bounds(view.mesh)
		var eye:Vector3=game.player.global_position+Vector3.UP*1.7
		details.append({"aabb_nearest_m":eye.distance_to(eye.clamp(box.position,box.end)),"extent_m":box.get_longest_axis_size(),"visible_in_tree":view.is_visible_in_tree() and view.layers!=0,"actual_lods":actual_lods,"actual_covered":actual_covered,"actual_coarse":actual_coarse,"path":key,"reason":reason,"triangles":triangles,"eligible_triangles":eligible,"coarsest_triangles":coarse})
	details.sort_custom(func(a,b):return a.triangles>b.triangles)
	return {"retained_triangles":retained_total,"base_triangles":base_total,"coarsest_triangles":retained_coarse,"interpretation":"Retained LOD dictionaries measured directly through RenderingServer with native index stride. Count compatibility separately allows exact sequential indices inserted by R16. Not GPU-selected or visible triangles.","runtime_statistics":map.statistics.get("mesh_lods_r16",{}),"bands":bands,"meshes":details}
func expected_indices(mesh:ArrayMesh,s:int,original_count:int)->bool:
	var count:int=mesh.surface_get_array_index_len(s)
	if count==original_count:return true
	if original_count!=0 or count!=mesh.surface_get_array_len(s):return false
	var ids:PackedInt32Array=mesh.surface_get_arrays(s)[Mesh.ARRAY_INDEX]
	for i in ids.size():
		if ids[i]!=i:return false
	return true
