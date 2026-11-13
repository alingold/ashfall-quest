extends SceneTree
var game:Node
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(title:String):
	for i in 10:await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(output+"/"+title+".png");print("CAPTURE_R7 ",title)
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var front:Node=game.front_menu;front.page="maps";front._map_page1003=0;front.refresh_page();await shot("01-MAP-SELECT-FIRST")
	front._map_page1003=1;front.refresh_page();await shot("02-MAP-SELECT-LIBRARY")
	front._map_page1003=26;front.refresh_page();await shot("03-MAP-SELECT-LAST")
	front._map_page1003=0;front.refresh_page();front.activate(front._map_slots1003[3]);await shot("04-ARCHIVE-SELECTED-READY")
	front.activate(0)
	for i in 5:await physics_frame
	freeze(game);game.combat_hud.hide();game.ui_layer.hide();game.vr_label.hide();game.vr_cue.hide();game.weapon.hide()
	var map:Node=game.active_sandbox_map;var camera:Camera3D=game.player.get_view_camera();camera.global_position=map.spawn+Vector3.UP*1.7;camera.look_at(camera.global_position+Vector3(0,-.1,-5));await shot("05-ARCHIVE-IN-GAME")
	var rack:Node=game.weapon;var index:int=rack.create_instance(17,5);var gun:Node=rack.weapons[index];gun.set_process(false);gun.set_physics_process(false);gun.show()
	gun.global_transform=Transform3D(camera.global_basis,camera.global_position+camera.global_basis*Vector3(.23,-.22,-.45));rack.show()
	var prop:Node=game.weapon_inventory._create_prop(index);prop.global_position=camera.global_position+camera.global_basis*Vector3(-.3,-.75,-1.5);prop.rotation.y=PI*.5;await shot("06-HELD-NORMAL-GROUND-RARITY")
	print("CAPTURE_R7 maps=",front._map_ids1003.size()," mounted_pack=",preload("res://scripts/archive_catalog1003.gd").mounted_pack_r7)
	game.queue_free();await process_frame;quit()
