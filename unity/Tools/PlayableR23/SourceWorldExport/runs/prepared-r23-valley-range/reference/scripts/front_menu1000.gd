extends "res://scripts/front_menu_091.gd"
const Maps1000=preload("res://scripts/map_catalog1000.gd")
const Rules1000=preload("res://scripts/net_rules1000.gd")
var _built1000 =false
var session_overlay=false
var _overlay_holstered=false
var _rule_guides:Array[ColorRect]=[]
var _host_start_index=-1
var _rule_sliders_r5:Array[HSlider]=[]
var _presets_r5:Dictionary={}
var _roster_r5:Control
var _surface_r5=preload("res://scripts/ui_surface_input_r5.gd").new()
var _pending_r5:Dictionary={}
var _rule_clock_r5=0.0
var _actions1000:Dictionary={}
var _pages1000:Dictionary={}
var _map_views1000:Array=[]
var _net_label1000:Label
var _roster1000:Label
var _rule_labels1000:Array=[]
var _rule_buttons1000:Array=[]
var _team_buttons1000:Array=[]
var _kick_buttons1000:Array=[]
var _custom_backing1000:ColorRect
var _tabs1000:Array=[]
var _host_tab1000 =0
var _keyboard_field1000 ="name"
var _keyboard_label1000:Label
var fields1000 ={"name":"Player","address":"127.0.0.1","port":"24760","password":"","room":"ASHFALL FRIENDS"}
var _return_page1000 ="connect"
var _map_page1003=0
var _map_slots1003:Array=[]
var _map_ids1003:Array=[]
var _map_nav1003:Array=[]
var _skin_backing_r9:Panel
var exploration_spawn_r16="lobby"
const TABS1000:=["SESSION","COMBAT","MOVEMENT","EQUIPMENT","RESPAWN","ZONE","PLAYERS"]
const KEYS1000:=[
	["max_players","teams","mode","late_join","spectators","locked","time_limit","score_limit","rounds"],
	["damage","build_damage","headshots","friendly_fire","starting_health","starting_shield"],
	["walk","run","flight","fling","stamina"],
	["loadout","loot","catalog","builds","starting_builds","infinite_builds","infinite_ammo","retain_inventory"],
	["respawn_delay","protection","downed_seconds"],["zone_enabled","zone_damage","zone_speed","zone_wait"],[]]
