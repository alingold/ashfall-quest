extends SceneTree
## Census of every prepared archive map's source materials: which surfaces use
## cutout/blended transparency (the leaf/fence candidates), where the material is
## bound, and their triangle share. Read-only; no gameplay or rendering state.
func _initialize():call_deferred("run")
func run():
	var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog()
	var index:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_lod_index_r16.json"))
	var maps:Dictionary={};var totals:Dictionary={}
	for slug in index.maps:
		var path:String="res://local_archive_r10/"+slug+".res"
		if not FileAccess.file_exists(path):maps[slug]={"missing":true};continue
		var scene:Node=load(path).geometry.instantiate();var row:Dictionary={}
		for view in scene.find_children("*","MeshInstance3D",true,false):
			if view.mesh==null:continue
			for s in view.mesh.get_surface_count():
				var m:Material=view.material_override;var origin="override"
				if m==null:m=view.mesh.surface_get_material(s);origin="surface"
				var c="none"
				if m is ShaderMaterial:c="shader"
				elif m is BaseMaterial3D:
					c=["opaque","blend","scissor","hash","prepass"][m.transparency]
					if c=="blend" and m.albedo_color.a>.95 and m.resource_name!="pb8a19331":c="blend->scissor"
				var key=c+"@"+origin
				var tris:int=maxi(view.mesh.surface_get_array_index_len(s),view.mesh.surface_get_array_len(s))/3
				if not row.has(key):row[key]={"surfaces":0,"triangles":0,"materials":{}}
				row[key].surfaces+=1;row[key].triangles+=tris
				if m!=null:row[key].materials[str(m.resource_name)]=true
				if not totals.has(key):totals[key]={"maps":{},"triangles":0}
				totals[key].maps[slug]=true;totals[key].triangles+=tris
		for key in row:row[key].materials=row[key].materials.keys()
		maps[slug]=row;scene.free()
	for key in totals:totals[key].maps=totals[key].maps.size()
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/R18-CUTOUT-CENSUS.json",FileAccess.WRITE);file.store_string(JSON.stringify({"totals":totals,"maps":maps},"\t"));file.close()
	print("R18_CENSUS ",JSON.stringify(totals));quit(0)
