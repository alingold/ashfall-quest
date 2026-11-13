extends SceneTree
var game:Node
var caption:Label
var receipts:Array=[]
var failures:Array=[]
func _initialize()->void:call_deferred("run")
func freeze(node:Node)->void:
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run()->void:
	var args=OS.get_cmdline_user_args();game=preload("res://scripts/main.gd").new();root.add_child(game)
	var layer=CanvasLayer.new();layer.layer=250;root.add_child(layer);var strip=ColorRect.new();strip.color=Color(.015,.025,.04,.95);strip.position=Vector2(0,470);strip.size=Vector2(960,70);layer.add_child(strip);caption=Label.new();caption.position=Vector2(16,476);caption.add_theme_font_size_override("font_size",18);layer.add_child(caption)
	for i in 5:await process_frame
	for slug in ["melis-cafe-gods-pitdevil","robotics-2-gods-pitdevil","banana-land-gods-pitdevil"]:
		game.front_menu.selected_map="archive:"+slug
		if game.front_end_active:game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,"archive:"+slug)
		for i in 8:await physics_frame
		var map:Node=game.active_sandbox_map
		if not is_instance_valid(map) or not map.map_ready or map.map_id!="archive:"+slug:push_error("Tour map did not load "+slug);quit(2);return
		if args.has("--cached-mips"):preload("res://scripts/archive_materials1004.gd").apply(map._geometry)
		freeze(game);game.player.input_enabled=true;game.player.global_position=map.spawn;game.player.reset_motion();var camera:Camera3D=game.player.get_view_camera();camera.current=true
		var start:Vector3=game.player.global_position;var before_ammo:int=game.weapon.ammo
		caption.text=str(map._meta.title)+(" / CACHED MIPMAPS" if args.has("--cached-mips") else " / ORIGINAL MATERIALS")+"\nActual Main render | scripted offline motion | XR OFF | local archive only"
		for frame in 180:
			if frame<90:
				var heading=Vector3.FORWARD.rotated(Vector3.UP,float(frame)/180)
				game.player.apply_locomotion_motion(heading,heading,false,false,1.0/30);camera.global_position=game.player.global_position+Vector3.UP*1.7;camera.look_at(camera.global_position+heading*20,Vector3.UP)
			else:
				var c:Array=map._meta.center;var center=map.global_position+Vector3(c[0],c[1],c[2]);var angle=.65+float(frame-90)/180;camera.global_position=center+Vector3(cos(angle)*145,70,sin(angle)*145);camera.look_at(center,Vector3.UP)
			var gun:Node=game.weapon.weapons[game.weapon.selected];gun._process(0)
			if frame==35:game.weapon._draw_delay=0;gun._requires_trigger_release=false;gun.cooldown=0;game.weapon.fire()
			await process_frame
		if game.weapon.ammo!=before_ammo-1:failures.append("scripted shot not fired "+slug)
		receipts.append({"map":slug,"spawn":map.spawn,"end":game.player.global_position,"distance":start.distance_to(game.player.global_position),"ammo_before":before_ammo,"ammo_after":game.weapon.ammo,"frames":180})
	var result={"passed":receipts.size()==3 and failures.is_empty(),"failures":failures,"maps":receipts,"variant":"cached mips" if args.has("--cached-mips") else "original","scope":"actual Main + map geometry/loot/HUD, scripted collision-aware player movement and one gun fire per map, then free-camera overview. Fixed movie30fps; XR off, no physical VR/user or WAN input. Not evidence of the original map scripts."};var file=FileAccess.open(args[0],FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();game._quitting=true;quit(0 if result.passed else 1)
