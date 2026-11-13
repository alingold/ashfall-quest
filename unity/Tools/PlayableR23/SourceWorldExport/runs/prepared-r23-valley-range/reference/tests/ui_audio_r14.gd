extends SceneTree
var checks=0
var failures:Array=[]
var game:Node
class NetworkAttempt extends Node:
	func live()->bool:return true
class Rescue extends Node3D:
	var calls=0
	func revive()->bool:calls+=1;return true
	func revive_contacts()->Array:return [global_position+Vector3(-.13,1,0),global_position+Vector3(.13,1,0)]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func typed_count(kind:String)->int:return root.find_children("*",kind,true,false).size()
func fx_count()->int:
	var count=0
	for node in root.find_children("*","Node3D",true,false):
		if node.get_script()==preload("res://scripts/revive_discharge_fx_091.gd"):count+=1
	return count
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var rarity=preload("res://scripts/weapon_rarity_r6.gd");var rack:Node=game.weapon;var inventory:Node=game.weapon_inventory;var wheel:Node=rack.wheel
	inventory.capacity=6;var owned:Array[int]=[]
	for stars in range(1,6):owned.append(rack.create_instance([0,2,3,7,9][stars-1],stars))
	var blade:int=rack.create_instance(14,5);owned.append(blade);inventory.owned.assign(owned);wheel.set_weapon_icons(rack.weapons);wheel.set_owned_indices(owned)
	for stars in range(1,6):
		var index:int=owned[stars-1];var slot:int=wheel.SLOT_ACTIONS.find("weapon_%d"%index);wheel._set_highlight(slot)
		var icon:Sprite3D=wheel.get_node("WeaponIcon%d"%index);var label:Label3D=wheel.get_node("WeaponStarsR14_%d"%index)
		check(icon.modulate.is_equal_approx(rarity.color(stars)),"wheel icon rarity color "+str(stars))
		check(label.modulate.is_equal_approx(rarity.color(stars)) and label.text==rarity.stars_text(stars),"wheel star count/color "+str(stars))
		check(wheel.status_label.modulate.is_equal_approx(rarity.color(stars)),"wheel pointed title rarity color "+str(stars))
	check(wheel.get_node("WeaponIcon%d"%blade).modulate==Color.WHITE and wheel.get_node_or_null("WeaponStarsR14_%d"%blade)==null,"fixed-form blade stays white with no stars")
	var service:Node=game.item_catalog_088;var ui:Node=game.field_menu.catalog_ui;service.testing=false;ui.refresh()
	var colored=0
	for i in ui.entries.size():
		var entry:Dictionary=ui.entries[i]
		if not entry.get("rarity_tinted",false):continue
		var content:Node=ui.pinned_buttons[i].get_child(0);var expected:Color=rarity.color(entry.rarity)
		check(content.get_child(0).modulate.is_equal_approx(expected) and content.get_child(1).modulate.is_equal_approx(expected),"backpack carried icon/caption color "+str(entry.rarity));colored+=1
	check(colored==5,"all five carried weapon levels are colored in backpack")
	service.testing=true;service.selected_rarity_r6=4;ui.refresh()
	for i in range(12,ui.entries.size()):
		var entry:Dictionary=ui.entries[i]
		if entry.kind!="weapon":continue
		var content:Node=ui.catalog_buttons[i-12].get_child(0);var expected:Color=rarity.color(4) if rarity.tiered(entry.catalog) else Color.WHITE
		check(content.get_child(0).modulate.is_equal_approx(expected) and content.get_child(1).modulate.is_equal_approx(expected),"catalog respects rarity/fixed form "+str(entry.catalog))
	for index in owned:
		for mesh in rack.weapons[index].model.find_children("*","MeshInstance3D",true,false):check(mesh.material_overlay==null,"UI tint does not glow held mesh "+mesh.name)
	var menu:Node=game.field_menu
	check(menu._panel.get_node("SettingsBoard").material_override.no_depth_test,"settings black backdrop ignores world depth")
	check(menu._spatial_title.render_priority>110,"settings text sorts above black backdrop")
	for part in menu._pointer_lines+menu._pointer_dots:check(part.material_override.no_depth_test and part.material_override.render_priority>118,"both hand lasers/dots render above map and panel")
	for part in menu._numeric_fills+menu._numeric_knobs:check(part.material_override.no_depth_test,"slider material ignores world depth")
	check(game.front_menu.board.material_override.no_depth_test and game.front_menu.pointer.material_override.no_depth_test,"main/host board and pointer ignore world depth")
	game.front_menu._draw_beam097(Vector3.ZERO,Vector3(0,0,-1));check(game.front_menu._beam097.material_override.no_depth_test,"main/host laser ignores world depth")
	check(ui.board.material_override.no_depth_test and ui.pointer.material_override.no_depth_test,"backpack board/pointer ignore world depth")
	var map:Node=game.network_bridge.tactical;game.front_end_active=false;menu.is_open=false;rack.is_holstered=true;game.front_menu.selected_map="range";map.set_open(true);map._process(0)
	check(map.caption.text=="SANDBOX RANGE" and map.caption.position.x<100,"tactical map name only at top left")
	check(map.roster.text=="YOU","no duplicate HP/shield readout")
	var clutter=false
	for label in map.canvas.find_children("*","Label",true,false):
		for text in ["NORTH UP","offhand trigger","Desktop: hold M","Local Archive","TACTICAL MAP"]:
			if label.text.contains(text):clutter=true
	check(not clutter,"unnecessary tactical help/subtitle/header labels removed");map.set_open(false)
	# Real paddle voices using the accepted samples; no microphone or XR runtime.
	game.player.input_enabled=true;game.player.inventory_open=false;game.player.head_obstructed=false;game.player.mantling=false;game.player._grabs.clear()
	var pads:Node=game.revive_paddles;pads.active=true;pads._facing=true;pads.charge=.4;pads._update_reference099(Vector3.ZERO)
	check(pads._reference099.playing and pads._reference099.pitch_scale==1,"recorded facing cue starts at original pitch")
	var facing:AudioStream=pads._reference099.stream;pads._charge_feedback_r14(1,Vector3.ZERO)
	check(pads._reference099.stream==facing and not pads._audio.playing,"charging progress does not overlap facing recording")
	pads._reference099.stop();pads._charge_feedback_r14(1,Vector3.ZERO);var tick:AudioStream=pads._reference099.stream
	check(tick==AshAudioBank.sample("revive_charge",mini(9,floori(pads.charge*10))) and not pads._audio.playing,"one voice owns progress ticks")
	pads._update_reference099(Vector3.ZERO);check(pads._reference099.stream==tick and pads._reference099.playing,"facing update does not restart or replace progress tick")
	pads._reference099.stop();pads.charge=.99;pads._beep_clock=0;pads._charge_feedback_r14(.22,Vector3.ZERO);check(not pads._reference099.playing,"tick cadence never truncates a .23s clip")
	var completions:int=pads.completion_events;pads._add_charge(.02)
	check(pads.charge_ready and pads._audio.stream==AshAudioBank.sample("revive_ready_full0992") and pads._audio.playing and not pads._reference099.playing,"ready one-shot replaces charging voice immediately")
	pads._add_charge(1);check(pads.completion_events==completions+1,"one completion onset per ready interval")
	game.player.input_enabled=false;pads._process(.01);check(not pads._audio.playing and not pads._reference099.playing,"suspension stops all paddle sound")
	game.player.input_enabled=true;pads._process(.01);check(not pads._audio.playing,"resume does not replay ready completion")
	var network:=NetworkAttempt.new();root.add_child(network);var target:=Rescue.new();root.add_child(target);target.add_to_group("network_players1000")
	var original_bridge:Node=inventory.network_bridge;inventory.network_bridge=network;pads.charge_ready=true
	var voices:int=typed_count("AudioStreamPlayer3D");var effects:int=fx_count();var successes:int=pads.successful_revives
	check(pads._revive(target) and target.calls==1,"network paddle issues one revive attempt")
	check(typed_count("AudioStreamPlayer3D")==voices and fx_count()==effects and pads.successful_revives==successes,"network attempt creates no duplicate discharge or premature success")
	inventory.network_bridge=original_bridge
	original_bridge._event("revive",{"position":game.player.global_position,"player":2,"by":original_bridge.net.me()})
	check(typed_count("AudioStreamPlayer3D")==voices+1 and fx_count()==effects+1,"authoritative confirmation creates one discharge sound and effect")
	pads.stow();network.queue_free();target.queue_free();game._quitting=true;game.queue_free();for i in 3:await process_frame
	var result={"passed":failures.is_empty(),"checks":checks,"failures":failures,"scope":"Actual Main UI/rack/material/audio nodes, five rarity levels, overlay order and controlled network revive attempt/confirmation; XR off and Dummy audio, no listening/headset acceptance"}
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/UI-AUDIO-R14.json",FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));print("UI_AUDIO_R14 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)

