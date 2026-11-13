extends SceneTree
var checks=0
var failures:Array=[]
var facts:Array=[]
var game:Node
var output:String
func _initialize():call_deferred("run")
func check(ok:bool,reason:String):
	checks+=1
	if not ok:failures.append(reason);print("FAIL_R8 ",reason)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func settle():
	for i in 5:await physics_frame;await process_frame
func check_hud(node:Node):
	if node is MeshInstance3D and node.material_override is BaseMaterial3D:check(node.material_override.no_depth_test,"HUD always visible: "+str(node.name))
	if node is Label3D or node is Sprite3D:check(node.no_depth_test,"HUD label/icon always visible: "+str(node.name))
	for child in node.get_children():check_hud(child)
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);await settle();freeze(game)
	var styles=preload("res://scripts/gemini_player_models_099.gd")
	check(styles.STYLES.size()==7,"seven styles: three inherited plus four completed creator variants")
	for i in range(3,7):
		var meshes:Dictionary=styles._load_kit_meshes(i);check(meshes.has("AvatarHead") and meshes.has("Jacket") and meshes.has("LUpperArm") and meshes.has("RBoot"),"creator variant has fitted head/body/limbs "+str(i))
		check(styles.STYLES[i].contains(["ASTRA","SOL","ARGON","CLAUDE"][i-3]),"creator model label "+str(i))
	var skins_button=-1
	for i in game.front_menu.buttons.size():
		if game.front_menu.buttons[i].text=="SKINS":skins_button=i
	check(skins_button>=0 and game.front_menu.buttons[skins_button].visible,"dedicated visible SKINS button")
	game.front_menu.activate(skins_button);check(game.front_menu.page=="loadout" and game.front_menu.gallery.characters,"SKINS opens actual character preview")
	var height:float=game.player.vertical_combat_target_top_height_m
	game.front_menu.gallery.show_item(true,6);game.front_menu.gallery.use_character()
	check(game.body_visual.get_meta("character_style",-1)==6 and game.field_menu.get_settings().character_style_r8==6,"Claude selection updates body and persisted preference")
	check(game.player.vertical_combat_target_top_height_m==height,"skin selection retains calibration")
	check_hud(game.combat_hud)
	for line in game.field_menu._pointer_lines:check(line.cast_shadow==GeometryInstance3D.SHADOW_CASTING_SETTING_OFF,"menu pointer cannot cast a second beam-like shadow")
	game.front_menu._draw_beam097(Vector3.ZERO,Vector3.FORWARD)
	game.player.xr_enabled=true;game.player.head_obstructed=true;game.field_menu.is_open=true
	game.front_menu._process(0);game.field_menu._process(.2)
	check(not game.front_menu._beam097.visible,"settings owns pointer rendering; inherited right beam is hidden")
	check(game.field_menu._obstruction_fade==0,"head obstruction retains collision without full-screen blackout")
	game.player.xr_enabled=false;game.player.head_obstructed=false;game.field_menu.is_open=false
	game.front_menu.page="lobby";game.front_menu.gallery.close();game.front_menu.refresh_page()
	for slug in ["atrocious-gods-trashpandakiller","battlefield-paintball-gods-pitdevil","banana-land-gods-pitdevil","bamboo-gods-pitdevil","classic-bigbox-vr"]:
		var id:String="archive:"+slug;game.front_menu.selected_map=id
		if game.front_end_active:game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
		await settle();freeze(game)
		var map:Node=game.active_sandbox_map
		check(map.map_ready and map.load_error.is_empty(),slug+" READY")
		check(map.statistics.get("prepared_adapter_r8",false),slug+" uses prepared validated adapter")
		check(map.safe_spawn_r8(map.warmup_spawn_r8),slug+" supported warmup spawn")
		var net:Node=game.network_bridge.net;net.rules.map=id;net.zone.clear()
		var players={1:{"team":0},2:{"team":1}}
		for player in [1,2]:check(map.safe_spawn_r8(game.network_bridge._spawn_point(player,players,net.rules)),slug+" supported unzoned match spawn "+str(player))
		var info:Dictionary=preload("res://scripts/map_catalog1000.gd").info(id);net.zone={"next":Rect2(info.min,info.max-info.min)}
		for player in [1,2]:check(map.safe_spawn_r8(game.network_bridge._spawn_point(player,players,net.rules)),slug+" supported zoned match spawn "+str(player))
		net.zone.clear()
		var play:Node=map.gameplay_r8
		check(play.sites.size()>0 and play.cache_ids.size()>0,slug+" real cache sites activated")
		for site in play.cache_ids:
			var items:Array=play.cache_ids[site];check(items.size()==6,slug+" six real weapons per activated site")
			for index in items:check(game.weapon_inventory.pickups.has(index) and index<game.weapon.weapons.size(),slug+" weapon registered with inventory")
		check(game.ammo_pickups.boxes.size()>=72,slug+" ammo/build providers retained")
		check(play.boards.size()>0,slug+" live scoreboard exists")
		if slug=="classic-bigbox-vr":
			check(play.pads.size()==12,"Classic twelve useful functional launch pads")
			game.player.global_position=play.pads[0]+Vector3.UP*.15;game.player.velocity=Vector3.ZERO;game.player.input_enabled=true;play._launch_cooldown=0;play._physics_process(.01)
			check(game.player.velocity.y==30 and play._launch_cooldown>1,"standing on actual launch site launches player with cooldown")
		var panel:Node=null
		for group in play._panels_r8.values():
			if not group.is_empty():panel=group[0];break
		if panel!=null:
			var before:bool=panel.collision_layer==1;panel.take_damage(1);await settle();check(before and panel.collision_layer==0 and not panel.view.visible,"breakable panel really removes solid collision and visible pane "+slug)
			panel._apply(false)
		facts.append({"map":id,"warmup":str(map.warmup_spawn_r8-map.global_position),"match_sites":map.spawn_points.size(),"cache_sites":play.sites.size(),"active_caches":play.cache_ids.size(),"panels":map._details_r8.panels.size(),"pads":play.pads.size(),"prepared":map.statistics.get("prepared_adapter_r8",false)})
		var weak:WeakRef=weakref(map);game._return_home0992();await settle();freeze(game);check(weak.get_ref()==null,slug+" unloads")
	var receipt={"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"xr":"off","headset_tested":false,"scope":"real Main, prepared map resources, inventory cache instances, floor/capsule spawn checks, panel physics layers, launcher velocity, skin selection and persistence; no headset input simulation"}
	var file=FileAccess.open(output+"/FEEDBACK-R8.json",FileAccess.WRITE);file.store_string(JSON.stringify(receipt,"\t"));print("FEEDBACK_R8 checks=",checks," failures=",failures.size());game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
