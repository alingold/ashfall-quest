extends Node
## Pause and Settings share the same dark cards, cyan accents and icon treatment.
var _styles:Dictionary={}

func setup(_menu:Node)->void:
	for selected in [false,true]:
		for state in ["normal","hover","focus","pressed"]:
			var style:=StyleBoxFlat.new()
			style.bg_color=Color("225d76") if selected else Color("252a30")
			style.border_color=Color("40e8ef") if selected or state!="normal" else Color("89939a")
			style.border_width_bottom=2
			style.content_margin_left=12;style.content_margin_right=12
			_styles[str(selected)+state]=style

static func icon_path(caption:String)->String:
	var index:=1
	if caption.begins_with("RESUME") or caption.contains("MATCH"):index=0
	elif caption.begins_with("INITIALIZE"):index=2
	elif caption.begins_with("HOME") or caption.begins_with("LEAVE"):index=3
	return "res://assets/ui/pause0993_%d.svg"%index

func refresh(menu:Node)->void:
	var pause:bool=menu.settings_page=="pause"
	var category:bool=menu.settings_page=="root" and not menu.audio_page
	var labels:Array=menu._labels()
	var board:MeshInstance3D=menu._panel.get_node("SettingsBoard")
	board.mesh.size=Vector2(1.3,1.48)
	menu._spatial_title.position.y=.61;menu._spatial_title.font_size=42
	menu._spatial_title.modulate=Color("40e8ef")
	if pause:menu._spatial_title.text="ASHFALL"
	elif category:menu._spatial_title.text="SETTINGS"
	menu._desktop_title.horizontal_alignment=HORIZONTAL_ALIGNMENT_LEFT
	menu._desktop_title.get_parent().custom_minimum_size.x=440
	menu._desktop_hint.visible=not pause
	if pause or category:menu._desktop_title.text=menu._spatial_title.text
	for i in menu._buttons.size():
		var card:bool=(pause and i<labels.size()) or (category and i<4)
		var button:Button=menu._buttons[i]
		button.custom_minimum_size.y=64 if card else 48
		for state in ["normal","hover","focus","pressed"]:
			if card:button.add_theme_stylebox_override(state,_styles[str(menu.selected==i)+state])
			else:button.remove_theme_stylebox_override(state)
		button.alignment=HORIZONTAL_ALIGNMENT_LEFT if card else HORIZONTAL_ALIGNMENT_CENTER
		if card:
			button.icon=load(icon_path(labels[i]) if pause else "res://assets/ui/settings_%d.svg"%i)
			button.expand_icon=true;button.add_theme_constant_override("icon_max_width",32)
