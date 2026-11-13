extends SceneTree
var game:Node
func _initialize()->void:call_deferred("run")
func freeze(node:Node)->void:
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run()->void:
	var args=OS.get_cmdline_user_args();Engine.max_fps=60;game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	game.front_menu.selected_map="archive:"+args[0];game._start_from_title()
	for i in 8:await physics_frame
	var map:Node=game.active_sandbox_map
	if not is_instance_valid(map) or not map.map_ready:quit(2);return
	var experiment:Dictionary={}
	if args.has("--cached-mips"):experiment=preload("res://scripts/archive_materials1004.gd").apply(map._geometry)
	elif args.has("--mips"):experiment=preload("res://tests/mipmap_experiment1004.gd").apply(map._geometry)
	freeze(game);var camera:Camera3D=game.player.get_view_camera();camera.current=true
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	if args.has("--paired"):
		var original:Dictionary=await measure_views(map,camera,args[1].get_basename()+"-original")
		var cached=preload("res://scripts/archive_materials1004.gd").apply(map._geometry)
		var optimized:Dictionary=await measure_views(map,camera,args[1].get_basename()+"-cached")
		var result={"passed":original.valid_views.size()==3 and optimized.valid_views.size()==3 and cached.failures.is_empty(),"map":args[0],"original":original,"cached":optimized,"cache":cached,"scope":"matched original/cached material views within one actual frozen Main process; identical cameras/geometry/loot/HUD. 1280x800 mobile Vulkan,15warm+60samples/view, cap60; compare GPU/renderCPU counters, not capped wall throughput. No headset/WAN acceptance."};var output=FileAccess.open(args[1],FileAccess.WRITE);output.store_string(JSON.stringify(result,"\t"));output.close();print("PAIRED1004 "+str(result.passed));game._quitting=true;quit(0 if result.passed else 1);return
	var samples:Array=[];var viewpoints:Array=[]
	for view in 3:
		game.player.global_position=map.spawn_points[mini(view,map.spawn_points.size()-1)];game.player.reset_motion()
		var point:Vector3=game.player.global_position+Vector3.UP*1.7;camera.global_position=point
		camera.look_at(point+Vector3.FORWARD.rotated(Vector3.UP,float(view)*1.2)*20,Vector3.UP);viewpoints.append(camera.global_transform)
		game.weapon.weapons[game.weapon.selected]._process(0)
		for i in 15:await process_frame
		root.get_texture().get_image().save_png(args[1].get_basename()+"-view"+str(view)+".png")
		for i in 60:
			var before=Time.get_ticks_usec();await process_frame
			samples.append({"view":view,"wall_ms":(Time.get_ticks_usec()-before)/1000.0,"gpu_ms":RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()),"render_cpu_ms":RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()),"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"video_memory":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)})
	var valid_views:Array=[]
	for view in 3:
		if samples.any(func(row:Dictionary):return row.view==view and row.draw_calls>0 and row.gpu_ms>0):valid_views.append(view)
	var result={"passed":valid_views.size()==3,"valid_views":valid_views,"map":args[0],"samples":samples,"viewpoints":viewpoints,"experiment":experiment,"scope":"actual Main scene and archive adapter, 3 standable spawn-height cameras, gameplay scripts frozen after load to isolate full-scene rendering; weapons/ground loot/HUD retained. 1280x800 desktop Vulkan mobile, 15 warm+60 samples/view, cap60. Wall timing includes cap and is not maximum throughput; GPU/render CPU counters are compared separately. No physical VR or online acceptance."};var file=FileAccess.open(args[1],FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();print("FULLGAME1004 "+str(result.passed));game._quitting=true;quit(0 if result.passed else 1)
func measure_views(map:Node,camera:Camera3D,prefix:String)->Dictionary:
	var samples:Array=[];var views:Array=[]
	for view in 3:
		game.player.global_position=map.spawn_points[mini(view,map.spawn_points.size()-1)];game.player.reset_motion();var at:Vector3=game.player.global_position+Vector3.UP*1.7;camera.global_position=at;camera.look_at(at+Vector3.FORWARD.rotated(Vector3.UP,float(view)*1.2)*20,Vector3.UP);game.weapon.weapons[game.weapon.selected]._process(0)
		for i in 15:await process_frame
		root.get_texture().get_image().save_png(prefix+"-view"+str(view)+".png")
		for i in 60:
			await process_frame;samples.append({"view":view,"gpu_ms":RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()),"render_cpu_ms":RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()),"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"video_memory":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)})
		if samples.filter(func(row:Dictionary):return row.view==view and row.draw_calls>0 and row.gpu_ms>0).size()>=54:views.append(view)
	return {"samples":samples,"valid_views":views}
