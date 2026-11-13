extends SceneTree
const Cache=preload("res://scripts/postcut_lods_r19.gd")
var game:Node
func _initialize():call_deferred("run")
func run():
	if not OS.get_cmdline_user_args().has("--no-postcut-lods-r19"):
		push_error("Offline rebuild requires --no-postcut-lods-r19; refusing to overwrite an applied cache");quit(1);return
	var slug:String="alfa-aglae-souza"
	for value in OS.get_cmdline_user_args():
		if value.begins_with("--r18-map="):slug=value.trim_prefix("--r18-map=")
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:"+slug;game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	var records:Dictionary={};var base_triangles:int=0;var coarse_triangles:int=0
	for view in map._geometry.find_children("*","MeshInstance3D",true,false):
		if not view.mesh is ArrayMesh or Cache.reducing(view.mesh) or view.mesh.get_blend_shape_count()>0:continue
		var mesh:ArrayMesh=view.mesh;var importer:=ImporterMesh.new();var valid:bool=mesh.get_surface_count()>0
		var base:int=0
		for s in mesh.get_surface_count():
			var arrays:Array=mesh.surface_get_arrays(s)
			# Preserve the exact base arrays; require existing indices and triangle surfaces.
			if mesh.surface_get_primitive_type(s)!=Mesh.PRIMITIVE_TRIANGLES or arrays[Mesh.ARRAY_INDEX]==null or arrays[Mesh.ARRAY_INDEX].is_empty():valid=false;break
			base+=arrays[Mesh.ARRAY_INDEX].size()/3
			importer.add_surface(Mesh.PRIMITIVE_TRIANGLES,arrays)
		if not valid or base<100:continue
		importer.generate_lods(60.0,0.0,[])
		var rows:Array=[];var coarse:int=0;var any:bool=false
		for s in importer.get_surface_count():
			var lods:Array=[];var minimum:int=mesh.surface_get_array_index_len(s)
			for level in importer.get_surface_lod_count(s):
				var ids:PackedInt32Array=importer.get_surface_lod_indices(s,level)
				if ids.size()<3 or ids.size()>=mesh.surface_get_array_index_len(s):continue
				assert(ids.size()%3==0)
				for id in ids:assert(id>=0 and id<mesh.surface_get_array_len(s))
				var stride:int=RenderingServer.mesh_surface_get_format_index_stride(mesh.surface_get_format(s),mesh.surface_get_array_len(s))
				assert(stride==2 or stride==4)
				var bytes:=PackedByteArray();bytes.resize(ids.size()*stride)
				for i in ids.size():
					if stride==2:bytes.encode_u16(i*2,ids[i])
					else:bytes.encode_u32(i*4,ids[i])
				lods.append(importer.get_surface_lod_size(s,level));lods.append(bytes);minimum=mini(minimum,ids.size());any=true
			rows.append(lods);coarse+=minimum/3
		if not any:continue
		var record:Dictionary={"signature":Cache.signature(mesh),"transform":view.transform,"lods":rows}
		if Cache.safe_bounds_materials(view) and mesh.shadow_mesh==null:
			var found:bool=false;var used:AABB
			for s in mesh.get_surface_count():
				var arrays:Array=mesh.surface_get_arrays(s);var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
				var referenced:PackedInt32Array=arrays[Mesh.ARRAY_INDEX].duplicate()
				var stride:int=RenderingServer.mesh_surface_get_format_index_stride(mesh.surface_get_format(s),vertices.size())
				for level in range(0,rows[s].size(),2):
					var bytes:PackedByteArray=rows[s][level+1]
					for i in bytes.size()/stride:referenced.append(bytes.decode_u16(i*stride) if stride==2 else bytes.decode_u32(i*stride))
				for id in referenced:
					assert(id>=0 and id<vertices.size())
					if not found:used=AABB(vertices[id],Vector3.ZERO);found=true
					else:used=used.expand(vertices[id])
			if found and mesh.get_aabb().get_longest_axis_size()>used.get_longest_axis_size()*1.05:
				record["render_bounds"]=used.grow(.05)
		records[str(map._geometry.get_path_to(view))]=record
		base_triangles+=base;coarse_triangles+=coarse
	if records.is_empty():
		push_error("No reducing post-cut records generated; existing cache preserved");game._quitting=true;quit(1);return
	var directory:String="res://assets/data/postcut_lods_r19/";DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(directory))
	var data:=Resource.new();data.set_meta("schema",1);data.set_meta("engine",Engine.get_version_info().string);data.set_meta("source_fingerprint",str(map._meta.cache_fingerprint));data.set_meta("meshes",records)
	var path:String=directory+slug+".res";assert(ResourceSaver.save(data,path,ResourceSaver.FLAG_COMPRESS)==OK)
	var index_path:String=directory+"index.json";var index:Dictionary={}
	if FileAccess.file_exists(index_path):index=JSON.parse_string(FileAccess.get_file_as_string(index_path))
	index[slug]=FileAccess.get_sha256(path)
	var file=FileAccess.open(index_path,FileAccess.WRITE);file.store_string(JSON.stringify(index,"\t"));file.close()
	print("POSTCUT_BUILD ",slug," meshes=",records.size()," base=",base_triangles," coarse=",coarse_triangles)
	game._quitting=true;game.queue_free();await process_frame;quit()
