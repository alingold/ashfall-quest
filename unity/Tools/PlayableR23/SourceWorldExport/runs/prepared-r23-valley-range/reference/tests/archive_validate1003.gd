extends SceneTree
var game:Node
var slug:String
var output:String
var failures:Array=[]
var facts:Dictionary={}
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);print("FAIL ",slug," ",label)
func run()->void:
	var args=OS.get_cmdline_user_args();slug=args[0];output=args[1];var id="archive:"+slug;var started=Time.get_ticks_msec()
	game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	game.front_menu.selected_map=id;game._start_from_title()
	var map:Node=game.active_sandbox_map
	if not is_instance_valid(map) or not "map_id" in map or map.map_id!=id:check(false,"map selector/registry failed to load requested map");finish();return
	for i in 8:await physics_frame
	check(map.load_error.is_empty(),"spawn/loot discovery: "+map.load_error)
	if not map.map_ready:check(false,"map never became playable");finish();return
	check(map.spawn_points.size()>0 and map.loot_points.size()>0,"safe spawn and loot sites")
	var outside_loot=0;var outside_spawns=0
	for point in map.loot_points:
		if map._outside(point):outside_loot+=1
	for point in map.spawn_points:
		if map._outside(point):outside_spawns+=1
	check(outside_loot==0,"loot sites must lie inside recoverable play bounds: "+str(outside_loot)+" outside")
	check(outside_spawns==0,"spawns must lie inside recoverable play bounds: "+str(outside_spawns)+" outside")
	check(preload("res://scripts/archive_catalog1003.gd").fingerprint(id)==map._meta.cache_fingerprint,"all local scene/material/texture cache hashes")
	for point in map.spawn_points:
		var at=map._floor(point+Vector3.UP,3);check(at.is_finite() and map._room(at),"spawn floor/headroom/hazard validation")
	var counts=[game.weapon_inventory.pickups.size(),game.ammo_pickups.boxes.size(),game.field_consumables.pickups.size()]
	check(counts[0]>0 and counts[1]>=72 and counts[2]>0,"ASHFALL loot providers")
	var floor_count=0
	for prop in game.weapon_inventory.pickups.values():
		if map._floor(prop.global_position+Vector3.UP,4).is_finite():floor_count+=1
	check(floor_count==counts[0],"weapon pickups have nearby floors")
	for cycle in 2:
		game._begin_chapter()
		for i in 5:await physics_frame
		check(counts==[game.weapon_inventory.pickups.size(),game.ammo_pickups.boxes.size(),game.field_consumables.pickups.size()],"loot reset count stable")
	facts={"spawns":map.spawn_points.size(),"spawn_source":map.spawn_origin,"loot_sites":map.loot_points.size(),"outside_loot_sites":outside_loot,"outside_spawns":outside_spawns,"weapons":counts[0],"supplies_including_hidden_original":counts[1],"heals":counts[2],"world_collision_tiles":map.statistics.collision_chunks,"solid_triangles":map.statistics.solid_triangles,"draw_batches":map.statistics.draw_batches,"layers":map.statistics.layers,"fingerprint":map._meta.cache_fingerprint,"spawn":map.spawn,"milliseconds_before_teardown":Time.get_ticks_msec()-started}
	preload("res://scripts/cyber_bird_registration.gd").select(game,"range")
	for i in 5:await physics_frame
	check(game.find_children("LocalArchiveMap","Node3D",true,false).is_empty(),"map geometry and volumes removed on range transition")
	check(game.active_sandbox_map.name=="StandaloneSandboxRange091","previous range remains playable")
	preload("res://scripts/cyber_bird_registration.gd").select(game,id)
	for i in 8:await physics_frame
	check(game.active_sandbox_map.map_ready,"repeated scene load playable")
	check(game.active_sandbox_map.spawn_points.size()==facts.spawns,"deterministic repeated safe spawn discovery")
	finish()
func finish()->void:
	var report={"slug":slug,"passed":failures.is_empty(),"failures":failures,"facts":facts,"scope":"actual ASHFALL game/physics load, safe spawn and loot reset, teardown and repeated load; scripted XR-off desktop inputs"};var f=FileAccess.open(output,FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"));f.close();print("ARCHIVE_VALIDATION "+slug+" "+str(report.passed));quit(0 if failures.is_empty() else 1)
