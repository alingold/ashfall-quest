extends RefCounted
## Offline indices only; never reconstruct source geometry or generate at runtime.
static func signature(mesh:ArrayMesh)->String:
	var context:=HashingContext.new();context.start(HashingContext.HASH_SHA256)
	for encoded in mesh.get("_surfaces"):
		var geometry:Dictionary=encoded.duplicate();geometry.erase("lods");geometry.erase("material");geometry.erase("name")
		context.update(var_to_bytes(geometry))
	return context.finish().hex_encode()

static func reducing(mesh:ArrayMesh)->bool:
	for s in mesh.get_surface_count():
		if not RenderingServer.mesh_get_surface(mesh.get_rid(),s).get("lods",[]).is_empty():return true
	return false

static func render_bounds(mesh:ArrayMesh)->AABB:
	return mesh.custom_aabb if mesh.custom_aabb!=AABB() else mesh.get_aabb()

static func safe_bounds_materials(view:MeshInstance3D)->bool:
	if view.skin!=null or view.material_overlay!=null:return false
	for s in view.mesh.get_surface_count():
		var material:Material=view.get_active_material(s)
		if not material is BaseMaterial3D:return false
		if material.transparency!=BaseMaterial3D.TRANSPARENCY_DISABLED or material.billboard_mode!=BaseMaterial3D.BILLBOARD_DISABLED or material.grow or material.fixed_size or material.next_pass!=null:return false
	return true

static func apply(map:Node)->Dictionary:
	if OS.get_cmdline_user_args().has("--no-postcut-lods-r19"):return {"disabled":true}
	return apply_cached(map)

static func apply_cached(map:Node)->Dictionary:
	var slug:String=preload("res://scripts/archive_catalog1003.gd").slug(map.map_id)
	var path:String="res://assets/data/postcut_lods_r19/"+slug+".res"
	var index_path:String="res://assets/data/postcut_lods_r19/index.json"
	if not FileAccess.file_exists(index_path):return {"reason":"no index"}
	var index=JSON.parse_string(FileAccess.get_file_as_string(index_path))
	var expected:String=index.get(slug,"") if index is Dictionary else ""
	if expected.is_empty() or not FileAccess.file_exists(path) or FileAccess.get_sha256(path)!=expected:return {"reason":"cache missing or hash mismatch"}
	var data:Resource=load(path)
	if data.get_meta("schema",0)!=1 or data.get_meta("engine","")!=Engine.get_version_info().string:return {"reason":"cache schema or engine mismatch"}
	if str(data.get_meta("source_fingerprint",""))!=str(map._meta.cache_fingerprint):return {"reason":"source mismatch"}
	var started:int=Time.get_ticks_usec()
	var changed:int=0;var rejected:int=0;var bounds_changed:int=0
	for key in data.get_meta("meshes",{}):
		var view=map._geometry.get_node_or_null(NodePath(key))
		var record:Dictionary=data.get_meta("meshes")[key]
		if view==null or not view is MeshInstance3D or not view.mesh is ArrayMesh:rejected+=1;continue
		var original:ArrayMesh=view.mesh
		if reducing(original) or original.get_blend_shape_count()>0 or original.get_surface_count()!=record.lods.size() or signature(original)!=record.signature or view.transform!=record.transform:rejected+=1;continue
		var encoded:Array=original.get("_surfaces")
		for s in encoded.size():
			encoded[s]=encoded[s].duplicate()
			encoded[s]["lods"]=record.lods[s]
		var mesh:=ArrayMesh.new();mesh.set("_surfaces",encoded)
		mesh.custom_aabb=render_bounds(original);mesh.shadow_mesh=original.shadow_mesh
		# Experimental, default off until native comparison. Only exact static map records.
		if OS.get_cmdline_user_args().has("--postcut-tight-bounds-r19") and record.has("render_bounds") and safe_bounds_materials(view) and original.shadow_mesh==null:
			mesh.custom_aabb=record.render_bounds;bounds_changed+=1
		view.mesh=mesh;changed+=1
	return {"apply_cpu_ms":float(Time.get_ticks_usec()-started)/1000.0,"meshes":changed,"rejected":rejected,"tight_bounds_meshes":bounds_changed,"runtime_generation":false,"hard_cutoff":false}
