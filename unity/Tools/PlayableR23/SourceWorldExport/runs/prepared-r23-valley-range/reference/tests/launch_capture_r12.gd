extends SceneTree
var game:Node
var output:String
var camera:Camera3D
var prefix=""
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(name:String,eye:Vector3,target:Vector3):
	camera.global_position=eye;camera.look_at(target)
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+prefix+name+".png");print("CAPTURE_R12 ",prefix+name)
func run():
	output=OS.get_cmdline_user_args()[0];var map_id="archive:alfa-aglae-souza"
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--capture-map="):map_id=arg.trim_prefix("--capture-map=")
		if arg.begins_with("--capture-prefix="):prefix=arg.trim_prefix("--capture-prefix=")
	game=load("res://scripts/main.gd").new();root.add_child(game)
	var net:Node=game.network_bridge.net;net.host("Pilot",25412,"r12");net.request("rules",{"map":map_id,"mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0})
	while net.stage!="lobby" or not net.world_ready or game.network_bridge._preparing:await process_frame
	net.request("start")
	while net.stage!="match" or game.network_bridge._preparing:await process_frame
	for i in 5:await physics_frame
	freeze(game);game.combat_hud.hide();game.ui_layer.hide();game.weapon.hide();game.body_visual.hide();camera=Camera3D.new();game.add_child(camera);camera.far=6000;camera.fov=74;camera.current=true
	var launch:Node=game.network_bridge.launch_r12;var frame:Transform3D=launch.stations[0].frame
	await shot("R12-01-SKY-PLATFORM",frame*Vector3(28,15,25),frame*Vector3(0,-1,-2))
	await shot("R12-02-DECK-AND-RED-PODS",frame*Vector3(-10,1.75,5),frame*Vector3(0,1.3,-6))
	await shot("R12-03-POD-CONTROL",frame*Vector3(-12.6,1.85,-4.9),frame*Vector3(-12.6,1.4,-6.8))
	var pod:Transform3D=launch.pod_pose(1);game.player.global_position=pod.origin+Vector3.UP*.12;net.poses[1].root.origin=game.player.global_position;net.request("pod_launch",{"pod":net.players[1].pod_key_r12});net.server_offset+=2;launch._refresh_docks();launch._refresh_flights()
	var ride:Dictionary=net.pods_r12[1];var at:Vector3=net.pod_position_r12(ride)
	await shot("R12-04-POD-IN-FLIGHT",at+frame.basis*Vector3(8,4,7),at+Vector3.UP*1.2)
	await shot("R12-05-EJECT-CONTROL",at+Vector3.UP*1.65,at+frame.basis*Vector3(0,1.45,-.9))
	game._quitting=true;game.network_bridge.leave_session();game.queue_free();await process_frame;quit()
