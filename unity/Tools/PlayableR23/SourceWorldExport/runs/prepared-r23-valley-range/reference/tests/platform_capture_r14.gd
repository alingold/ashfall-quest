extends SceneTree
var game:Node
var camera:Camera3D
func _initialize():call_deferred("run")
func shot(name:String,at:Vector3,target:Vector3):
	camera.global_position=at;camera.look_at(target)
	for i in 6:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/"+name+".png")
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game)
	var net:Node=game.network_bridge.net;assert(net.host("Native Platform",25425,"private")==OK)
	net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0})
	var deadline:int=Time.get_ticks_msec()+120000
	while not (net.stage=="lobby" and game.network_bridge.playing()):
		await process_frame
		if Time.get_ticks_msec()>deadline:quit(1);return
	net.request("start")
	while not (net.stage=="match" and game.network_bridge.playing()):
		await process_frame
		if Time.get_ticks_msec()>deadline:quit(1);return
	game.player.set_physics_process(false);game.network_bridge.set_process(false);game.set_process(false);game.body_visual.hide();game.combat_hud.hide();game.ui_layer.hide();game.vr_cue.hide();game.vr_label.hide();game.weapon.hide();game.field_menu.hide();game.front_menu.hide()
	camera=Camera3D.new();camera.current=true;camera.far=4000;camera.fov=68;game.add_child(camera)
	var frame:Transform3D=game.network_bridge.launch_r12.stations[0].frame
	await shot("R14-IN-GAME-PLATFORM-OVERVIEW",frame*Vector3(-42,29,-42),frame*Vector3(0,1,0))
	await shot("R14-IN-GAME-PLATFORM-DECK",frame*Vector3(-20,1.8,6),frame*Vector3(0,1.6,-6))
	game.network_bridge.leave_session();game._quitting=true;game.queue_free();await process_frame;quit()
