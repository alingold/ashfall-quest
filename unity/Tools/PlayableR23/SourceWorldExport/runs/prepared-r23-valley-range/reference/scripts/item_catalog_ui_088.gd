extends Node3D
## Shared desktop/VR4x8 drop-items board:3pinned rows plus5scrolling catalog rows.
const SIZE:=Vector2i(960,1152)
const BOARD_SIZE:=Vector2(1.2,1.44)
var menu:Node
var service:Node
var viewport:SubViewport
var desktop:CanvasLayer
var container:SubViewportContainer
var board:MeshInstance3D
var scroll:ScrollContainer
var pinned:GridContainer
var catalog:GridContainer
var pinned_buttons:Array[Button]=[]
var catalog_buttons:Array[Button]=[]
var entries:Array[Dictionary]=[]
var status:Label
var rarity_row_r6:HBoxContainer
var rarity_buttons_r6:Array[Button]=[]
var close_button:Button
var divider_label:Label
var display_size:=Vector2(SIZE)
var display_board:=BOARD_SIZE
var pointer:MeshInstance3D
var active:=false
var _armed:=false
var _trigger:=false
var _grip:=false
var _close:=false
var _hovered:=-1
var _last_anchor:=Vector3.ZERO
func setup(owner_menu:Node,provider:Node)->void:
	menu=owner_menu;service=provider
	desktop=CanvasLayer.new();desktop.layer=80;add_child(desktop)
	container=SubViewportContainer.new();container.size=Vector2(SIZE);desktop.add_child(container)
	viewport=SubViewport.new();viewport.size=SIZE;viewport.transparent_bg=true;viewport.handle_input_locally=true;container.add_child(viewport)
	var background:=PanelContainer.new();background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);viewport.add_child(background)
	var outer:=StyleBoxFlat.new();outer.bg_color=Color("4a5259");outer.set_border_width_all(0);outer.set_content_margin_all(16);background.add_theme_stylebox_override("panel",outer)
	var column:=VBoxContainer.new();column.add_theme_constant_override("separation",8);background.add_child(column)
	var header:=HBoxContainer.new();column.add_child(header)
	var title:=Label.new();title.text="BACKPACK";title.add_theme_font_size_override("font_size",32);title.add_theme_color_override("font_color",Color("40e8ef"));title.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;title.size_flags_horizontal=Control.SIZE_EXPAND_FILL;header.add_child(title)
	var close:=Button.new();close.text="X";close.custom_minimum_size=Vector2(48,48);close.add_theme_color_override("font_color",Color("ff423c"));close.add_theme_font_size_override("font_size",30);close.pressed.connect(func():menu.set_open(false));header.add_child(close)
	close_button=close
	rarity_row_r6=HBoxContainer.new();rarity_row_r6.add_theme_constant_override("separation",8);column.add_child(rarity_row_r6)
	for stars in range(1,6):
		var button:=Button.new();button.text=preload("res://scripts/weapon_rarity_r6.gd").stars_text(stars);button.custom_minimum_size=Vector2(176,42);button.modulate=preload("res://scripts/weapon_rarity_r6.gd").color(stars);button.pressed.connect(_select_rarity_r6.bind(stars));rarity_row_r6.add_child(button);rarity_buttons_r6.append(button)
	pinned=GridContainer.new();pinned.columns=4;pinned.add_theme_constant_override("h_separation",8);pinned.add_theme_constant_override("v_separation",8);column.add_child(pinned)
	var divider:=Label.new();divider_label=divider;divider.text="ALL ITEMS  /  TEST CATALOG" if service.testing else "CARRIED INVENTORY";divider.add_theme_font_size_override("font_size",20);divider.modulate=Color("dfe6ea");column.add_child(divider)
	scroll=ScrollContainer.new();scroll.custom_minimum_size=Vector2(0,612);scroll.horizontal_scroll_mode=ScrollContainer.SCROLL_MODE_DISABLED;column.add_child(scroll)
	catalog=GridContainer.new();catalog.columns=4;catalog.size_flags_horizontal=Control.SIZE_EXPAND_FILL;catalog.add_theme_constant_override("h_separation",8);catalog.add_theme_constant_override("v_separation",8);scroll.add_child(catalog)
	status=Label.new();status.text="GRIP / CLICK TO EQUIP  |  TRIGGER / RIGHT CLICK TO DROP  |  STICK / WHEEL TO SCROLL";status.add_theme_font_size_override("font_size",16);status.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;status.modulate=Color("aab8b7");column.add_child(status)
	board=MeshInstance3D.new();board.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;var quad:=QuadMesh.new();quad.size=BOARD_SIZE;board.mesh=quad
	var mat:=StandardMaterial3D.new();mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.albedo_texture=viewport.get_texture();mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;mat.cull_mode=BaseMaterial3D.CULL_DISABLED;mat.no_depth_test=true;mat.render_priority=110;board.material_override=mat;add_child(board)
	pointer=MeshInstance3D.new();pointer.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;var dot:=SphereMesh.new();dot.radius=.004;dot.height=.008;pointer.mesh=dot;var white:=StandardMaterial3D.new();white.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;white.albedo_color=Color.WHITE;pointer.material_override=white;add_child(pointer)
	white.no_depth_test=true;white.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;white.render_priority=127
	service.changed.connect(refresh);refresh();set_active(false)