func setup(player:Node3D)->void:
	super.setup(player)
	gallery.style=game.field_menu.character_style_r8
	preload("res://scripts/lobby_map_091.gd").apply_style(lobby.avatars[0].get_child(0),gallery.style)
	preload("res://scripts/gemini_player_models_099.gd").apply_style(game.body_visual,gallery.style)
	preload("res://scripts/gemini_player_models_099.gd").style_gloves(actor,gallery.style)
	buttons[3].size.x=180
	fields1000.name=OS.get_environment("USERNAME").left(24)
	if not OS.get_cmdline_user_args().has("--scope-playtest"):selected_map="cyber_bird"
	if OS.get_cmdline_user_args().has("--community-playtest") and preload("res://scripts/community_catalog1002.gd").installed():selected_map="community_local"
	# Existing gallery and READY keep their action IDs and release activation.
	buttons[11].position=Vector2(60,430);buttons[11].size=Vector2(350,76)
	buttons[12].position=Vector2(465,430);buttons[12].size=Vector2(350,76)
	_add1000("CYBER BIRD",Vector2(870,430),Vector2(350,76),["maps"],func():_select_map1000("cyber_bird"))
	var community_button=_add1000("LOCAL ARCHIVE",Vector2(680,600),Vector2(440,60),["maps"],func():_map_pick1003(3))
	var map_buttons=[11,12,13,community_button]
	_map_slots1003=map_buttons;_map_ids1003=Maps1000.ids()
	for i in 4:
		var slot=i;_actions1000[map_buttons[i]]=func():_map_pick1003(slot);_pages1000[map_buttons[i]]=["maps"]
		var at=Vector2(90+(i%2)*590,145+floori(float(i)/2)*270)
		buttons[map_buttons[i]].position=at+Vector2(0,180);buttons[map_buttons[i]].size=Vector2(440,60)
		var pic =TextureRect.new();pic.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;pic.position=at;pic.size=Vector2(440,170);pic.clip_contents=true;pic.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_COVERED;pic.mouse_filter=Control.MOUSE_FILTER_IGNORE;viewport.add_child(pic);_map_views1000.append(pic)
	_map_nav1003.append(_add1000("PREVIOUS",Vector2(390,710),Vector2(220,58),["maps"],func():_map_page1003=maxi(0,_map_page1003-1);_map_render1003()))
	_map_nav1003.append(_add1000("NEXT",Vector2(670,710),Vector2(220,58),["maps"],func():_map_page1003=mini(ceili(float(_map_ids1003.size())/4)-1,_map_page1003+1);_map_render1003()))
	_net_label1000=words("",Vector2(40,105),19,Color("7ceaf4"));_net_label1000.size=Vector2(1180,70);_net_label1000.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART
	_custom_backing1000=ColorRect.new();_custom_backing1000.size=Vector2(SIZE);_custom_backing1000.color=Color(.015,.04,.065,.97);_custom_backing1000.mouse_filter=Control.MOUSE_FILTER_IGNORE;viewport.add_child(_custom_backing1000);viewport.move_child(_custom_backing1000,0)
	_roster1000=words("",Vector2(42,220),21,Color.WHITE);_roster1000.size=Vector2(365,400)
	_roster1000.position.y=206;_roster1000.add_theme_constant_override("line_spacing",10)
	_add1000("HOST / JOIN",Vector2(970,580),Vector2(270,64),["lobby"],func():page="connect";refresh_page())
	_add1000("HOST CONTROLS",Vector2(970,655),Vector2(270,64),["lobby"],func():page="host";refresh_page())
	_add1000("LEAVE LOBBY",Vector2(970,730),Vector2(270,48),["lobby"],func():game.network_bridge.leave_session();refresh_page())
	_add1000("START MATCH",Vector2(820,584),Vector2(145,88),["lobby"],func():game.network_bridge.net.request("start"))
	_add1000("REMATCH",Vector2(465,584),Vector2(350,88),["lobby"],func():game.network_bridge.net.request("restart"))
	_add1000("RETURN TO LOBBY",Vector2(465,490),Vector2(350,58),["lobby"],func():game.network_bridge.net.request("home"))
	for i in ["name","address","port","password","room"].size():
		var field:String=["name","address","port","password","room"][i]
		_add1000(field.to_upper(),Vector2(180,195+i*74),Vector2(900,64),["connect"],func():_keyboard_field1000=field;_return_page1000="connect";page="keyboard";refresh_page())
	_add1000("HOST / ENTER WARMUP",Vector2(180,600),Vector2(430,76),["connect"],_host_lobby1000)
	_add1000("JOIN / ENTER WARMUP",Vector2(650,600),Vector2(430,76),["connect"],func():game.network_bridge.net.join(fields1000.address,fields1000.name,int(fields1000.port),fields1000.password);page="lobby";refresh_page())
	_add1000("JOIN AS SPECTATOR",Vector2(650,690),Vector2(430,58),["connect"],func():game.network_bridge.net.join(fields1000.address,fields1000.name,int(fields1000.port),fields1000.password,true);page="lobby";refresh_page())
	_add1000("BACK",Vector2(40,710),Vector2(170,58),["connect","host","keyboard"],func():
		if session_overlay:close_session_controls()
		else:page=_return_page1000 if page=="keyboard" else "lobby";refresh_page())
	for i in TABS1000.size():
		var tab =i
		_add1000(TABS1000[i],Vector2(35+i*176,160),Vector2(166,54),["host"],func():_host_tab1000=tab;refresh_page())
	for i in 9:
		var row =i
		var guide:=ColorRect.new();guide.position=Vector2(730,251+i*43);guide.size=Vector2(210,1);guide.color=Color("345969");guide.mouse_filter=Control.MOUSE_FILTER_IGNORE;viewport.add_child(guide);_rule_guides.append(guide)
		var text =words("",Vector2(130,238+i*43),21,Color.WHITE);text.size=Vector2(800,40);_rule_labels1000.append(text)
		var value_button=_add1000("CHANGE",Vector2(810,232+i*43),Vector2(345,39),["host"],func():_adjust1000(row,1));_rule_buttons1000.append([value_button])
		var slider:=HSlider.new();slider.position=Vector2(810,235+i*43);slider.size=Vector2(345,32);slider.value_changed.connect(func(value:float):_queue_rule_r5(row,value));viewport.add_child(slider);_rule_sliders_r5.append(slider)
	for spec in [["PRACTICE","practice",260,210],["TEAM REVIVE","team_revive",485,230],["DEATHMATCH","team_respawn",730,240],["FFA","free_for_all",985,170]]:
		var id:String=spec[1];var index:int=_add1000(str(spec[0]),Vector2(spec[2],655),Vector2(spec[3],50),["host"],func():_preset1000(id));_presets_r5[index]=id
	_roster_r5=preload("res://scripts/player_roster_r5.gd").new();viewport.add_child(_roster_r5);_roster_r5.position=Vector2(130,230);_roster_r5.setup(game,true)
	_add1000("SAVE RULES",Vector2(260,720),Vector2(210,50),["host"],func():_save_preset1000())
	_add1000("LOAD RULES",Vector2(485,720),Vector2(230,50),["host"],func():_load_preset1000())
	_add1000("BALANCE TEAMS",Vector2(730,720),Vector2(240,50),["host"],func():game.network_bridge.net.request("balance"))
	_host_start_index=_add1000("START MATCH",Vector2(985,720),Vector2(240,50),["host"],func():game.network_bridge.net.request("cancel" if game.network_bridge.net.stage=="countdown" else "start"))
	for i in 8:
		var row =i
		_team_buttons1000.append(_add1000("TEAM",Vector2(350,205+i*43),Vector2(90,37),["lobby"],func():_cycle_team1000(row)))
		_kick_buttons1000.append(_add1000("×",Vector2(445,205+i*43),Vector2(37,37),["lobby"],func():_kick1000(row)))
	_keyboard_label1000=words("",Vector2(120,165),30,Color("ffef5a"));_keyboard_label1000.size=Vector2(1080,80)
	var chars ="1234567890QWERTYUIOPASDFGHJKLZXCVBNM.:-_"
	for i in chars.length():
		var c =chars[i]
		_add1000(c,Vector2(120+(i%10)*105,275+floori(i/10)*80),Vector2(94,66),["keyboard"],func():fields1000[_keyboard_field1000]=(str(fields1000[_keyboard_field1000])+c).left(64);refresh_page())
	_add1000("SPACE",Vector2(225,635),Vector2(230,60),["keyboard"],func():fields1000[_keyboard_field1000]+=" ";refresh_page())
	_add1000("⌫",Vector2(480,635),Vector2(230,60),["keyboard"],func():fields1000[_keyboard_field1000]=str(fields1000[_keyboard_field1000]).left(maxi(0,str(fields1000[_keyboard_field1000]).length()-1));refresh_page())
	_add1000("DONE",Vector2(740,635),Vector2(335,60),["keyboard"],func():page="connect";refresh_page())
	buttons[4].position=Vector2(465,506);buttons[4].size=Vector2(350,58)
	buttons[0].position=Vector2(465,574)
	_add1000("SKINS",Vector2(650,712),Vector2(180,64),["lobby"],func():page="loadout";gallery.show_item(true,gallery.style);refresh_page())
	_skin_backing_r9=Panel.new();_skin_backing_r9.position=Vector2(85,130);_skin_backing_r9.size=Vector2(480,535);_skin_backing_r9.mouse_filter=Control.MOUSE_FILTER_IGNORE;var skin_style:=StyleBoxFlat.new();skin_style.bg_color=Color(.025,.055,.085,.92);skin_style.border_color=Color("297a91");skin_style.border_width_left=3;skin_style.corner_radius_top_left=12;skin_style.corner_radius_top_right=12;skin_style.corner_radius_bottom_left=12;skin_style.corner_radius_bottom_right=12;_skin_backing_r9.add_theme_stylebox_override("panel",skin_style);viewport.add_child(_skin_backing_r9);viewport.move_child(_skin_backing_r9,1)
	_add1000("LOBBY SPAWN",Vector2(200,315),Vector2(400,82),["spawn_choice"],func():_explore_from_r16("lobby"))
	_add1000("GAME SPAWN",Vector2(660,315),Vector2(400,82),["spawn_choice"],func():_explore_from_r16("game"))
	_add1000("BACK",Vector2(465,470),Vector2(350,60),["spawn_choice"],func():page="lobby";refresh_page())
	_built1000=true;refresh_page()
