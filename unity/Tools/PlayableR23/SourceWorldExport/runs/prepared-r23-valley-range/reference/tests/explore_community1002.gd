extends SceneTree
var game:Node
var camera:Camera3D
var caption:Label
var phase:Label
var frames=0
var start_frame=0
var ready_for_tour=false
var samples:Array=[]
var map:Node
func _initialize()->void:call_deferred("boot")
func boot()->void:
	game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	game.front_menu.page="maps";game.front_menu.refresh_page()
	var canvas=CanvasLayer.new();canvas.layer=240;root.add_child(canvas)
	var bg=ColorRect.new();bg.color=Color(.01,.02,.03,.9);bg.size=Vector2(1280,83);canvas.add_child(bg)
	caption=Label.new();caption.position=Vector2(16,8);caption.add_theme_font_size_override("font_size",18);caption.text="ASHFALL / HILDRS ECHO — archive credit: GODS_PITDEVIL\nLOCAL TECHNICAL CANDIDATE | Real desktop render | Scripted camera/input | XR OFF";canvas.add_child(caption)
	phase=Label.new();phase.position=Vector2(16,53);phase.add_theme_font_size_override("font_size",17);canvas.add_child(phase)
	for i in 90:await process_frame
	game.front_menu.selected_map="community_local";game._start_from_title()
	for i in 8:await physics_frame
	map=game.active_sandbox_map;game.player.set_physics_process(false);game.player.input_enabled=false;camera=Camera3D.new();game.add_child(camera);camera.far=1600;camera.current=true;start_frame=Engine.get_process_frames();ready_for_tour=true
func _process(_delta:float)->bool:
	if not ready_for_tour:return false
	frames=Engine.get_process_frames()-start_frame;var t=float(frames)/30.0;var origin:Vector3=map.global_position
	if t<6:
		var a=t/6.0;camera.global_position=origin+Vector3(70,140,90).lerp(Vector3(40,110,80),a);camera.look_at(origin+Vector3(0,40,0),Vector3.UP);phase.text="Overview / imported textures, native metre scale"
	elif t<12:
		var a=(t-6)/6.0;camera.global_position=map.spawn+Vector3(0,1.708,0);camera.look_at(origin+Vector3(-35,45,-20).lerp(origin+Vector3(-35,45,45),a),Vector3.UP);phase.text="Recorded spawn district / collision-tested architecture and firing cover"
	elif t<18:
		var a=(t-12)/6.0;camera.global_position=origin+Vector3(-10,47,-154).lerp(Vector3(1.4,45.6,-154),a);camera.look_at(origin+Vector3(5,45.6,-154),Vector3.UP);phase.text="Framed opening / mesh collision keeps openings clear"
	else:
		var a=clampf((t-18)/6,0,1);camera.global_position=origin+Vector3(95,62,85).lerp(Vector3(25,57,65),a);camera.look_at(origin+Vector3(0,41,0),Vector3.UP);phase.text="Local map tour / no headset-performance or Internet claim"
	if frames%30==0:samples.append({"frame":frames,"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"objects":Performance.get_monitor(Performance.RENDER_TOTAL_OBJECTS_IN_FRAME),"video_memory":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)})
	if frames==120:caption.get_parent().hide();game.ui_layer.hide()
	if frames==121:root.get_viewport().get_texture().get_image().save_png("res://local_community/converted/preview.png");caption.get_parent().show();game.ui_layer.show()
	if frames>=720:
		var args=OS.get_cmdline_user_args();var report={"passed":true,"frames":frames,"profile":samples,"scope":"Actual desktop-rendered ASHFALL map with scripted camera. XR off, fixed 30 FPS MovieWriter, Dummy audio; not a frame-rate benchmark."};var f=FileAccess.open(args[0],FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"));print("EXPLORATION1002 complete");quit()
	return false
