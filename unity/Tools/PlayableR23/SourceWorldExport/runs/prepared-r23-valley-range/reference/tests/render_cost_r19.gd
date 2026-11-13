extends SceneTree
## Native-only, fixed camera and frozen gameplay. Coordinator GPU slot required.
var game:Node
func _initialize():call_deferred("run")
func argument(prefix:String,fallback:String)->String:
	for value in OS.get_cmdline_user_args():
		if value.begins_with(prefix):return value.trim_prefix(prefix)
	return fallback
func run():
	if DisplayServer.get_name()=="headless":print("RENDER_COST_NATIVE_REQUIRED");quit(2);return
	var slug:String=argument("--r18-map=","alfa-aglae-souza")
	var tag:String="baseline" if OS.get_cmdline_user_args().has("--no-postcut-lods-r19") else "postcut"
	if OS.get_cmdline_user_args().has("--postcut-tight-bounds-r19"):tag+="-tightbounds"
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:"+slug;game._start_from_title()
	var deadline:int=Time.get_ticks_msec()+180000
	while (not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready) and Time.get_ticks_msec()<deadline:await process_frame
	assert(is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready)
	var map:Node=game.active_sandbox_map;game.process_mode=Node.PROCESS_MODE_DISABLED
	var camera:Camera3D=game.player.desktop_camera;var meta:Dictionary=map._meta
	var focus:Vector3=map.global_position+Vector3(meta.center[0],meta.center[1],meta.center[2])
	var floor:Vector3=map._floor(focus+Vector3.UP*40,120)
	var eye:Vector3=(floor if floor.is_finite() else focus)+Vector3.UP*1.7
	var origin:String=argument("--r19-origin=","center")
	if origin=="lobby":eye=map.warmup_spawn_r8+Vector3.UP*1.7;tag+="-lobby"
	var rid:RID=root.get_viewport_rid();RenderingServer.viewport_set_measure_render_time(rid,true)
	var rows:Array=[]
	for yaw in range(0,360,45):
		camera.global_position=eye;camera.global_rotation=Vector3(0,deg_to_rad(yaw),0)
		for i in 32:await process_frame
		var gpu:Array=[];var cpu:Array=[];var draws:Array=[];var triangles:Array=[]
		for i in 32:
			await RenderingServer.frame_post_draw
			gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(rid));cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(rid))
			draws.append(RenderingServer.viewport_get_render_info(rid,RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE,RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME))
			triangles.append(RenderingServer.viewport_get_render_info(rid,RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE,RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME))
		gpu.sort();cpu.sort();draws.sort();triangles.sort()
		rows.append({"yaw":yaw,"gpu_ms_median":gpu[16],"render_cpu_ms_median":cpu[16],"visible_draws_median":draws[16],"visible_primitives_median":triangles[16]})
		if OS.get_cmdline_user_args().has("--r19-png"):root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/R19-COST-"+slug+"-"+tag+"-yaw"+str(yaw)+".png")
	var report:Dictionary={"map":slug,"tag":tag,"origin":origin,"views":rows,"eye":str(eye-map.global_position),"far":camera.far,"fov":camera.fov,"viewport_size":str(root.get_visible_rect().size),"lod_threshold":root.mesh_lod_threshold,"msaa":root.msaa_3d,"cache":map.statistics.get("mesh_lods_r16",{}),"limits":"Frozen desktop native viewport; no XR/compositor/headset performance or comfort claim. Eight fixed headings; triangle counts are primitives reported by RenderingServer."}
	var file:=FileAccess.open(OS.get_cmdline_user_args()[0]+"/R19-COST-"+slug+"-"+tag+".json",FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("R19_RENDER_COST ",JSON.stringify(report));game._quitting=true;game.queue_free();await process_frame;quit()