func _add1000(label:String,at:Vector2,size_value:Vector2,pages:Array,action:Callable)->int:
	var index =buttons.size();button(label,at,size_value);buttons[index].action_mode=BaseButton.ACTION_MODE_BUTTON_RELEASE;_actions1000[index]=action;_pages1000[index]=pages;return index
func activate(index:int)->void:
	if not active or index<0 or index>=buttons.size() or not buttons[index].visible or buttons[index].disabled:return
	if _actions1000.has(index):
		var cancel_countdown:bool=index==_host_start_index and game.network_bridge.net.stage=="countdown"
		_flush_rules_r5() # Commit the last slider drag before Start, tabs or presets.
		if cancel_countdown:game.network_bridge.net.request("cancel");return
		preload("res://scripts/sound_cues099.gd").play_ui(self,"menu_click");_pressed_button097=-1;_actions1000[index].call();return
	var net:Node=game.network_bridge.net if game.get("network_bridge")!=null else null
	if index==0 and net!=null and net.active:
		if session_overlay:close_session_controls()
		return
	if index==0 and selected_map.begins_with("archive:"):
		page="spawn_choice";refresh_page();return
	if index in [11,12]:_select_map1000("valley" if index==11 else "range");return
	super.activate(index)
	if index==9 and net!=null and net.active:net.request("character",{"style":gallery.style})
