extends RefCounted
## Deliberately small merge surface. Call after the lobby has restored its pose/environment.
static func select(game: Node, id: String) -> void:
	if id.begins_with("archive:") and not preload("res://scripts/archive_catalog1003.gd").playable(id):game._feedback("Map pack is unavailable.",6);return
	var current:Node=game.active_sandbox_map
	if is_instance_valid(current):
		var current_id:String=str(current.get("map_id")) if "map_id" in current else (str(current.get("MAP_ID")) if current.get_script().get_script_constant_map().has("MAP_ID") else "range")
		if current_id==id and (not "map_ready" in current or current.map_ready):return
	var loader=preload("res://scripts/map_loading_r18.gd").get_for(game);var owned:bool=not loader.active;var token:int=loader.begin() if owned else loader.serial;loader.pending=true
	loader.record_stage_r20("MAP_SELECTED",id)
	var tree:SceneTree=game.get_tree()
	for i in 2:
		await tree.process_frame
		if not is_instance_valid(game) or not is_instance_valid(loader) or token!=loader.serial:return
	loader.update_phase("LOADING MAP",.12)
	var cached:Resource
	if id.begins_with("archive:"):
		var path:String="res://local_archive_r10/"+preload("res://scripts/archive_catalog1003.gd").slug(id)+".res"
		cached=await loader.load_resource_r18(path,token)
	if not is_instance_valid(game) or not is_instance_valid(loader) or token!=loader.serial:return
	loader.update_phase("PREPARING MAP",.65)
	_select_now_r18(game,id,cached)
	var deadline:int=Time.get_ticks_msec()+120000
	while is_instance_valid(game.active_sandbox_map) and "map_ready" in game.active_sandbox_map and not game.active_sandbox_map.map_ready and Time.get_ticks_msec()<deadline:
		if "load_error" in game.active_sandbox_map and not game.active_sandbox_map.load_error.is_empty():break
		if not is_instance_valid(game) or not is_instance_valid(loader) or token!=loader.serial:return
		loader.update_phase("PREPARING MAP",.9);await tree.process_frame
		if not is_instance_valid(game) or not is_instance_valid(loader):return
	if not is_instance_valid(game) or not is_instance_valid(loader) or token!=loader.serial:return
	preload("res://scripts/map_runtime_rules_r14.gd").apply(game,id)
	loader.pending=false
	if owned:loader.finish(token)
static func _select_now_r18(game:Node,id:String,cached:Resource=null)->void:
	if id.begins_with("archive:") and not preload("res://scripts/archive_catalog1003.gd").verify(id):game._feedback("Local map cache is incomplete or changed. Revalidate it before playing.",10);return
	var current: Node3D = game.active_sandbox_map
	if id=="valley" and not is_instance_valid(current):
		game.world.show();game.new_campaign();return
	if is_instance_valid(current):
		var current_id: String = str(current.get("map_id")) if "map_id" in current else (str(current.get("MAP_ID")) if current.get_script().get_script_constant_map().has("MAP_ID") else "range")
		if current_id == id:
			if id in ["cyber_bird","community_local"] or id.begins_with("archive:"): game.player.get_world_3d().environment = current._environment
			return
		preload("res://scripts/map_runtime_rules_r14.gd").restore(game)
		if current.has_method("leave"): current.leave()
		game.active_sandbox_map = null
		current.get_parent().remove_child(current)
		current.queue_free()
	if id.begins_with("archive:") and preload("res://scripts/archive_catalog1003.gd").playable(id):
		var archive=preload("res://scripts/archive_map1003.gd").new();archive.map_id=id;archive.preloaded_r18=cached;game.add_child(archive);archive.enter(game)
	elif id == "community_local" and preload("res://scripts/community_catalog1002.gd").installed():
		var community=preload("res://scripts/community_map1002.gd").new();game.add_child(community);community.enter(game)
	elif id == "cyber_bird":
		var city := preload("res://scripts/cyber_bird.gd").new()
		game.add_child(city)
		city.enter(game)
	elif id == "range":
		var practice := preload("res://scripts/sandbox_map_091.gd").new()
		game.add_child(practice)
		practice.enter(game)
	else:
		game.world.show()
		game.new_campaign()

static func unload(game:Node)->void:
	var loader=game.get_node_or_null("MapLoadingR18")
	if loader!=null:loader.cancel()
	preload("res://scripts/map_runtime_rules_r14.gd").restore(game)
	var current:Node=game.active_sandbox_map
	game.active_sandbox_map=null
	if is_instance_valid(current):
		if current.has_method("leave"):current.leave()
		current.process_mode=Node.PROCESS_MODE_DISABLED
		current.get_parent().remove_child(current)
		current.queue_free()
	# Providers live outside the map. Retire map-owned loot and transient instances too.
	for prop in game.weapon_inventory.pickups.values():
		if is_instance_valid(prop):prop.queue_free()
	game.weapon_inventory.pickups.clear()
	for prop in game.weapon_inventory.review_owned_props.values():
		if is_instance_valid(prop):prop.queue_free()
	game.weapon_inventory.review_owned_props.clear();game.weapon_inventory.review_positions.clear()
	game.weapon.selected=0;game.weapon_inventory.owned.assign([0,2,9])
	for i in range(30,game.weapon.weapons.size()):
		if is_instance_valid(game.weapon.weapons[i]):game.weapon.weapons[i].queue_free()
	game.weapon.weapons.resize(30);game.weapon.weapon_names.resize(30);game.weapon.weapon_ids.resize(30);game.weapon.instance_catalog.clear()
	for prop in game.ammo_pickups.boxes:
		if is_instance_valid(prop):prop.queue_free()
	game.ammo_pickups.boxes.clear()
	for family in game.ammo_pickups.families:game.ammo_pickups.spawn_box(str(family),game.world.player_spawn+Vector3(0,.3,-3))
	for prop in game.field_consumables.pickups:
		if is_instance_valid(prop):prop.queue_free()
	game.field_consumables.pickups.clear()
	game.field_builder.reset_builds()
	game.world_ping.clear_all()
	game.impact_marks.clear_all()
	for prop in game.get_tree().get_nodes_in_group("ashfall_projectile"):
		if is_instance_valid(prop):prop.queue_free()
	# Menus use their own environment; don't retain the city through a saved reference.
	if is_instance_valid(game.front_menu):
		game.front_menu._previous_environment=null
		game.front_menu._return_pose=Transform3D(Basis.IDENTITY,game.world.player_spawn)
	_report_unloaded(game)
static func _report_unloaded(game:Node)->void:
	var tree:SceneTree=game.get_tree()
	await tree.process_frame
	await tree.process_frame
	if is_instance_valid(game):print("MAP_LIFECYCLE retired nodes=",game.get_tree().get_node_count()," static_bytes=",OS.get_static_memory_usage())