func _style(selected:bool)->StyleBoxFlat:
	# R23: menu-family style. Tiles keep their dark fill; no yellow outlines. Selection uses the
	# pause/settings cyan underline and blue selected fill.
	var style:=StyleBoxFlat.new();style.bg_color=Color("225d76") if selected else Color("101e26");style.border_color=Color("40e8ef");style.set_border_width_all(0);style.border_width_bottom=3 if selected else 0;style.set_content_margin_all(7);return style
func _button(index:int,entry:Dictionary)->Button:
	var button:=Button.new();button.custom_minimum_size=Vector2(220,116);button.size_flags_horizontal=Control.SIZE_EXPAND_FILL
	button.add_theme_stylebox_override("normal",_style(false));button.add_theme_stylebox_override("hover",_style(true));button.add_theme_stylebox_override("focus",_style(true));button.add_theme_stylebox_override("pressed",_style(true));button.add_theme_stylebox_override("disabled",_style(false))
	button.disabled=not bool(entry.get("active",true));button.tooltip_text=str(entry.title);button.pressed.connect(_activate.bind(index,false))
	button.gui_input.connect(func(event:InputEvent):
		if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_RIGHT and event.pressed:_activate(index,true))
	var content:=VBoxContainer.new();content.mouse_filter=Control.MOUSE_FILTER_IGNORE;content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);content.offset_left=6;content.offset_right=-6;content.offset_top=5;content.offset_bottom=-4;button.add_child(content)
	var picture:=TextureRect.new();picture.mouse_filter=Control.MOUSE_FILTER_IGNORE;picture.texture=entry.get("icon");picture.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;picture.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;picture.custom_minimum_size=Vector2(0,75);content.add_child(picture)
	var caption:=Label.new();caption.mouse_filter=Control.MOUSE_FILTER_IGNORE;caption.text=str(entry.title);caption.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;caption.add_theme_font_size_override("font_size",16);caption.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;content.add_child(caption)
	var tint:Color=preload("res://scripts/weapon_rarity_r6.gd").color(int(entry.get("rarity",1))) if entry.get("rarity_tinted",false) else Color.WHITE
	picture.modulate=tint;caption.modulate=tint
	if entry.has("count") and (entry.kind!="slot" or int(entry.count)>1):
		var count:=Label.new();count.mouse_filter=Control.MOUSE_FILTER_IGNORE;count.text="∞" if entry.get("infinite",false) else "x%d"%int(entry.count);count.position=Vector2(176,46) if entry.kind=="slot" else Vector2(9,75);count.add_theme_font_size_override("font_size",26);count.add_theme_color_override("font_shadow_color",Color.BLACK);count.add_theme_constant_override("shadow_offset_x",2);count.add_theme_constant_override("shadow_offset_y",2);button.add_child(count)
	if entry.kind=="slot":
		var number:=Label.new();number.mouse_filter=Control.MOUSE_FILTER_IGNORE;number.text=str(int(entry.slot)+1);number.position=Vector2(8,6);number.modulate=Color("aab8b7");button.add_child(number)
	return button
func refresh()->void:
	if not is_instance_valid(service):return
	_hovered=-1 # Old buttons are rebuilt; retain held edges so refresh cannot repeat a drop.
	var previous_scroll:=scroll.scroll_vertical if scroll!=null else 0
	entries=service.cards()
	var full:bool=service.testing
	display_size=Vector2(SIZE) if full else Vector2(960,512)
	display_board=BOARD_SIZE if full else Vector2(1.2,.64)
	viewport.size=Vector2i(display_size);container.size=display_size
	if board!=null:board.mesh.size=display_board
	scroll.visible=full;divider_label.visible=full;rarity_row_r6.visible=full
	for i in rarity_buttons_r6.size():rarity_buttons_r6[i].button_pressed=i+1==service.selected_rarity_r6;rarity_buttons_r6[i].text=("● " if i+1==service.selected_rarity_r6 else "")+preload("res://scripts/weapon_rarity_r6.gd").stars_text(i+1)
	scroll.custom_minimum_size.y=564 if full else 0
	status.text="GRIP / CLICK EQUIPS  |  TRIGGER / RIGHT CLICK DROPS"+ ("  |  SCROLL CATALOG" if full else "")
	for group in [pinned,catalog]:
		for child in group.get_children():group.remove_child(child);child.queue_free()
	pinned_buttons.clear();catalog_buttons.clear()
	for index in entries.size():
		var button:=_button(index,entries[index])
		if index<12:pinned.add_child(button);pinned_buttons.append(button)
		else:catalog.add_child(button);catalog_buttons.append(button)
	scroll.set_deferred("scroll_vertical",previous_scroll)
