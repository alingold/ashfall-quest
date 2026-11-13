extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
var game:Node
var camera:Camera3D
var label:Label
var details:Label
var samples:Array=[]
var maps:Array=["archive:5-levels-zyro","archive:classic-bigbox-vr","archive:melis-cafe-gods-pitdevil","archive:mushy-gods-putangfreyja"]
func _initialize()->void:call_deferred("run")
func run()->void:
	game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	var canvas=CanvasLayer.new();canvas.layer=250;root.add_child(canvas);var bg=ColorRect.new();bg.color=Color(.01,.02,.03,.9);bg.size=Vector2(1280,88);canvas.add_child(bg)
	label=Label.new();label.position=Vector2(80,6);label.add_theme_font_size_override("font_size",16);label.text="ASHFALL 1003 / COMPLETE LOCAL MAP ARCHIVE\nDesktop render | Scripted camera | XR OFF | Reuse rights unverified";canvas.add_child(label)
	details=Label.new();details.position=Vector2(80,56);details.add_theme_font_size_override("font_size",16);canvas.add_child(details)
	var menu:Node=game.front_menu;menu.page="maps";menu._map_page1003=0;menu.refresh_page();details.text="%d archive maps + preserved maps · %d pages · geometry loads on selection"%[Catalog.rows().size(),ceili(float(menu._map_ids1003.size())/4)]
	for i in 60:await process_frame
	menu._map_page1003=ceili(float(menu._map_ids1003.size())/4)-1;menu.refresh_page()
	for i in 60:await process_frame
	for id in maps:
		var info=Catalog.info(id);details.text="Loading "+str(info.title)+" · archive credit: "+str(info.creator)
		if game.front_end_active:menu.selected_map=id;game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
		for i in 8:await physics_frame
		var map:Node=game.active_sandbox_map
		if not is_instance_valid(map) or not map.map_ready:push_error("Tour map failed: "+id);quit(1);return
		game.player.input_enabled=false;game.player.set_physics_process(false)
		game.weapon.hide();game.body_visual.hide();game.combat_hud.hide()
		if not is_instance_valid(camera):camera=Camera3D.new();game.add_child(camera);camera.far=10000
		camera.current=true;var meta:Dictionary=map._meta;var center:Array=meta.center;var target=map.global_position+Vector3(center[0],center[1],center[2]);var low:Array=meta.solid_min;var high:Array=meta.solid_max
		var play_low:Array=meta.bounds_min;var play_high:Array=meta.bounds_max
		var span=clampf(maxf(float(play_high[0])-float(play_low[0]),float(play_high[2])-float(play_low[2]))*.30,38,230);var vertical=clampf(span*.42,22,95)
		# Shot framing only: the tiny vertical arena is surrounded by a large scenery floor.
		if id=="archive:5-levels-zyro":span=115;vertical=85
		details.text=str(info.title)+" · "+str(info.creator)+" · ASHFALL gameplay / original map scripts not recreated"
		for frame in 210:
			var angle=.65+float(frame)/210*.55;camera.global_position=target+Vector3(cos(angle)*span,vertical,sin(angle)*span);camera.look_at(target,Vector3.UP)
			if frame%30==0:samples.append({"map":id,"frame":frame,"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"video_memory":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)})
			await process_frame
		# Brief closer view, at a validated player spawn, with a camera pan.
		var close=map.spawn;var distance=INF;var desired=target+Vector3(span*.35,-4,span*.35)
		for point in map.loot_points:
			var eye:Vector3=point+Vector3.UP*1.1;var direction:Vector3=(target+Vector3.UP*3-eye).normalized()
			var hit=game.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(eye,eye+direction*8,1,map._floor_exclusions))
			if not hit.is_empty():continue
			if point.distance_squared_to(desired)<distance:distance=point.distance_squared_to(desired);close=point
		for frame in 90:
			camera.global_position=close+Vector3.UP*1.1;var angle=float(frame)/90*.55;var toward=target+Vector3.UP*3-camera.global_position
			if id=="archive:5-levels-zyro":camera.global_position=target+Vector3(cos(angle+3.8)*110,45,sin(angle+3.8)*110);toward=target-camera.global_position
			if toward.length()<1:toward=Vector3.FORWARD*10
			camera.look_at(camera.global_position+toward.rotated(Vector3.UP,angle),Vector3.UP);await process_frame
	var args=OS.get_cmdline_user_args();var report={"passed":true,"maps":maps,"samples":samples,"scope":"Actual-rendered scripted multi-map tour; XR off, Dummy audio, fixed 30 FPS MovieWriter. Not a headset or achieved-FPS benchmark."};var f=FileAccess.open(args[0],FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"));f.close();print("ARCHIVE_TOUR_OK");quit()