func _host_lobby1000()->void:
	var chosen:String=selected_map;var net:Node=game.network_bridge.net
	if net.host(fields1000.name,int(fields1000.port),fields1000.password)==OK:
		net.room_name=net.clean_name(fields1000.room);net.request("rules",{"map":chosen});page="lobby"
	refresh_page()
func _explore_from_r16(kind:String)->void:
	if kind not in ["lobby","game"]:return
	exploration_spawn_r16=kind;page="lobby";play_requested.emit()
func refresh_page()->void:
	super.refresh_page()
	if not _built1000:return
	if page not in ["lobby","maps","loadout"]:
		for i in 13:buttons[i].hide()
	var net:Node=game.network_bridge.net
	_custom_backing1000.visible=page in ["host","connect","keyboard","spawn_choice"]
	for index in _pages1000:buttons[index].visible=page in _pages1000[index];buttons[index].disabled=false
	for index in _actions1000:
		if buttons[index].tooltip_text.begins_with("Local community"):buttons[index].disabled=not preload("res://scripts/community_catalog1002.gd").installed()
	for pic in _map_views1000:pic.visible=page=="maps"
	if page=="maps":_map_render1003()
	for label in _rule_labels1000:label.visible=false
	for guide in _rule_guides:guide.visible=false
	for slider in _rule_sliders_r5:slider.hide()
	_roster_r5.visible=page=="host" and _host_tab1000==6
	if _roster_r5.visible:_roster_r5.refresh()
	_keyboard_label1000.visible=page=="keyboard";_keyboard_label1000.text=_keyboard_field1000.to_upper()+": "+str(fields1000[_keyboard_field1000])
	_roster1000.visible=page=="lobby" and net.active;_net_label1000.visible=page!="loadout"
	_net_label1000.text="" if not net.active else "%s · %s · %s · %d/%d PLAYERS%s"%[net.room_name,net.stage.to_upper(),_value1000("mode",net.rules.mode),net.players.size(),int(net.rules.max_players)," · HOST" if net.is_host() else ""]
	if net.active and net.stage=="countdown":_net_label1000.text+=" · %.1f"%maxf(0,net.countdown_end-net.now())
	if net.active:selected_map=str(net.rules.map)
	buttons[0].text="RESUME WARMUP" if net.active else "READY"
	buttons[0].disabled=false
	if net.active and net.players.has(net.me()):buttons[0].disabled=net.players[net.me()].spectator or net.stage not in ["lobby","countdown"]
	var roster ="";var ids:Array=net.players.keys()
	for id in ids:
		var p:Dictionary=net.players[id];roster+="%s%s  %dms\n"%["✓ " if p.ready else "○ ",p.name.left(15)+(" ★" if id==net.admin else ""),int(p.ping)]
	_roster1000.text=roster
	for i in _team_buttons1000.size():
		var b:Button=buttons[int(_team_buttons1000[i])];b.visible=page=="lobby" and net.active and i<ids.size()
		if b.visible:b.text="FFA" if int(net.players[ids[i]].team)<0 else "TEAM %d"%(int(net.players[ids[i]].team)+1);b.disabled=not net.is_host() and int(ids[i])!=net.me()
		buttons[int(_kick_buttons1000[i])].visible=page=="lobby" and net.active and net.is_host() and i<ids.size() and int(ids[i])!=net.me()
	for index in _actions1000:
		var b:Button=buttons[index]
		if index==_host_start_index:
			b.visible=page=="host";b.text="CANCEL COUNTDOWN" if net.stage=="countdown" else "START MATCH";b.disabled=not net.is_host() or (net.stage!="countdown" and not net.can_start())
		elif b.text=="HOST CONTROLS":b.visible=page=="lobby" and net.active and net.is_host()
		elif b.text=="START MATCH":b.visible=page=="lobby" and net.active and net.is_host() and net.stage=="lobby";b.disabled=not net.can_start()
		elif b.text=="LEAVE LOBBY":b.visible=page=="lobby" and net.active
		elif b.text=="HOST / JOIN":b.visible=page=="lobby" and not net.active
		elif b.text in ["REMATCH","RETURN TO LOBBY"]:b.visible=page=="lobby" and net.active and net.stage=="results" and net.is_host()
		elif page=="connect" and b.text.split(":")[0].to_lower() in fields1000:
			var key =b.text.split(":")[0].to_lower();b.text=key.to_upper()+": "+("••••" if key=="password" and not fields1000.password.is_empty() else str(fields1000[key]))
	if page=="host":
		title_text.text="HOST CONTROLS";var keys:Array=KEYS1000[_host_tab1000]
		for i in 9:
			var shown =i<keys.size();_rule_labels1000[i].visible=shown;_rule_guides[i].visible=shown
			for b in _rule_buttons1000[i]:buttons[int(b)].visible=false
			if shown:_configure_rule_r5(i,str(keys[i]))
		for index in _presets_r5:
			var button:Button=buttons[index];var chosen:bool=_preset_selected_r5(str(_presets_r5[index]));button.text=("● " if chosen else "")+{"practice":"PRACTICE","team_revive":"TEAM REVIVE","team_respawn":"DEATHMATCH","free_for_all":"FFA"}[str(_presets_r5[index])];button.modulate=Color("75ffd0") if chosen else Color.WHITE;button.disabled=not net.is_host() or net.stage!="lobby"
		for i in TABS1000.size():
			for index in _pages1000:
				if buttons[index].text==TABS1000[i]:buttons[index].modulate=Color("75ffd0") if i==_host_tab1000 else Color.WHITE
	elif page=="connect":title_text.text="PRIVATE MULTIPLAYER"
	elif page=="keyboard":title_text.text="ENTER "+_keyboard_field1000.to_upper()
	elif page=="spawn_choice":title_text.text=""
	selection_text.visible=page=="lobby" or page=="loadout"
	if page=="lobby":
		selection_text.text=str(Maps1000.info(selected_map).title)
		selection_text.position=Vector2(465,471)
		title_text.text="SESSION" if session_overlay else ""
	if net.active and net.stage=="results" and page=="lobby":buttons[0].hide();buttons[4].hide();selection_text.text="ROUND COMPLETE"
	_layout_skin_r9()
