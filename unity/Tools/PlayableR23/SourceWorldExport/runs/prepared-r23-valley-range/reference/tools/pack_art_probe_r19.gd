extends SceneTree
func _initialize():
 var output=OS.get_cmdline_user_args()[0]
 var config=output+"/art-probe-project.godot"
 var file=FileAccess.open(config,FileAccess.WRITE);file.store_string("config_version=5\n[application]\nconfig/name=\"R19 art resource probe\"\n");file.close()
 var pack=PCKPacker.new();assert(pack.pck_start(output+"/art-resource-probe-r19.pck")==OK)
 assert(pack.add_file("res://project.godot",config)==OK)
 for path in ["res://assets/art_r19/freight_texture.res","res://tests/art_resource_paths_r19.gd"]:assert(pack.add_file(path,ProjectSettings.globalize_path(path))==OK)
 assert(pack.flush()==OK);print("R19_ISOLATED_RESOURCE_PACK_WRITTEN no raw PNG or import cache");quit()
