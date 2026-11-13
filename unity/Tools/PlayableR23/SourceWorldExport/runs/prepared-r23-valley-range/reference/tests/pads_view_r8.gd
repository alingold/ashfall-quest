extends SceneTree
func _initialize():call_deferred("run")
func run():
	var output:String=OS.get_cmdline_user_args()[0];var game:Node=load("res://scripts/main.gd").new();root.add_child(game)
	game.front_menu.selected_map="archive:classic-bigbox-vr";game._start_from_title()
	for i in 5:await physics_frame
	for node in game.find_children("*","Node",true,false):node.set_process(false);node.set_physics_process(false)
	game.set_process(false);game.combat_hud.hide();game.ui_layer.hide();game.weapon.hide();game.vr_label.hide();game.vr_cue.hide();game.body_visual.hide()
	var at:Vector3=game.active_sandbox_map.global_position+Vector3(-580.1268,55.9768,382.8787);var camera:Camera3D=game.player.get_view_camera();camera.global_position=at+Vector3(0,5,8);camera.look_at(at)
	for i in 6:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/classic-pad-candidate.png")
	var metadata:Dictionary=preload("res://scripts/archive_catalog1003.gd").manifest("archive:classic-bigbox-vr")
	for node in game.active_sandbox_map._geometry.find_children("*","MeshInstance3D",true,false):
		if node.material_override!=null and node.material_override.resource_name in ["s46","s48","s34","s35","s36"]:
			var image:Image=node.material_override.albedo_texture.get_image();if image.is_compressed():image.decompress()
			image.save_png(output+"/classic-"+node.material_override.resource_name+".png")
	game.queue_free();await process_frame;quit()
