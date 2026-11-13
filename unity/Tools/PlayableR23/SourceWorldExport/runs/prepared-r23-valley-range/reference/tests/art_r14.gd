extends SceneTree
const Art=preload("res://scripts/map_art_repair_r14.gd")
func _initialize():call_deferred("run")
func _colliders(root_node:Node)->Array:
	var result:Array=[]
	for body in root_node.find_children("*","CollisionShape3D",true,false):result.append([body.get_instance_id(),body.shape.get_instance_id(),body.transform,body.disabled])
	return result
func run():
	var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.mount_local_pack_r7()
	var rows:Array=[]
	var audit:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_art_audit_r14.json"))
	assert(audit.maps.size()==105);assert(audit.audit_summary.missing_or_undecodable_images==0)
	for slug in ["alfa-aglae-souza","phoenix-bigbox-vr","classic-bigbox-vr","banana-land-gods-pitdevil"]:
		var scene:Node=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();var before:Array=_colliders(scene);var before_ids:Dictionary={}
		for child in scene.find_children("*","Node",true,false):before_ids[child.get_instance_id()]=[str(scene.get_path_to(child)),child.name]
		var begin=Time.get_ticks_msec();var stats:Dictionary=Art.apply(scene,"archive:"+slug);var elapsed=Time.get_ticks_msec()-begin
		assert(_colliders(scene)==before,"Art repair changed collision geometry")
		assert(Art.apply(scene,"archive:"+slug)==stats,"Art pass must be idempotent")
		if slug=="alfa-aglae-souza":assert(stats.container_end_panels>300 and stats.wood_panels>0)
		if slug=="banana-land-gods-pitdevil":assert(stats.container_end_panels==0 and stats.wood_panels==0,"Unrelated art changed")
		rows.append({"id":"archive:"+slug,"stats":stats,"collision_nodes_unchanged":before.size(),"apply_ms":elapsed});print("ART_R14 ",slug," ",stats," collisions=",before.size()," ms=",elapsed);var tracked:Array=[]
		for child in scene.find_children("*","Node",true,false):tracked.append(child.get_instance_id())
		scene.free()
		for id in tracked:
			if is_instance_id_valid(id):
				var orphan=instance_from_id(id);print("ART_ORPHAN_R14 ",orphan," name=",orphan.name," parent=",orphan.get_parent()," script=",orphan.get_script()," mat=",orphan.material_override.resource_name if orphan is MeshInstance3D else ""," before=",before_ids.get(id,"NEW"));quit(1);return
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/ART-R14-TEST.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":true,"scope":"Offline XR-off engine validation; no headset or multiplayer claim.","audit_summary":audit.audit_summary,"maps":rows},"\t"));Art._materials.clear()
	for i in 4:await process_frame
	quit()