func _layout_skin_r9()->void:
	var skin:bool=page=="loadout" and gallery.characters
	_skin_backing_r9.visible=skin
	for spec in [[5,Vector2(350,672),Vector2(210,105)],[6,Vector2(720,672),Vector2(210,105)],[7,Vector2(400,108),Vector2(230,54)],[8,Vector2(650,108),Vector2(230,54)],[9,Vector2(500,594),Vector2(280,54)],[10,Vector2(40,650),Vector2(170,54)]]:buttons[spec[0]].position=spec[1];buttons[spec[0]].size=spec[2]
	title_text.position=Vector2(42,28);selection_text.add_theme_font_size_override("font_size",17);selection_text.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;selection_text.size=Vector2(350,28);selection_text.autowrap_mode=TextServer.AUTOWRAP_OFF
	if not skin:return
	title_text.text="CHARACTERS";title_text.position=Vector2(120,165)
	selection_text.position=Vector2(120,250);selection_text.size=Vector2(410,85);selection_text.horizontal_alignment=HORIZONTAL_ALIGNMENT_LEFT;selection_text.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART;selection_text.add_theme_font_size_override("font_size",25)
	buttons[5].position=Vector2(120,360);buttons[5].size=Vector2(180,64);buttons[6].position=Vector2(330,360);buttons[6].size=Vector2(180,64)
	buttons[9].position=Vector2(120,460);buttons[9].size=Vector2(390,72);buttons[10].position=Vector2(120,565);buttons[10].size=Vector2(180,64)
	buttons[7].position=Vector2(330,565);buttons[7].size=Vector2(180,64);buttons[8].hide()
