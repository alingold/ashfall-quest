extends SceneTree
const Cache=preload("res://scripts/postcut_lods_r19.gd")
var game:Node
func _initialize():call_deferred("run")
func run():
	assert(OS.get_cmdline_user_args().has("--no-postcut-lods-r19"))
	var slug:String="alfa-aglae-souza"
	for value in OS.get_cmdline_user_args():
		if value.begins_with("--r18-map="):slug=value.trim_prefix("--r18-map=")
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:"+slug;game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	var before:Dictionary={};var collisions:Array=[]
	for view in map._geometry.find_children("*","MeshInstance3D",true,false):
		if view.mesh is ArrayMesh:before[str(map._geometry.get_path_to(view))]={"mesh":view.mesh,"signature":Cache.signature(view.mesh),"overrides":view.material_override,"transform":view.transform}
	for shape in map.find_children("*","CollisionShape3D",true,false):collisions.append([shape,shape.shape,shape.transform,shape.disabled])
	var result:Dictionary=Cache.apply_cached(map);assert(result.get("meshes",0)>0)
	var changed:int=0;var added_surfaces:int=0
	for key in before:
		var view:MeshInstance3D=map._geometry.get_node(NodePath(key));var old:Dictionary=before[key]
		assert(Cache.signature(view.mesh)==old.signature);assert(view.material_override==old.overrides);assert(view.transform==old.transform)
		if view.mesh==old.mesh:continue
		changed+=1
		if OS.get_cmdline_user_args().has("--postcut-tight-bounds-r19"):
			var bounds:AABB=Cache.render_bounds(view.mesh)
			for s in view.mesh.get_surface_count():
				var arrays:Array=view.mesh.surface_get_arrays(s);var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
				for id in arrays[Mesh.ARRAY_INDEX]:assert(bounds.has_point(vertices[id]))
				var raw:Dictionary=RenderingServer.mesh_get_surface(view.mesh.get_rid(),s)
				var stride:int=RenderingServer.mesh_surface_get_format_index_stride(raw.format,raw.vertex_count)
				for lod in raw.get("lods",[]):
					var bytes:PackedByteArray=lod.index_data
					for i in bytes.size()/stride:assert(bounds.has_point(vertices[bytes.decode_u16(i*stride) if stride==2 else bytes.decode_u32(i*stride)]))
		var a:Array=old.mesh.get("_surfaces");var b:Array=view.mesh.get("_surfaces")
		for s in a.size():
			var old_surface:Dictionary=a[s].duplicate();var new_surface:Dictionary=b[s].duplicate();old_surface.erase("lods");new_surface.erase("lods")
			assert(old_surface==new_surface)
			if not RenderingServer.mesh_get_surface(view.mesh.get_rid(),s).get("lods",[]).is_empty():added_surfaces+=1
	for row in collisions:assert(is_instance_valid(row[0]) and row[0].shape==row[1] and row[0].transform==row[2] and row[0].disabled==row[3])
	assert(added_surfaces>0)
	# Restore source meshes, then change one exact base vertex while keeping all counts.
	var victim:MeshInstance3D
	for key in before:
		var view:MeshInstance3D=map._geometry.get_node(NodePath(key))
		if view.mesh!=before[key].mesh and victim==null:victim=view
		view.mesh=before[key].mesh
	var mutated:=ArrayMesh.new();var original:ArrayMesh=victim.mesh
	for s in original.get_surface_count():
		var arrays:Array=original.surface_get_arrays(s)
		if s==0:arrays[Mesh.ARRAY_VERTEX][0]+=Vector3(.001,0,0)
		mutated.add_surface_from_arrays(original.surface_get_primitive_type(s),arrays)
	victim.mesh=mutated
	var rejected:Dictionary=Cache.apply_cached(map);assert(victim.mesh==mutated);assert(rejected.rejected>=1);assert(rejected.meshes==changed-1)
	print("POSTCUT_TEST ",slug," changed=",changed," tight_bounds=",result.get("tight_bounds_meshes",0)," added_lod_surfaces=",added_surfaces," collision_shapes=",collisions.size()," exact_raw_base_buffers=true same_count_mutation_rejected=true")
	game._quitting=true;game.queue_free();await process_frame;quit()
