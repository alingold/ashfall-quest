extends SceneTree
var game:Node
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(name:String):
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(output+"/"+name+".png");print("UI_CAPTURE_R14 ",name)
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game);game.hide()
	for layer in game.find_children("*","CanvasLayer",true,false):layer.hide()
	var stage:=Node3D.new();root.add_child(stage)
	var environment:=WorldEnvironment.new();var env:=Environment.new();env.background_mode=Environment.BG_COLOR;env.background_color=Color("354b58");environment.environment=env;stage.add_child(environment)
	var camera:=Camera3D.new();stage.add_child(camera);camera.position=Vector3(0,1.5,2.5);camera.fov=55;camera.look_at(Vector3(0,1.5,0));camera.current=true
	var wall:=MeshInstance3D.new();var shape:=BoxMesh.new();shape.size=Vector3(3,2.8,.1);wall.mesh=shape;var finish:=StandardMaterial3D.new();finish.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;finish.albedo_color=Color("697981");wall.material_override=finish;wall.position=Vector3(0,1.5,.35);stage.add_child(wall)
	# Actual spatial menu surfaces and pointer meshes, behind an opaque world wall.
	var menu:Node=game.field_menu;menu.is_open=true;menu._refresh();menu._panel.reparent(stage,false);menu._panel.position=Vector3(0,1.5,0);menu._panel.show()
	for layer in game.find_children("*","CanvasLayer",true,false):layer.hide()
	for i in 2:
		var line:Node3D=menu._pointer_lines[i];var dot:Node3D=menu._pointer_dots[i];line.reparent(stage,false);dot.reparent(stage,false)
		var target:=Vector3(-.22 if i==0 else .22,1.5,.02);var origin:=Vector3(-.58 if i==0 else .58,.9,.15);line.global_position=(origin+target)*.5;line.look_at(target);line.scale.z=origin.distance_to(target);line.show();dot.global_position=target;dot.show()
	await shot("R14-UI-01-SETTINGS-AND-BOTH-LASERS-THROUGH-WALL")
	menu._panel.hide()
	for part in menu._pointer_lines+menu._pointer_dots:part.hide()
	wall.hide()
	var rack:Node=game.weapon;var inventory:Node=game.weapon_inventory;inventory.capacity=6;var owned:Array[int]=[]
	for stars in range(1,6):owned.append(rack.create_instance([0,2,3,7,9][stars-1],stars))
	owned.append(rack.create_instance(14,1));inventory.owned.assign(owned)
	var wheel:Node3D=rack.wheel;wheel.set_owned_indices(owned);wheel.reparent(stage,false);wheel.transform=Transform3D(Basis.IDENTITY,Vector3(0,1.5,0));wheel.show();wheel._set_highlight(wheel.SLOT_ACTIONS.find("weapon_%d"%owned[4]));camera.position.z=1.4
	await shot("R14-UI-02-FIVE-RARITIES-AND-FIXED-BLADE")
	wheel.hide();camera.position.z=2.5
	var service:Node=game.item_catalog_088;var catalog:Node=menu.catalog_ui;service.testing=false;catalog.refresh();catalog.viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS;catalog.board.reparent(stage,false);catalog.board.transform=Transform3D(Basis.IDENTITY,Vector3(0,1.5,0));catalog.board.show();wall.show()
	await shot("R14-UI-03-BACKPACK-RARITY-COLORS-THROUGH-WALL")
	catalog.board.hide();wall.hide()
	game.front_end_active=false;game.front_menu.selected_map="range";rack.is_holstered=true;menu.is_open=false
	var map:Node=game.network_bridge.tactical;map.set_open(true);map._process(0)
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw
	map.viewport.get_texture().get_image().save_png(output+"/R14-UI-04-CLEAN-TACTICAL-MAP.png");print("UI_CAPTURE_R14 CLEAN-TACTICAL-MAP")
	game._quitting=true;game.queue_free();stage.queue_free();await process_frame;quit()
