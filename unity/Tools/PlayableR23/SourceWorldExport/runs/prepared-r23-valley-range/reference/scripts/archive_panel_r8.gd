extends StaticBody3D
var map:Node
var key=""
var box:AABB
var view:MeshInstance3D
var health=1.0
var _broken=false
var glass_r10=false
var sound_r18=""
var batch_r17:WeakRef
func setup(owner_map:Node,id:String,row:Dictionary)->void:
	map=owner_map;key=id;name="BreakablePassageR8_"+id;box=row.box;collision_layer=1;collision_mask=0;add_to_group("climbable");add_to_group("construction_terrain");set_meta("archive_breakable_r8",true)
	glass_r10=bool(row.glass);sound_r18=str(row.get("sound_r18",""))
	view=MeshInstance3D.new();view.mesh=row.mesh;view.material_override=row.material;add_child(view)
	var all_faces:PackedVector3Array=row.faces.duplicate()
	for attachment in row.get("attachments",[]):
		var mesh:=MeshInstance3D.new();mesh.mesh=attachment.mesh;mesh.material_override=attachment.material;view.add_child(mesh);all_faces.append_array(attachment.faces)
		box=box.merge(attachment.mesh.get_aabb())
	var shape:=CollisionShape3D.new();var faces:=ConcavePolygonShape3D.new();faces.backface_collision=true;faces.set_faces(all_faces);shape.shape=faces;add_child(shape)
func _physics_process(_delta:float)->void:
	var net:Node=map._game.network_bridge.net
	if net.active:
		var wanted:bool=net.archive_broken_r8.has(key)
		if wanted!=_broken:_apply(wanted)
		if _broken or not net.server or net.stage not in ["lobby","countdown","match"]:return
		for pose in net.poses.values():
			if touches(pose.root.origin) or touches(pose.head.origin-Vector3.UP*.5):take_damage(1);return
	elif not _broken and map._game.player.input_enabled and not map._game.front_end_active:
		if touches(map._game.player.global_position) or touches(map._game.player.get_view_camera().global_position-Vector3.UP*.5):take_damage(1)
func touches(point:Vector3)->bool:return box.grow(.48).has_point(to_local(point)+Vector3.UP*.9)
func take_damage(_amount:float)->void:
	var net:Node=map._game.network_bridge.net
	if _broken or (net.active and not net.server):return
	if net.active:net.archive_broken_r8[key]=true;net._publish()
	_apply(true)
func take_melee_damage(amount:float)->void:take_damage(amount)
func _apply(broken:bool)->void:
	_broken=broken;health=0 if broken else 1;view.visible=not broken;collision_layer=0 if broken else 1
	if batch_r17!=null and is_instance_valid(batch_r17.get_ref()):batch_r17.get_ref().changed(self)
	if broken:
		var voice:=AudioStreamPlayer3D.new();map.add_child(voice);voice.global_position=to_global(box.get_center());voice.stream=load(sound_r18 if not sound_r18.is_empty() else ("res://assets/audio/sfx/glass_break_r8.wav" if glass_r10 else "res://assets/audio/sfx/wood_break_r10.wav"));voice.bus="SFX";voice.volume_db=-3;voice.unit_size=5;voice.max_distance=35;voice.finished.connect(voice.queue_free);voice.play()
