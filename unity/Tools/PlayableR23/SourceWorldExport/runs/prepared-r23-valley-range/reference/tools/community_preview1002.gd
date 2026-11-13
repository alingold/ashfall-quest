extends SceneTree
## Render the actual converted map only, without game HUD or test overlays.
func _initialize()->void:call_deferred("render_preview")
func render_preview()->void:
	var stage=Node3D.new();root.add_child(stage)
	stage.add_child(load("res://local_community/converted/map.scn").instantiate())
	var world=WorldEnvironment.new();world.environment=Environment.new();world.environment.background_mode=Environment.BG_COLOR;world.environment.background_color=Color("9db6c6");world.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;world.environment.ambient_light_color=Color("c5d5e2");world.environment.ambient_light_energy=.8;world.environment.tonemap_mode=Environment.TONE_MAPPER_FILMIC;stage.add_child(world)
	var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-53,-28,0);sun.light_energy=1.1;stage.add_child(sun)
	var camera=Camera3D.new();camera.far=1600;stage.add_child(camera);camera.position=Vector3(50,120,90);camera.look_at(Vector3(0,40,0),Vector3.UP);camera.current=true
	for i in 10:await process_frame
	root.get_texture().get_image().save_png("res://local_community/converted/preview.png")
	print("COMMUNITY_PREVIEW_OK");quit()
