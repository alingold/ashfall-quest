extends SceneTree
var game:Node
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(title:String):
	for i in 6:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+title+".png");print("CAPTURE_R8 ",title)
func enter(id:String):
	game.front_menu.selected_map=id
	if game.front_end_active:game._start_from_title()
	else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
	for i in 5:await physics_frame;await process_frame
	freeze(game);game.combat_hud.hide();game.ui_layer.hide();game.vr_label.hide();game.vr_cue.hide();game.weapon.hide();game.body_visual.hide()
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	game.front_menu.page="lobby";game.front_menu.refresh_page();await shot("R8-01-MENU-SKIN-BUTTON")
	game.front_menu.page="loadout";game.front_menu.gallery.show_item(true,6);game.front_menu.refresh_page();await shot("R8-02-CLAUDE-SKIN-SELECTOR")
	await enter("archive:battlefield-paintball-gods-pitdevil")
	var map:Node=game.active_sandbox_map;var play:Node=map.gameplay_r8;var camera:Camera3D=game.player.get_view_camera();camera.global_position=map.warmup_spawn_r8+Vector3.UP*1.65
	play._physics_process(.21);var board:Node3D=play.boards[0].get_parent();camera.look_at(board.global_position);await shot("R8-03-PAINTBALL-WARMUP-SCOREBOARD")
	var site:int=play.cache_ids.keys()[0];var at:Vector3=play.sites[site];camera.global_position=at+Vector3(3.2,1.2,2.8);camera.look_at(at)
	for index in play.cache_ids[site]:
		var prop:Node3D=game.weapon_inventory.pickups[index]
		if prop.get_child_count()==0:game.weapon_inventory._populate_art_r8(prop,index)
	await shot("R8-04-PAINTBALL-SIX-WEAPON-CACHE")
	await enter("archive:classic-bigbox-vr")
	map=game.active_sandbox_map;play=map.gameplay_r8;camera=game.player.get_view_camera();var pad:Vector3=play.pads[0];camera.global_position=pad+Vector3(0,4,7);camera.look_at(pad);await shot("R8-05-CLASSIC-FUNCTIONAL-PAD")
	var panel:Node=null
	for group in play._panels_r8.values():
		if not group.is_empty():panel=group[0];break
	if panel!=null:
		var shape:CollisionShape3D=panel.get_child(1);var faces:PackedVector3Array=shape.shape.get_faces();var normal:Vector3=(faces[1]-faces[0]).cross(faces[2]-faces[0]).normalized();camera.global_position=panel.to_global(panel.box.get_center())+normal*2.8;camera.look_at(panel.to_global(panel.box.get_center()));await shot("R8-06-CLASSIC-PANE-INTACT");panel.take_damage(1);await shot("R8-07-CLASSIC-PANE-BROKEN")
	var tactical:Node=game.network_bridge.tactical;tactical._desktop_hold_r5=true;tactical.set_open(true);tactical._process(0);await shot("R8-08-CLASSIC-TACTICAL-MAP");tactical.set_open(false)
	game.player.global_position=map.spawn;camera.global_transform=Transform3D(Basis.IDENTITY,map.spawn+Vector3.UP*1.65)
	var net:Node=game.network_bridge.net;net.active=true;net.server=true;net.stage="match";net.match_started=net.now()-46;net.match_scores={0:9,1:11};net.rules.score_limit=40;net.players={1:{"team":0,"spectator":false,"name":"Local Player","life":"alive"},2:{"team":1,"spectator":false,"name":"Other Player","life":"alive"}}
	game.combat_hud.show();game.combat_hud.refresh(0);game.combat_hud.get_node("CompactMatchHUDR3")._process(0)
	var wall:=MeshInstance3D.new();wall.mesh=BoxMesh.new();wall.mesh.size=Vector3(4,4,.1);var mat:=StandardMaterial3D.new();mat.albedo_color=Color("59646a");wall.material_override=mat;game.add_child(wall);wall.global_position=camera.global_position+Vector3(0,0,-.35);await shot("R8-09-HUD-IN-FRONT-OF-WALL")
	net.active=false;game.queue_free();await process_frame;quit()
