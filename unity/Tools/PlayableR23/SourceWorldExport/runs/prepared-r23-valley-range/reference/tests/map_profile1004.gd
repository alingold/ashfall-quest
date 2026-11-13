extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
func _initialize()->void:call_deferred("run")
func run()->void:
	var args=OS.get_cmdline_user_args();var id="archive:"+args[0];var stage=Node3D.new();root.add_child(stage);Engine.max_fps=60
	var geometry=load(Catalog.scene(id)).instantiate();stage.add_child(geometry);var meta=Catalog.manifest(id)
	var world=WorldEnvironment.new();world.environment=Environment.new();world.environment.background_mode=Environment.BG_COLOR;world.environment.background_color=Color("9db6c6");world.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;world.environment.ambient_light_color=Color("c5d5e2");world.environment.ambient_light_energy=.8;world.environment.tonemap_mode=Environment.TONE_MAPPER_FILMIC;stage.add_child(world)
	var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-53,-28,0);stage.add_child(sun)
	var camera=Camera3D.new();camera.far=10000;camera.current=true;stage.add_child(camera);RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	var c:Array=meta.center;var target=Vector3(c[0],c[1],c[2]);var low:Array=meta.bounds_min;var high:Array=meta.bounds_max;var radius=clampf(maxf(high[0]-low[0],high[2]-low[2])*.30,45,230);var samples:Array=[]
	for view in 3:
		camera.position=target+Vector3(cos(view*2.1+.65)*radius,clampf(radius*.5,30,110),sin(view*2.1+.65)*radius);camera.look_at(target,Vector3.UP)
		# Avoid the previous camera's delayed Performance counters and warm pipelines.
		for warm in 12:await process_frame
		if view==0:root.get_texture().get_image().save_png(args[1].get_basename()+"-view.png")
		for frame in 36:
			var before=Time.get_ticks_usec();await process_frame
			samples.append({"view":view,"wall_ms":(Time.get_ticks_usec()-before)/1000.0,"gpu_ms":RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()),"render_cpu_ms":RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()),"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"video_memory":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)})
	var rendered:bool=samples.any(func(row:Dictionary):return row.draw_calls>0 and row.primitives>0 and row.gpu_ms>0)
	var result={"passed":rendered,"map":id,"samples":samples,"geometry":meta.solid_triangles,"batches":meta.draw_batches,"scope":"actual desktop Vulkan mobile renderer, isolated archive environment geometry at 1280x800, 3 deterministic cameras, 12 warm frames then 36 samples each, max_fps=60; wall time includes cap. GPU/render CPU counters are not headset performance; ASHFALL actors/loot are measured separately on representative worst maps."}
	var f=FileAccess.open(args[1],FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("PROFILE1004 "+str(rendered)+" "+id);quit(0 if rendered else 1)
