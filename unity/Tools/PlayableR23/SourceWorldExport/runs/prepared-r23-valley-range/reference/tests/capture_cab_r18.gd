extends SceneTree
func _initialize():call_deferred("run")
func run():
 var game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
 while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
 game.player.set_physics_process(false)
 var camera:Camera3D=game.player.desktop_camera
 var center=game.active_sandbox_map.global_position+Vector3(-359.4039,36.3051,280.6427)
 camera.global_position=center+Vector3(7,4,8);camera.look_at(center)
 for i in 12:await process_frame
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/cab-r18.png")
 game._quitting=true;quit()
