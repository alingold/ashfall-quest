extends SceneTree
func _initialize():call_deferred("run")
func run():
	var output:String=OS.get_cmdline_user_args()[0];var destination:String=OS.get_cmdline_user_args()[-1];DirAccess.make_dir_recursive_absolute(destination)
	var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog();var results:Array=[]
	for row in catalog.rows():
		if OS.get_cmdline_user_args().has("--r8-classic-only") and str(row.slug)!="classic-bigbox-vr":continue
		var path:String=destination.path_join(str(row.slug)+".res")
		if FileAccess.file_exists(path):print("ADAPTER_R8 cached ",row.slug);continue
		var packed:PackedScene=load(catalog.scene(str(row.id)));var geometry:Node=packed.instantiate();var started:int=Time.get_ticks_msec()
		var details:Dictionary=preload("res://scripts/archive_semantics_r8.gd").prepare(geometry,str(row.id)=="archive:classic-bigbox-vr")
		# Authoring review meshes are not shipped into normal play: removing them also saves memory.
		for mesh in geometry.find_children("*","MeshInstance3D",true,false):
			if mesh.has_meta("review_mesh_r8"):mesh.remove_meta("review_mesh_r8")
		var clean:=PackedScene.new();clean.pack(geometry);var resource:=preload("res://scripts/archive_prepared_r8.gd").new();resource.geometry=clean;resource.details=details;resource.source_fingerprint=str(catalog.manifest(str(row.id)).cache_fingerprint)
		var error:int=ResourceSaver.save(resource,path,ResourceSaver.FLAG_COMPRESS)
		results.append({"id":row.id,"saved":error==OK,"sites":details.weapon_sites.size(),"panels":details.panels.size(),"removed_triangles":details.removed_symbol_triangles,"seconds":(Time.get_ticks_msec()-started)*.001})
		print("ADAPTER_R8 ",row.slug," sites=",details.weapon_sites.size()," panels=",details.panels.size()," seconds=",(Time.get_ticks_msec()-started)*.001," saved=",error==OK)
		geometry.free();packed=null;resource=null;clean=null;details.clear()
		await process_frame
	var file=FileAccess.open(output+"/ADAPTER-BAKE-R8.json",FileAccess.WRITE);file.store_string(JSON.stringify(results,"\t"));quit()