func refresh_network()->void:
	if _built1000:refresh_page()
func _process(delta:float)->void:
	super._process(delta)
	_rule_clock_r5+=delta
	if _rule_clock_r5>=.1:_rule_clock_r5=0;_flush_rules_r5()
	_host_pointer_r5()
	if active and _built1000 and game.network_bridge.net.stage=="countdown":refresh_page()
func _select_map1000(id:String)->void:
	var net:Node=game.network_bridge.net
	if net.active:
		if not net.is_host():return
		net.request("rules",{"map":id})
	else:selected_map=id
	page="lobby";refresh_page()
func _map_pick1003(slot:int)->void:
	var index=_map_page1003*4+slot
	if index<_map_ids1003.size():_select_map1000(str(_map_ids1003[index]))
func _map_render1003()->void:
	for slot in 4:
		var index=_map_page1003*4+slot;var button:Button=buttons[_map_slots1003[slot]];var pic:TextureRect=_map_views1000[slot]
		button.visible=page=="maps" and index<_map_ids1003.size();pic.visible=button.visible
		if not button.visible:continue
		var id:String=_map_ids1003[index];var row=Maps1000.info(id);button.text=str(row.title);button.disabled=false;button.tooltip_text=""
		if id.begins_with("archive:"):
			var archive=preload("res://scripts/archive_catalog1003.gd").info(id);button.disabled=not preload("res://scripts/archive_catalog1003.gd").playable(id);button.text+="\n"+str(archive.get("creator","Unknown"))+(" ["+str(archive.get("status","missing")).to_upper()+"]" if button.disabled else "");button.tooltip_text="LOCAL ONLY · reuse license unknown\n"+str(archive.get("status","missing"))+": "+str(archive.get("error",""));button.add_theme_font_size_override("font_size",17)
		else:button.add_theme_font_size_override("font_size",23)
		if pic.get_meta("map_id","")!=id:pic.texture=Maps1000.image(id);pic.set_meta("map_id",id)
	buttons[_map_nav1003[0]].disabled=_map_page1003==0;buttons[_map_nav1003[1]].disabled=(_map_page1003+1)*4>=_map_ids1003.size();title_text.text="SELECT MAP  %d / %d"%[_map_page1003+1,ceili(float(_map_ids1003.size())/4)]
