extends SceneTree
func _initialize():call_deferred("run")
func run():
	preload("res://scripts/archive_catalog1003.gd").mount_local_pack_r7();var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate();var tracked:Array=[]
	for child in scene.find_children("*","Node",true,false):tracked.append(child.get_instance_id())
	scene.free();var left=0
	for id in tracked:
		if is_instance_id_valid(id):left+=1;var child=instance_from_id(id);print("BASELINE_ORPHAN ",child," name=",child.name," parent=",child.get_parent())
	print("BASELINE_LEFT ",left);quit()
