extends SceneTree
var game:Node
var camera:Camera3D
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(title:String):
	for i in 6:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+title+".png");print("CAPTURE_R10 ",title)
func enter(id:String):
	game.front_menu.selected_map=id
	if game.front_end_active:game._start_from_title()
	else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
	for i in 5:await physics_frame;await process_frame
	freeze(game);game.combat_hud.hide();game.ui_layer.hide();game.vr_label.hide();game.vr_cue.hide();game.weapon.hide();game.body_visual.hide();camera.current=true
func city_views(city:Node):
	camera.global_position=city.global_position+Vector3(350,240,350);camera.look_at(city.global_position+Vector3.UP*35);await shot("R10-08-CYBER-BIRD-RETAINED")
	camera.global_position=city.global_position+Vector3(0,1.7088,55);camera.look_at(city.global_position+Vector3(0,21,0));city.update_lighting(camera.global_position);await shot("R10-09-CYBER-BIRD-STREET")
	var building:Dictionary=city.buildings.filter(func(row:Dictionary):return row.id==36)[0]
	camera.global_position=city.global_position+building.center+Vector3(-2,1.7088,0);camera.look_at(city.global_position+building.center+Vector3(building.stair_x,2,1));city.update_lighting(camera.global_position);await shot("R10-10-CYBER-BIRD-INTERIOR")
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game);camera=Camera3D.new();game.add_child(camera);camera.current=true;camera.far=6000;camera.fov=70
	if OS.get_cmdline_user_args().has("--cyber-only-r10"):
		await enter("cyber_bird");await city_views(game.active_sandbox_map);game._quitting=true;game.queue_free();await process_frame;quit();return
	await enter("archive:alfa-aglae-souza");var map:Node=game.active_sandbox_map
	var center:Vector3=map.global_position+Vector3(0,40,0);camera.global_position=center+Vector3(550,700,600);camera.look_at(center);await shot("R10-01-ALPHA-OVERVIEW")
	center=map.global_position+Vector3(-41.716,21.82875,11.37975);camera.global_position=center+Vector3(8,3,8);camera.look_at(center);await shot("R10-02-ALPHA-STREET-VAN")
	center=map.global_position+Vector3(-130.3305,22.1088,-41.49125);camera.global_position=center+Vector3(8,3,8);camera.look_at(center);await shot("R10-03-ALPHA-CONTAINER")
	var chosen:Node=null;var distance=INF
	for group in map.gameplay_r8._panels_r8.values():
		for panel in group:
			if not panel.glass_r10 or map._details_r8.panels[int(panel.key)].get("attachments",[]).is_empty():continue
			var d:float=panel.box.get_center().distance_to(Vector3(-41.716,21.82875,11.37975))
			if d<distance:chosen=panel;distance=d
	if chosen!=null:
		var row:Dictionary=map._details_r8.panels[int(chosen.key)];var normal:Vector3=preload("res://scripts/archive_semantics_r9.gd")._surface_normal(row.faces);center=map.global_position+chosen.box.get_center();camera.global_position=center+normal*2.7;camera.look_at(center);await shot("R10-04-TRANSPARENT-WINDOW-INTACT");chosen.take_damage(1);await shot("R10-05-WINDOW-AND-FRAME-REMOVED")
	await enter("archive:afterlife-gods-alamo-hank");map=game.active_sandbox_map;center=map.global_position+Vector3(map._meta.center[0],map._meta.center[1],map._meta.center[2]);camera.global_position=center+Vector3(240,340,280);camera.look_at(center);await shot("R10-06-AFTERLIFE-SOURCE-PALETTE")
	await enter("archive:classic-bigbox-vr");map=game.active_sandbox_map;var pad:Vector3=map.gameplay_r8.pads[0];camera.global_position=pad+Vector3(4,3,5);camera.look_at(pad);await shot("R10-07-FUNCTIONAL-HIGH-LAUNCH-PAD")
	await enter("cyber_bird");await city_views(game.active_sandbox_map)
	game._quitting=true;game.queue_free();await process_frame;quit()
