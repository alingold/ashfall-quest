extends Node3D
const Maps=preload("res://scripts/map_catalog1000.gd")
var game:Node
var session:Node
var opened =false
var _trigger =false
var viewport:SubViewport
var board:MeshInstance3D
var canvas:Control
var roster:Label
var caption:Label
var preview:TextureRect
var drawer:Control
var desktop:CanvasLayer
var _last_map =""
var top_image:Texture2D
var player_list_r5:Control
var display_r5:SubViewportContainer
var _desktop_hold_r5=false
var _surface_r5=preload("res://scripts/ui_surface_input_r5.gd").new()
func setup(g:Node,net:Node)->void:
	game=g;session=net;name="TacticalMap1000";process_priority=125;process_physics_priority=80
	viewport=SubViewport.new();viewport.size=Vector2i(1400,850);viewport.transparent_bg=true;add_child(viewport)
	canvas=Control.new();canvas.set_script(preload("res://scripts/tactical_map_draw1000.gd"));canvas.map=self;canvas.size=Vector2(1400,850);viewport.add_child(canvas)
	roster=Label.new();roster.position=Vector2(38,105);roster.size=Vector2(245,680);roster.add_theme_font_size_override("font_size",23);canvas.add_child(roster)
	caption=Label.new();caption.position=Vector2(38,30);caption.size=Vector2(925,48);caption.add_theme_font_size_override("font_size",27);caption.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;canvas.add_child(caption)
	preview=TextureRect.new();preview.position=Vector2(1000,245);preview.size=Vector2(355,235);preview.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;preview.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_COVERED;canvas.add_child(preview)
	board=MeshInstance3D.new();var quad =QuadMesh.new();quad.size=Vector2(1.65,1.0);board.mesh=quad
	var mat =StandardMaterial3D.new();mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.albedo_texture=viewport.get_texture();mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;mat.no_depth_test=true;mat.render_priority=110;board.material_override=mat;board.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;add_child(board)
	desktop=CanvasLayer.new();desktop.layer=180;add_child(desktop)
	display_r5=SubViewportContainer.new();display_r5.size=Vector2(1400,850);desktop.add_child(display_r5);viewport.reparent(display_r5)
	player_list_r5=preload("res://scripts/player_roster_r5.gd").new();canvas.add_child(player_list_r5);player_list_r5.position=Vector2(30,150);player_list_r5.setup(game)
	set_open(false)
func map_id()->String:
	if session.active:return str(session.rules.map)
	return str(game.front_menu.selected_map) if is_instance_valid(game.front_menu) else "valley"
func set_open(value:bool)->void:
	opened=value;board.visible=value and game.player.xr_enabled;desktop.visible=value and not game.player.xr_enabled;viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS if value else SubViewport.UPDATE_DISABLED
	game.player.set_meta("tactical_map1000",value)
	if not game.player.xr_enabled:Input.mouse_mode=Input.MOUSE_MODE_VISIBLE if value else Input.MOUSE_MODE_CAPTURED
	if not value:_surface_r5.step(viewport,board,Transform3D.IDENTITY,false,false,[])
	if not value:top_image=null;_last_map=""
func _unhandled_input(event:InputEvent)->void:
	if event is InputEventKey and not event.echo and event.physical_keycode==KEY_M:
		_desktop_hold_r5=event.pressed;consume_trigger(event.pressed,map_eligible());get_viewport().set_input_as_handled()
func map_eligible()->bool:
	if game.front_end_active or game.field_menu.is_open:return false
	return game.weapon.is_holstered or game.weapon.catalog_index(game.weapon.selected) not in [11,12]
func consume_trigger(pressed:bool,eligible:bool)->void:
	if opened!=(pressed and eligible):set_open(pressed and eligible)
	_trigger=pressed
func _physics_process(_delta:float)->void:
	var pressed =_desktop_hold_r5 if not game.player.xr_enabled else false
	var hand:Node=game.player.get_support_hand()
	if game.player.xr_enabled and is_instance_valid(hand) and game.player.is_hand_input_ready(hand):pressed=hand.get_float("trigger")>.65 or hand.is_button_pressed("trigger_click")
	consume_trigger(pressed,map_eligible())
func _process(_delta:float)->void:
	if not map_eligible():
		if opened:set_open(false)
	if not opened:return
	var camera:Camera3D=game.player.get_view_camera();var facing =preload("res://scripts/upright_ui.gd").view_basis(camera.global_basis)
	board.global_transform=Transform3D(facing,camera.global_position+facing*Vector3(0,0,-1.7))
	var id =map_id()
	if id!=_last_map:
		_last_map=id;var row =Maps.info(id);caption.text=row.title;preview.texture=Maps.image(id)
		var top_path="res://assets/maps1000/%s_top.png"%id;top_image=load(top_path) if ResourceLoader.exists(top_path) else null
	roster.text="PLAYERS" if session.active else "YOU"
	var current:Node=game.active_sandbox_map
	if is_instance_valid(current) and "tactical_texture_r8" in current and current.tactical_texture_r8!=null:top_image=current.tactical_texture_r8
	player_list_r5.visible=session.active
	if session.active:player_list_r5.refresh()
	if not game.player.xr_enabled:
		var screen:Vector2=get_viewport().get_visible_rect().size;var fit:float=minf(screen.x/1400,screen.y/850);display_r5.scale=Vector2.ONE*fit;display_r5.position=(screen-Vector2(1400,850)*fit)*.5
	else:
		var hand:XRController3D=game.player.get_weapon_hand();var pressed:bool=hand.get_float("trigger")>.65 or hand.is_button_pressed("trigger_click")
		_surface_r5.step(viewport,board,preload("res://scripts/weapon.gd").corrected_grip_transform(hand.global_transform),pressed,game.player.is_hand_input_ready(hand),player_list_r5.regions())
	canvas.queue_redraw()

func ping_rows_r19()->Array:
	var result:Array=[];var marks:Node=game.building_input.team_marks if is_instance_valid(game.building_input) else null
	var local_added=false
	if is_instance_valid(marks):
		for row in marks.records:
			if Time.get_ticks_msec()-int(row.created_ms)>float(row.ttl_seconds)*1000:continue
			if str(row.get("map_r19",map_id()))!=map_id():continue
			if session.active and int(row.get("epoch_r19",session.epoch))!=session.epoch:continue
			var point=Vector3(row.position[0],row.position[1],row.position[2]);var index:int=marks.ACTIONS.find(row.action)
			result.append({"point":point,"owner_point":game.player.global_position,"index":index,"catalog":int(row.get("catalog_index",-1))});local_added=true
	if session.active:
		for owner in session.marks:
			if owner==session.me() and local_added:continue
			if not session.allied(session.me(),owner):continue
			var row:Dictionary=session.marks[owner]
			if float(row.until)<session.now():continue
			if owner!=session.me() and not session.poses.has(owner):continue
			result.append({"point":row.point,"owner_point":game.player.global_position if owner==session.me() else session.poses[owner].root.origin,"index":int(row.color),"catalog":int(row.catalog)})
	return result
