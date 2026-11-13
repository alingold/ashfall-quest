extends SceneTree
func _initialize()->void:call_deferred("run")
func run()->void:
	var args=OS.get_cmdline_user_args();var path=preload("res://scripts/archive_catalog1003.gd").scene("archive:"+args[0]);var scene=load(path).instantiate();root.add_child(scene);var facts:Array=[]
	for node in scene.find_children("*","MeshInstance3D",true,false):
		for surface in node.mesh.get_surface_count():
			var arrays:Array=node.mesh.surface_get_arrays(surface)
			facts.append({"name":node.name,"surface":surface,"vertices":arrays[Mesh.ARRAY_VERTEX].size(),"indices":arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX]!=null else 0,"format":node.mesh.surface_get_format(surface)})
	var row={"map":args[0],"surfaces":facts};var file=FileAccess.open(args[1],FileAccess.WRITE);file.store_string(JSON.stringify(row,"\t"));file.close();quit()