func leave_lobby()->void:
	if not _entered:return
	_entered=false;actor.global_transform=_return_pose;actor.restore_calibration_r17();actor.reset_motion();actor.get_world_3d().environment=_previous_environment;lobby.hide();gallery.close()
	preload("res://scripts/cyber_bird_registration.gd").select(game,selected_map)
func _adjust1000(row:int,direction:int)->void:
	var net:Node=game.network_bridge.net
	if not net.is_host():return
	var key:String=KEYS1000[_host_tab1000][row];var value:Variant=net.rules[key]
	if value is bool:value=not value
	elif Rules1000.CHOICES.has(key):
		var options:Array=Rules1000.CHOICES[key];value=options[posmod(options.find(value)+direction,options.size())]
	else:
		var step:float=.25 if value is float else 1.0
		if key in ["starting_health","starting_shield","starting_builds"]:step=10
		if key=="time_limit":step=60
		if key=="zone_wait":step=5
		if key=="zone_damage":step=1
		if key=="zone_speed":step=.5
		value=float(value)+step*direction
	net.request("rules",{key:value})
func _preset1000(id:String)->void:
	var rules:Dictionary=Rules1000.preset(id);rules.map=game.network_bridge.net.rules.map
	game.network_bridge.net.request("rules",rules)
func _save_preset1000()->void:
	if not game.network_bridge.net.is_host():return
	var file =FileAccess.open("user://lobby-preset1000.json",FileAccess.WRITE)
	if file:file.store_string(JSON.stringify(game.network_bridge.net.rules))
func _load_preset1000()->void:
	if not FileAccess.file_exists("user://lobby-preset1000.json"):return
	var value=JSON.parse_string(FileAccess.get_file_as_string("user://lobby-preset1000.json"))
	if value is Dictionary:game.network_bridge.net.request("rules",Rules1000.validate(value))
func _cycle_team1000(index:int)->void:
	var net:Node=game.network_bridge.net;var ids:Array=net.players.keys()
	if index>=ids.size() or int(net.rules.teams)==0:return
	var id:int=ids[index];net.request("team",{"id":id,"team":(int(net.players[id].team)+1)%int(net.rules.teams)})
func _kick1000(index:int)->void:
	var net:Node=game.network_bridge.net;var ids:Array=net.players.keys()
	if index<ids.size():net.request("kick",{"id":int(ids[index])})
func _value1000(key:String,value:Variant)->String:
	if value is bool:return "ON" if value else "OFF"
	if key in ["damage","npc_damage","build_damage","headshots","walk","run","flight","fling","stamina"]:return "%d%%"%roundi(float(value)*100)
	if key in ["respawn_delay","protection","downed_seconds"]:return "%.1f s"%float(value)
	if key=="mode":return "DEATHMATCH" if value=="respawn" else "TEAM REVIVE"
	if key=="zone_damage":return "%.1f HP / s"%float(value)
	if key=="zone_speed":return "%.1f m / s"%float(value)
	if key=="zone_wait":return "%.0f s"%float(value)
	if key=="time_limit":return "UNLIMITED" if int(value)==0 else "%d MIN"%roundi(float(value)/60)
	if key=="score_limit" and int(value)==0:return "UNLIMITED"
	if key=="teams" and int(value)==0:return "FREE-FOR-ALL"
	return str(value).to_upper().replace("_"," ")

