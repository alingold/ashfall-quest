extends SceneTree
func _initialize():call_deferred("run")
func geometry_state(node:Node)->Dictionary:
	var collision:=HashingContext.new();collision.start(HashingContext.HASH_SHA256);var triangles=0;var textured:Dictionary={}
	for shape in node.find_children("*","CollisionShape3D",true,false):
		if shape.shape is ConcavePolygonShape3D:collision.update(shape.shape.get_faces().to_byte_array())
	for mesh in node.find_children("*","MeshInstance3D",true,false):
		triangles+=mesh.mesh.get_faces().size()/3
		if mesh.material_override!=null and mesh.material_override.resource_name!="vc" and not str(mesh.name).begins_with("RecoveredDetailR10_"):textured[str(mesh.name)]=mesh.material_override.get_instance_id()
	return {"collision":collision.finish().hex_encode(),"triangles":triangles,"textured":textured}
func run():
	preload("res://scripts/archive_catalog1003.gd").catalog();var failures:Array=[];var facts:Array=[];var checks=0
	for slug in ["alfa-aglae-souza","classic-bigbox-vr","afterlife-gods-alamo-hank"]:
		var scene:Node=load("res://local_archive/maps/"+slug+"/converted/map.scn").instantiate();var before:Dictionary=geometry_state(scene);var matched:Dictionary=preload("res://scripts/archive_prop_materials_r10.gd").apply(scene);var after:Dictionary=geometry_state(scene)
		# New detail meshes add materials; compare every original binding by its original node name.
		for name in after.textured.keys():
			if not before.textured.has(name):after.textured.erase(name)
		for key in ["collision","triangles","textured"]:
			checks+=1
			if before[key]!=after[key]:failures.append(slug+" "+key)
		facts.append({"slug":slug,"matched":matched,"collision_sha256":after.collision,"triangles":after.triangles,"original_textured_materials":after.textured.size()});scene.free();await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/PROP-PRESERVATION-R10.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"shared rendering-only prop extraction preserves original triangle count, every collision face byte, and all original textured material references"},"\t"));print("PROP_PRESERVATION_R10 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
