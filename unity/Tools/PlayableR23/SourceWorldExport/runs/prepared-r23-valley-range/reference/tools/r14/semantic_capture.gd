extends SceneTree
var game:Node
var camera:Camera3D
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(name:String,at:Vector3,offset:Vector3,target:Vector3):
	camera.global_position=at+offset;camera.look_at(at+target)
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+name+".png");print("SEMANTIC_CAPTURE ",name)
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	freeze(game);game.weapon.hide();game.body_visual.hide();game.ui_layer.hide();game.combat_hud.hide();camera=Camera3D.new();game.add_child(camera);camera.current=true;camera.fov=68;camera.far=3000
	var origin:=Vector3(0,0,4800)
	await shot("SEMANTIC-WHITE-RING",origin+Vector3(62.912,22.522,33.090),Vector3(0,2,-3),Vector3(0,.1,0))
	await shot("SEMANTIC-BLACK-RING",origin+Vector3(202.412,52.557,-16.859),Vector3(0,1.7,-3),Vector3(0,.9,0))
	await shot("SEMANTIC-GREEN-PAD",origin+Vector3(-526.7505,48.7341,125.6655),Vector3(0,2.6,-3.5),Vector3.ZERO)
	await shot("SEMANTIC-ORANGE-PAD",origin+Vector3(-507.8938,48.523,90.7413),Vector3(0,2.6,-3.5),Vector3.ZERO)
	await shot("SEMANTIC-TILTED-PAD",origin+Vector3(-543.4,39.3,-135.6),Vector3(0,2.0,-3.5),Vector3(0,.5,0))
	var semantic:Node=game.active_sandbox_map.find_child("MapSemanticsR14",true,false)
	if semantic!=null:
		semantic.set_markers_visible(false)
		await shot("SEMANTIC-HOST-NO-STATUE",origin+Vector3(202.412,52.557,-16.859),Vector3(0,1.7,-3),Vector3(0,.9,0))
		await shot("SEMANTIC-HOST-NO-WHITE-RING",origin+Vector3(62.912,22.522,33.090),Vector3(0,2,-3),Vector3(0,.1,0))
	game._quitting=true;game.queue_free();await process_frame;quit()
