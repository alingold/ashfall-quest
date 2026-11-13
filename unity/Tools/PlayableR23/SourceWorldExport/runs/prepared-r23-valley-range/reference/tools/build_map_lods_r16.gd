extends SceneTree
## Editor-only preprocessing. Exported games only read the resulting LOD indices.
const Catalog=preload("res://scripts/archive_catalog1003.gd")
func _initialize():call_deferred("run")
func run():
	var output_root=ProjectSettings.globalize_path("res://assets/data/lods_r16/")
	var output_index=ProjectSettings.globalize_path("res://assets/data/map_lod_index_r16.json")
	DirAccess.make_dir_recursive_absolute(output_root)
	Catalog.catalog()
	var destination="res://assets/data/lods_r16/"
	var index:Dictionary={"schema":1,"scope":"Render index LODs only. Original base vertices/indices, materials, collision and map bounds are unchanged.","maps":{}}
	for info in Catalog.rows():
		var slug:String=info.slug
		var original:Resource=load("res://local_archive_r10/"+slug+".res")
		if original==null:push_error("Missing prepared map "+slug);quit(1);return
		var scene:Node=original.geometry.instantiate();var meshes:Dictionary={};var surfaces=0;var original_triangles=0;var last_triangles=0
		for view in scene.find_children("*","MeshInstance3D",true,false):
			if not view.mesh is ArrayMesh:continue
			var importer:=ImporterMesh.new()
			for surface in view.mesh.get_surface_count():
				var arrays:Array=view.mesh.surface_get_arrays(surface)
				if arrays[Mesh.ARRAY_INDEX]==null or arrays[Mesh.ARRAY_INDEX].is_empty():arrays[Mesh.ARRAY_INDEX]=PackedInt32Array(range(arrays[Mesh.ARRAY_VERTEX].size()))
				importer.add_surface(Mesh.PRIMITIVE_TRIANGLES,arrays)
			importer.generate_lods(60.0,0.0,[])
			var rows:Array=[];var any=false
			for surface in importer.get_surface_count():
				var arrays:Array=view.mesh.surface_get_arrays(surface);var count:int=arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX]!=null else 0
				var base_count:int=count if count>0 else arrays[Mesh.ARRAY_VERTEX].size()
				var lods:Dictionary={}
				for level in importer.get_surface_lod_count(surface):
					var ids:PackedInt32Array=importer.get_surface_lod_indices(surface,level)
					if ids.size()>=3 and ids.size()<base_count:lods[importer.get_surface_lod_size(surface,level)]=ids
				rows.append({"vertices":arrays[Mesh.ARRAY_VERTEX].size(),"indices":count,"lods":lods})
				original_triangles+=base_count/3
				last_triangles+=(lods.values()[-1].size() if not lods.is_empty() else base_count)/3
				if not lods.is_empty():any=true;surfaces+=1
			if any:meshes[str(scene.get_path_to(view))]=rows
		var data:=Resource.new();data.set_meta("source_fingerprint",original.source_fingerprint);data.set_meta("meshes",meshes)
		var path=destination+slug+".res"
		var output_path=output_root.path_join(slug+".res")
		if ResourceSaver.save(data,output_path,ResourceSaver.FLAG_COMPRESS)!=OK:scene.free();push_error("LOD save failed "+slug);quit(1);return
		index.maps[slug]={"path":path,"sha256":FileAccess.get_sha256(output_path),"source_fingerprint":original.source_fingerprint,"meshes":meshes.size(),"surfaces":surfaces,"base_triangles":original_triangles,"coarsest_triangles":last_triangles}
		print("LOD_R16 ",slug," meshes=",meshes.size()," triangles=",original_triangles," -> ",last_triangles)
		scene.free()
	var file=FileAccess.open(output_index,FileAccess.WRITE);file.store_string(JSON.stringify(index,"\t"));file.close()
	print("LOD_R16_COMPLETE maps=",index.maps.size());quit()
