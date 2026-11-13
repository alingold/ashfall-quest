extends SceneTree
var game:Node
var output:String
func _initialize():call_deferred("run")
func shot(name:String):
 for i in 4:await process_frame
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png(output+"/"+name+".png")
func run():
 output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);game.player.set_physics_process(false)
 var loader=load("res://scripts/map_loading_r18.gd").get_for(game);var token:int=loader.begin()
 await shot("R19-loading-map")
 loader.finish(token)
 game.front_menu.selected_map="archive:alfa-aglae-souza";game.front_menu._explore_from_r16("game")
 var deadline:int=Time.get_ticks_msec()+90000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not loader.active:break
 var map:Node=game.active_sandbox_map;assert(map!=null and map.map_ready)
 var eye:Camera3D=game.player.desktop_camera;game.player.global_position=map.spawn;eye.position=Vector3(0,game.player.get_standing_eye_height(),0)
 var at:Vector3=map._floor(map.spawn+Vector3(180,1000,-180),2000)
 if not at.is_finite():at=map.spawn+Vector3(180,0,-180)
 game.building_input.team_marks._create_mark(at,Vector3.UP,4)
 eye.look_at(at+Vector3.UP*.5)
 await shot("R19-world-target")
 var tactical=game.network_bridge.tactical;tactical._desktop_hold_r5=true;tactical.set_open(true)
 for i in 3:await process_frame
 await shot("R19-tactical-target-line")
 tactical.set_open(false);game._quitting=true;game.queue_free();for i in 6:await process_frame
 print("R19_NATIVE_UI_CAPTURE_PASS");quit()
