extends SceneTree
func _initialize():call_deferred("run")
func run():
	var output:String=OS.get_cmdline_user_args()[0];var destination:String=OS.get_cmdline_user_args()[-1];DirAccess.make_dir_recursive_absolute(destination)
	var metadata:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/archive_semantics_r9.json")).maps
	var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog();var results:Array=[]
	for row in catalog.rows():
		if OS.get_cmdline_user_args().has("--official-only-r9") and not metadata.get(str(row.id),{}).get("official",false):continue
		var only:String=""
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--only-r9="):only=arg.trim_prefix("--only-r9=")
		if not only.is_empty() and row.slug!=only:continue
		if OS.get_cmdline_user_args().has("--quick-r9") and str(row.slug) not in ["poppy-gods-poplightning","phoenix-bigbox-vr","classic-bigbox-vr","vortex-sd-stumpdawg","melipop-gods-poplightning","magnum-chaosvr","magnum-3-chaosvr"]:continue
		var path:String=destination.path_join(str(row.slug)+".res")
		if OS.get_cmdline_user_args().has("--doors-only-r9") and FileAccess.file_exists(path):
			var previous:Resource=load(path);var has_door=false
			for panel in previous.details.panels:
				if not panel.glass:has_door=true;break
			if not has_door:continue
		if FileAccess.file_exists(path) and not OS.get_cmdline_user_args().has("--force-r9"):
			var old:Resource=load(path)
			if old.adapter_version=="r9-v4":print("ADAPTER_R9 cached ",row.slug);continue
		var packed:PackedScene=load(catalog.scene(str(row.id)));var geometry:Node=packed.instantiate();var started:int=Time.get_ticks_msec()
		var details:Dictionary=preload("res://scripts/archive_semantics_r9.gd").prepare(geometry,str(row.id),metadata.get(str(row.id),{}))
		for mesh in geometry.find_children("*","MeshInstance3D",true,false):
			if mesh.has_meta("review_mesh_r8"):mesh.remove_meta("review_mesh_r8")
		var clean:=PackedScene.new();clean.pack(geometry);var resource:=preload("res://scripts/archive_prepared_r8.gd").new();resource.geometry=clean;resource.details=details;resource.source_fingerprint=str(catalog.manifest(str(row.id)).cache_fingerprint);resource.adapter_version="r9-v4"
		var error:int=ResourceSaver.save(resource,path,ResourceSaver.FLAG_COMPRESS)
		var doors=0
		for panel in details.panels:
			if not panel.glass:doors+=1
		results.append({"id":row.id,"saved":error==OK,"sites":details.weapon_sites.size(),"panels":details.panels.size(),"doors":doors,"door_cut":details.door_cut_r9,"pads":details.jump_pads.size(),"lobby_floors":details.spawn_floors_r9.size(),"frames":details.frame_triangles_r9,"score_faces":details.score_faces_r9.size(),"weapon_boards":details.weapon_boards_r9.size(),"signs":details.signs_r9.size(),"seconds":(Time.get_ticks_msec()-started)*.001})
		print("ADAPTER_R9 ",JSON.stringify(results[-1]));geometry.free();packed=null;resource=null;clean=null;details.clear();await process_frame
	var file=FileAccess.open(output+"/ADAPTER-BAKE-R9.json",FileAccess.WRITE);file.store_string(JSON.stringify(results,"\t"));quit()
