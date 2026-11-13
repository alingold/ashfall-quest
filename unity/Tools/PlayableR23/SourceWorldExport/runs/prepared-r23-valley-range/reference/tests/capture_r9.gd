extends SceneTree
var game:Node
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(title:String):
	for i in 5:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+title+".png");print("CAPTURE_R9 ",title)
func enter(id:String):
	game.front_menu.selected_map=id
	if game.front_end_active:game._start_from_title()
	else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
	for i in 5:await physics_frame;await process_frame
	freeze(game);game.combat_hud.hide();game.ui_layer.hide();game.vr_label.hide();game.vr_cue.hide();game.weapon.hide();game.body_visual.hide()
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var front:Node=game.front_menu;front.page="loadout";front.gallery.show_item(true,6);game.player.xr_enabled=true;front.set_active(true);front.desktop.hide();front.board.show();front.refresh_page()
	var camera:=Camera3D.new();game.add_child(camera);camera.current=true;camera.far=6000;camera.fov=70;camera.global_position=front.lobby.global_position+Vector3(-.1,1.8,4.0);camera.look_at(front.lobby.global_position+Vector3(.7,1.25,-.4));await shot("R9-01-WORLD-LOCKED-SKIN-SELECTOR")
	front.lobby._process(8);await shot("R9-02-SLOW-ROTATION")
	game.player.xr_enabled=false;front.set_active(false)
	await enter("archive:melipop-gods-poplightning");var map:Node=game.active_sandbox_map;var play:Node=map.gameplay_r8;camera.current=true
	var center:Vector3=map.global_position+Vector3(-34.2385,216.251,56.477);camera.global_position=center+Vector3(35,3,36);camera.look_at(center);map.get_node("AuthoredWorldScoreR9")._process(1);await shot("R9-03-MELIPOP-SOURCE-MEGATRON")
	if not play._weapon_boards_r9.is_empty():
		var board:Dictionary=play._weapon_boards_r9[0];center=map.global_position+board.box.get_center();center.y=float(board.floor_hint)
		camera.global_position=center+board.normal*10+Vector3.UP*2.5;camera.look_at(center+board.normal*2+Vector3.UP*2)
		for index in play.display_ids_r9:game.weapon_inventory._populate_art_r8(game.weapon_inventory.pickups[index],index)
		await shot("R9-04-MELIPOP-WEAPON-BOARD")
	await enter("archive:phoenix-bigbox-vr");map=game.active_sandbox_map;play=map.gameplay_r8;camera.current=true
	var pad:Vector3=play.pads[0];camera.global_position=pad+Vector3(.01,7,.01);camera.look_at(pad,Vector3.FORWARD);await shot("R9-05-PHOENIX-ORANGE-PAD")
	await enter("archive:poppy-gods-poplightning");map=game.active_sandbox_map;camera.current=true;camera.global_position=map.warmup_spawn_r8+Vector3(0,2,1);camera.look_at(map.spawn+Vector3.UP*1.6);await shot("R9-07-POPPY-SEPARATE-WARMUP")
	await enter("archive:vortex-sd-stumpdawg");map=game.active_sandbox_map;camera.current=true;var captured_field=false
	for mesh in map._geometry.find_children("*","MeshInstance3D",true,false):
		if not mesh.material_override is ShaderMaterial or not mesh.material_override.shader.resource_path.ends_with("force_field_r9.gdshader"):continue
		var parts:Dictionary=preload("res://scripts/archive_semantics_r8.gd")._components(mesh.mesh)
		for index in parts.rows:
			var box:AABB=parts.rows[index].box
			if box.size.y<2 or box.size.y>30 or maxf(box.size.x,box.size.z)<3:continue
			var row:Dictionary=preload("res://scripts/archive_semantics_r9.gd")._extract(mesh.mesh.surface_get_arrays(0),parts,index,mesh.material_override);var normal:Vector3=preload("res://scripts/archive_semantics_r9.gd")._surface_normal(row.faces)
			if absf(normal.y)>.2:continue
			center=mesh.to_global(box.get_center());camera.global_position=center+normal*8;camera.look_at(center);await shot("R9-06-TRANSLUCENT-FORCEFIELD");captured_field=true;break
		if captured_field:break
	await enter("archive:classic-bigbox-vr");map=game.active_sandbox_map;play=map.gameplay_r8;camera.current=true
	for kind in ["DOOR","WINDOW-ASSEMBLY"]:
		var panel:Node=null
		for group in play._panels_r8.values():
			for candidate in group:
				var row:Dictionary=map._details_r8.panels[int(candidate.key)]
				if (kind=="DOOR" and not row.glass and row.faces.size()==30) or (kind=="WINDOW-ASSEMBLY" and row.glass and not row.get("attachments",[]).is_empty()):panel=candidate;break
			if panel!=null:break
		if panel!=null:
			var faces:PackedVector3Array=map._details_r8.panels[int(panel.key)].faces;var normal:Vector3=preload("res://scripts/archive_semantics_r9.gd")._surface_normal(faces);center=map.global_position+panel.box.get_center();camera.global_position=center+normal*2.7;camera.look_at(center);await shot("R9-08-"+kind+"-INTACT");panel.take_damage(1);await shot("R9-09-"+kind+"-BROKEN")
	camera.current=false;var view:Camera3D=game.player.desktop_camera;view.current=true;view.global_transform=Transform3D(Basis.IDENTITY,map.spawn+Vector3.UP*1.65);var paddles:Node=game.revive_paddles;paddles.active=true;paddles.charge=.5;game.player.input_enabled=true;paddles._process(0)
	var wall:=MeshInstance3D.new();wall.mesh=BoxMesh.new();wall.mesh.size=Vector3(5,5,.1);var mat:=StandardMaterial3D.new();mat.albedo_color=Color("59646a");wall.material_override=mat;game.add_child(wall);wall.global_position=view.global_position+Vector3(0,0,-.35);await shot("R9-10-REVIVE-HUD-THROUGH-WALL")
	game.queue_free();await process_frame;quit()
