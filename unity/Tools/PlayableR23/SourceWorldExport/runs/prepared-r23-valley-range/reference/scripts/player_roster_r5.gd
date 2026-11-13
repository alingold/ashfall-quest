extends Control
## Shared room roster: local mute/volume everywhere, authoritative kick in host controls.
var game:Node
var host_view=false
var rows:Array=[]
var ids:Array=[]
var volume:HSlider
var volume_title:Label
var selected_peer=-1
var _refresh_clock=0.0
func setup(g:Node,host:bool=false)->void:
	game=g;host_view=host;mouse_filter=Control.MOUSE_FILTER_IGNORE
	var wide:bool=host_view
	for i in 8:
		var y:float=i*(46 if wide else 68)
		var name_label:=Label.new();name_label.position=Vector2(0,y);name_label.size=Vector2(390 if wide else 126,32);name_label.add_theme_font_size_override("font_size",21 if wide else 17);name_label.text_overrun_behavior=TextServer.OVERRUN_TRIM_ELLIPSIS;add_child(name_label)
		var indicator:=Label.new();indicator.position=Vector2(408 if wide else 129,y);indicator.size=Vector2(30,32);indicator.text="●";indicator.add_theme_font_size_override("font_size",20);add_child(indicator)
		var speaker:=Button.new();speaker.position=Vector2(450 if wide else 157,y);speaker.size=Vector2(65 if wide else 40,32);speaker.icon=preload("res://assets/ui/speaker_r5.svg");speaker.expand_icon=true;speaker.add_theme_constant_override("icon_max_width",22);speaker.tooltip_text="Player voice volume";add_child(speaker);speaker.pressed.connect(func():_select_volume(i))
		var mute:=Button.new();mute.position=Vector2(530 if wide else 204,y);mute.size=Vector2(90 if wide else 58,32);mute.text="MUTE";mute.add_theme_font_size_override("font_size",14);add_child(mute);mute.pressed.connect(func():if i<ids.size():game.network_bridge.voice.toggle_mute(int(ids[i]));refresh())
		var kick:=Button.new();kick.position=Vector2(800,y);kick.size=Vector2(100,32);kick.text="KICK";add_child(kick);kick.pressed.connect(func():if i<ids.size():game.network_bridge.net.request("kick",{"id":int(ids[i])}))
		rows.append([name_label,indicator,speaker,mute,kick])
	volume_title=Label.new();volume_title.position=Vector2(0,390 if wide else 570);volume_title.add_theme_font_size_override("font_size",18);add_child(volume_title)
	volume=HSlider.new();volume.position=Vector2(0,420 if wide else 602);volume.size=Vector2(600 if wide else 262,28);volume.min_value=0;volume.max_value=1;volume.step=.01;add_child(volume);volume.value_changed.connect(func(value:float):if selected_peer>=0:game.network_bridge.voice.set_player_volume(selected_peer,value);_volume_text())
	refresh()
func _select_volume(row:int)->void:
	if row>=ids.size():return
	selected_peer=int(ids[row]);volume.set_value_no_signal(game.network_bridge.voice.player_volume(selected_peer));refresh()
func _volume_text()->void:
	volume_title.text="%s · %d%%"%[str(game.network_bridge.net.players.get(selected_peer,{}).get("name","PLAYER")).left(18),roundi(volume.value*100)]
func refresh()->void:
	if not is_instance_valid(game):return
	var net:Node=game.network_bridge.net;var voice:Node=game.network_bridge.voice;ids=net.players.keys();ids.sort()
	for i in 8:
		for control in rows[i]:control.visible=i<ids.size()
		if i>=ids.size():continue
		var id:int=ids[i];var p:Dictionary=net.players[id];var own:bool=id==net.me()
		rows[i][0].text=str(p.name)+(" (YOU)" if own else "")
		rows[i][0].modulate=Color("ff525e") if str(p.get("life","alive")) in ["downed","eliminated","dead"] else Color.WHITE
		rows[i][1].modulate=Color("4affb1") if voice.is_speaking(id) else Color("415665")
		rows[i][2].disabled=own;rows[i][3].disabled=own
		rows[i][3].text="UNMUTE" if voice.muted.has(id) else "MUTE"
		rows[i][3].modulate=Color("ff8c88") if voice.muted.has(id) else Color.WHITE
		rows[i][4].visible=host_view and net.is_host() and not own
	volume.visible=selected_peer>=0 and net.players.has(selected_peer) and selected_peer!=net.me();volume_title.visible=volume.visible
	if volume.visible:_volume_text()
func regions()->Array:
	var result:Array=[]
	for row in rows:
		for i in [2,3,4]:
			if row[i].visible:result.append(row[i].get_global_rect())
	if volume.visible:result.append(volume.get_global_rect().grow(8))
	return result
func _process(delta:float)->void:
	if not is_visible_in_tree():return
	_refresh_clock+=delta
	if _refresh_clock>=.1:_refresh_clock=0;refresh()
