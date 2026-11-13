extends RefCounted
static func capture(map:Node)->void:
	if DisplayServer.get_name()=="headless":return
	var viewport:=SubViewport.new();viewport.size=Vector2i(1024,1024);viewport.world_3d=map.get_world_3d();viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS;map.add_child(viewport)
	var camera:=Camera3D.new();camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.cull_mask=1;camera.far=2500;viewport.add_child(camera)
	var row:Dictionary=preload("res://scripts/map_catalog1000.gd").info(map.map_id);var extent:Vector2=row.max-row.min
	# Match the tactical map's exact north-up rectangle; its texture is stretched to the same rectangle.
	viewport.size=Vector2i(1024,maxi(128,roundi(1024*extent.y/extent.x)));camera.keep_aspect=Camera3D.KEEP_WIDTH;camera.size=extent.x
	camera.global_transform=Transform3D(Basis(Vector3.RIGHT,Vector3.FORWARD,Vector3.UP),map.global_position+Vector3((row.min.x+row.max.x)*.5,float(map._meta.solid_max[1])+10,(row.min.y+row.max.y)*.5))
	camera.current=true
	await map.get_tree().process_frame
	await RenderingServer.frame_post_draw
	if is_instance_valid(map) and is_instance_valid(viewport):
		map.tactical_texture_r8=ImageTexture.create_from_image(viewport.get_texture().get_image());viewport.queue_free()