func _activate(index:int,drop:bool)->void:
	if index<0 or index>=entries.size():return
	var entry:Dictionary=entries[index].duplicate()
	# Existing equipment actions require resumed input; close before dispatch.
	if (entry.kind in ["slot","weapon","consumable"] and not drop) or entry.kind=="utility":menu.set_open(false)
	var ok:bool=service.activate(entry,drop)
	if not ok:status.text="UNAVAILABLE  /  COMPLETE THE CURRENT INTERACTION FIRST";return
	if (entry.kind in ["slot","weapon","consumable"] and not drop) or entry.kind=="utility":menu.set_open(false)
	else:status.text=("DROPPED " if entry.kind in ["slot","reserve"] else "PLACED ")+str(entry.title)
func set_active(value:bool)->void:
	if value and not active:
		_armed=false;_trigger=false;_grip=false;refresh();_last_anchor=menu.player.get_locomotion_anchor_position()
	active=value
	if viewport==null:return
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS if value else SubViewport.UPDATE_DISABLED
	desktop.visible=value and not menu.player.xr_enabled;board.visible=value and menu.player.xr_enabled;pointer.hide()
	if not value:_mouse_release()
func _mouse_release()->void:
	_trigger=false;_grip=false;_close=false
	_armed=false
func _select_rarity_r6(stars:int)->void:
	service.selected_rarity_r6=clampi(stars,1,5);refresh()
func point_index(position:Vector2)->int:
	if service.testing:
		for i in rarity_buttons_r6.size():
			if rarity_buttons_r6[i].get_global_rect().has_point(position):return -3-i
	if close_button.get_global_rect().has_point(position):return -2
	for i in pinned_buttons.size():
		if not pinned_buttons[i].disabled and pinned_buttons[i].get_global_rect().has_point(position):return i
	if scroll.get_global_rect().has_point(position):
		for i in catalog_buttons.size():
			if not catalog_buttons[i].disabled and catalog_buttons[i].get_global_rect().has_point(position):return i+12
	return -1
func point_input(position:Vector2,trigger:bool,grip:bool)->void:
	var index:=point_index(position)
	if index!=_hovered:
		if _hovered>=0:
			var old:Button=pinned_buttons[_hovered] if _hovered<12 else catalog_buttons[_hovered-12]
			old.add_theme_stylebox_override("normal",_style(false))
		if index>=0:
			var button:Button=pinned_buttons[index] if index<12 else catalog_buttons[index-12]
			button.add_theme_stylebox_override("normal",_style(true))
		_hovered=index
	var activate:=grip and not _grip;var drop:=trigger and not _trigger and not activate
	_trigger=trigger;_grip=grip
	if index<=-3 and (activate or drop):_select_rarity_r6(-index-2)
	elif index==-2 and (activate or drop):menu.set_open(false)
	elif index>=0 and (activate or drop):_activate(index,drop)
func _process(delta:float)->void:
	var wanted:bool=is_instance_valid(menu) and menu.is_open and menu.settings_page=="loadout"
	if wanted!=active:set_active(wanted)
	if not active:return
	menu._panel.hide();menu._desktop_layer.hide()
	if not menu.player.xr_enabled:
		var size:Vector2=get_viewport().get_visible_rect().size;var scale_factor:=minf((size.y-32)/display_size.y,(size.x-32)/display_size.x)
		container.scale=Vector2.ONE*scale_factor;container.position=(size-display_size*scale_factor)*.5;return
	var anchor:Vector3=menu.player.get_locomotion_anchor_position()
	menu._panel.global_position+=anchor-_last_anchor;_last_anchor=anchor
	board.global_transform=menu._panel.global_transform;pointer.hide()
	var hand:XRController3D=menu.player.get_weapon_hand()
	if not menu.player.tracking_available or not menu.player.is_hand_input_ready(hand) or menu.player.head_obstructed:_armed=false;_mouse_release();return
	var trigger:bool=hand.get_float("trigger")>.65 or hand.is_button_pressed("trigger_click")
	var grip:bool=hand.get_float("grip")>.65 or hand.is_button_pressed("grip_click")
	var close:bool=hand.is_button_pressed("ax_button")
	if not _armed:
		if not trigger and not grip and not close:_armed=true
		return
	if close and not _close:menu.set_open(false);return
	_close=close
	var pose:Transform3D=preload("res://scripts/weapon.gd").corrected_grip_transform(hand.global_transform)
	var start:=board.to_local(pose.origin);var direction:Vector3=board.global_basis.inverse()*(-pose.basis.z)
	if absf(direction.z)<.00001:_mouse_release();return
	var distance:float=-start.z/direction.z
	var hit:=start+direction*distance
	if distance<0 or absf(hit.x)>display_board.x*.5 or absf(hit.y)>display_board.y*.5:_mouse_release();return
	var position:=Vector2(hit.x/display_board.x+.5,.5-hit.y/display_board.y)*display_size
	pointer.global_position=board.to_global(hit+Vector3(0,0,.008));pointer.show()
	point_input(position,trigger,grip)
	var axis:float=menu.player.right_hand.get_vector2("primary").y
	if absf(axis)>.35:scroll.scroll_vertical+=roundi(-axis*700*delta)
