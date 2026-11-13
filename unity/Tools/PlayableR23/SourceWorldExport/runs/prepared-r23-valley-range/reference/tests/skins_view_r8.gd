extends SceneTree
func _initialize():call_deferred("run")
func run():
	var output:String=OS.get_cmdline_user_args()[0];var game:Node=load("res://scripts/main.gd").new();root.add_child(game)
	for node in game.find_children("*","Node",true,false):node.set_process(false);node.set_physics_process(false)
	game.set_process(false);game.front_menu.set_active(false);game.front_menu.lobby.hide();game.world.hide();game.combat_hud.hide();game.ui_layer.hide();game.weapon.hide();game.vr_label.hide();game.vr_cue.hide();game.body_visual.hide()
	var environment:=Environment.new();environment.background_mode=Environment.BG_COLOR;environment.background_color=Color("142630");environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;environment.ambient_light_color=Color.WHITE;environment.ambient_light_energy=.65;game.get_world_3d().environment=environment
	var sun:=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-40,-35,0);sun.light_energy=1.3;game.add_child(sun)
	var figures:=Node3D.new();game.add_child(figures);figures.position=Vector3(4000,0,0)
	for i in 4:
		var figure:Node=preload("res://scripts/lobby_map_091.gd").make_avatar(i+3);figures.add_child(figure);figure.position.x=(i-1.5)*1.65;figure.rotation.y=PI
		var label:=Label3D.new();label.text=["GPT-6 ASTRA","GPT-6.1 SOL","GEMINI 4 ARGON","CLAUDE OPUS"][i];label.position=Vector3((i-1.5)*1.65,2.25,0);label.font_size=34;label.pixel_size=.002;figures.add_child(label)
	var camera:Camera3D=game.player.get_view_camera();camera.global_position=figures.global_position+Vector3(0,1.25,5);camera.look_at(figures.global_position+Vector3(0,1.15,0));camera.fov=55
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/SKINS-WITCH-DOCTORS-R8.png")
	game.queue_free();await process_frame;quit()