func open_session_controls()->void:
	if not game.network_bridge.live():return
	session_overlay=true;page="host"
	_overlay_holstered=game.weapon.is_holstered;game.weapon.is_holstered=true;game.weapon._apply_selection()
	# The same tested pointer panel, anchored in the running map. No lobby teleport.
	_entered=true;set_active(true);_entered=false
	lobby.hide();game.field_menu.front_menu_visible=true;actor.input_enabled=false
func close_session_controls()->void:
	if not session_overlay:return
	_flush_rules_r5()
	session_overlay=false;set_active(false);game.field_menu.front_menu_visible=false
	game.weapon.is_holstered=_overlay_holstered;game.weapon._apply_selection();actor.input_enabled=true;actor.require_neutral_input()


func _configure_rule_r5(row:int,key:String)->void:
	var net:Node=game.network_bridge.net;var slider:HSlider=_rule_sliders_r5[row];var value:Variant=net.rules[key]
	var can_edit:bool=net.is_host() and Rules1000.editable(net.stage,key)
	if Rules1000.LIMITS.has(key):
		slider.set_block_signals(true)
		slider.show();slider.editable=can_edit;slider.min_value=float(Rules1000.LIMITS[key][0]);slider.max_value=float(Rules1000.LIMITS[key][1]);slider.step=1.0 if Rules1000.DEFAULTS[key] is int else .05
		if key=="time_limit":slider.step=60
		slider.set_value_no_signal(float(_pending_r5.get(key,value)))
		slider.set_block_signals(false)
	else:
		var button:Button=buttons[int(_rule_buttons1000[row][0])];button.show();button.text=_value1000(key,value);button.disabled=not can_edit
	_rule_labels1000[row].text=({"score_limit":"KILL CAP","time_limit":"TIME CAP"}.get(key,key.to_upper().replace("_"," ")))+"     "+_value1000(key,value)+( " · NEXT WARMUP" if not can_edit and net.is_host() else "")
func _queue_rule_r5(row:int,value:float)->void:
	var keys:Array=KEYS1000[_host_tab1000]
	if row<keys.size() and game.network_bridge.net.is_host():_pending_r5[str(keys[row])]=value
func _flush_rules_r5()->void:
	if _pending_r5.is_empty():return
	var changes:Dictionary=_pending_r5.duplicate();_pending_r5.clear();game.network_bridge.net.request("rules",changes)
func _preset_selected_r5(id:String)->bool:
	var r:Dictionary=game.network_bridge.net.rules
	var practice:bool=r.mode=="respawn" and float(r.damage)==0 and r.catalog and r.infinite_ammo and not r.zone_enabled
	if id=="practice":return practice
	if practice:return false
	if id=="team_revive":return r.mode=="revive"
	if id=="free_for_all":return r.mode=="respawn" and int(r.teams)==0
	return r.mode=="respawn" and int(r.teams)>0
func _host_pointer_r5()->void:
	var eligible:bool=active and page=="host" and actor.xr_enabled and actor.tracking_available
	var hand:XRController3D=actor.get_weapon_hand()
	eligible=eligible and actor.is_hand_input_ready(hand)
	var regions:Array=[]
	for slider in _rule_sliders_r5:
		if slider.visible:regions.append(slider.get_global_rect().grow(5))
	if _roster_r5.visible:regions.append_array(_roster_r5.regions())
	var pressed:bool=hand.get_float("trigger")>.65 or hand.is_button_pressed("trigger_click")
	_surface_r5.step(viewport,board,preload("res://scripts/weapon.gd").corrected_grip_transform(hand.global_transform),pressed,eligible,regions)
