extends RefCounted
## Native screen-error mesh LODs retain the whole map: no visibility cutoff/fade.
static var _index:Dictionary={}
static func apply(map:Node)->Dictionary:
	if _index.is_empty():
		var value=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_lod_index_r16.json"))
		if value is Dictionary:_index=value
	var record:Dictionary=_index.get("maps",{}).get(preload("res://scripts/archive_catalog1003.gd").slug(map.map_id),{})
	if record.is_empty() or record.source_fingerprint!=str(map._meta.cache_fingerprint):return {"meshes":0,"reason":"no matching LOD record"}
	var path:String=record.path
	if not FileAccess.file_exists(path) or FileAccess.get_sha256(path)!=str(record.sha256):return {"meshes":0,"reason":"LOD asset missing or mismatched"}
	var data:Resource=load(path);var changed=0;var skipped=0
	for key in data.get_meta("meshes",{}):
		var view:MeshInstance3D=map._geometry.get_node_or_null(NodePath(key))
		if view==null or not view.mesh is ArrayMesh:skipped+=1;continue
		var rows:Array=data.get_meta("meshes")[key];var valid:bool=rows.size()==view.mesh.get_surface_count()
		for surface in rows.size():
			if not valid:break
			var arrays:Array=view.mesh.surface_get_arrays(surface)
			var count:int=arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX]!=null else 0
			valid=arrays[Mesh.ARRAY_VERTEX].size()==int(rows[surface].vertices) and count==int(rows[surface].indices)
		if not valid:skipped+=1;continue # Source marker/art cuts must never be undone.
		var mesh:=ArrayMesh.new()
		for surface in rows.size():
			var arrays:Array=view.mesh.surface_get_arrays(surface)
			if int(rows[surface].indices)==0:arrays[Mesh.ARRAY_INDEX]=PackedInt32Array(range(arrays[Mesh.ARRAY_VERTEX].size()))
			mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays,[],rows[surface].lods)
			mesh.surface_set_material(surface,view.mesh.surface_get_material(surface))
		mesh.custom_aabb=view.mesh.get_aabb();view.mesh=mesh;changed+=1
	return {"meshes":changed,"cut_meshes_skipped":skipped,"hard_cutoff":false,"base_triangles":record.base_triangles,"coarsest_triangles":record.coarsest_triangles}
