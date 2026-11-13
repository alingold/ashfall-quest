extends "res://scripts/community_map1002.gd"
## Uses one optional map cache at a time. Archive rules/scenery pickups are not game logic.
const Archive=preload("res://scripts/archive_catalog1003.gd")
var map_id=""
var preloaded_r18:Resource
var load_error=""
var spawn_origin="recorded"
var _hazard_clock=0.0
var _floor_exclusions:Array[RID]=[]
var warmup_spawn_r8:=Vector3.ZERO
var gameplay_r8:Node
var _details_r8:Dictionary={}
var tactical_texture_r8:Texture2D
var fall_level_r9=-INF
var center_respawn_r9:=Vector3.ZERO
var _fall_spawning_r9:Dictionary={}
var _panel_spawn_cells_r9:Dictionary={}
var semantics_r14:Node3D
func enter(game:Node)->void:
	_game=game;name="LocalArchiveMap";position=Vector3(0,0,4800);_meta=Archive.manifest(map_id);statistics=_meta.duplicate(true)
	var prepared:String="res://local_archive_r10/"+Archive.slug(map_id)+".res"
	if FileAccess.file_exists(prepared):
		var resource:Resource=preloaded_r18 if preloaded_r18!=null else load(prepared)
		preloaded_r18=null
		if resource==null or resource.source_fingerprint!=str(_meta.cache_fingerprint):load_error="Prepared map does not match its source checkpoint";return
		var index_path:String="res://assets/data/archive_adapters_r10.json"
		if FileAccess.file_exists(index_path):
			var index:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(index_path))
			if str(index.get("files",{}).get(Archive.slug(map_id),""))!=FileAccess.get_sha256(prepared):load_error="Prepared map fingerprint does not match this build";return
		_geometry=resource.geometry.instantiate();_details_r8=resource.details.duplicate(true);statistics["prepared_adapter_r8"]=true
	else:
		load_error="This build requires its matching R10 map adapters";return
	add_child(_geometry);_previous_environment=get_world_3d().environment
	var texture_optimizer:=preload("res://scripts/map_textures_r17.gd").new()
	statistics["mobile_textures_r17"]=texture_optimizer.apply(self)
	if not OS.get_cmdline_user_args().has("--archive-original-art-r14"):statistics["art_repair_r14"]=preload("res://scripts/map_art_repair_r14.gd").apply(_geometry,map_id)
	var surface_repair=preload("res://scripts/map_art_r17.gd").new()
	statistics["art_r17"]=surface_repair.apply(_geometry,map_id,str(_meta.get("cache_fingerprint","")))
	statistics["wood_panels_r17"]=surface_repair.wood_panels(_details_r8)
	statistics["art_r19"]=preload("res://scripts/map_art_r19.gd").new().apply(_geometry,map_id,str(_meta.get("cache_fingerprint","")))
	semantics_r14=preload("res://scripts/map_semantics_r14.gd").new();add_child(semantics_r14);semantics_r14.setup(self)
	preload("res://scripts/map_fixtures_r18.gd").apply(self)
	statistics["hatch_backing_r21"]=preload("res://scripts/hatch_backing_r21.gd").apply(self)
	if FileAccess.file_exists("res://assets/data/map_lod_index_r16.json") and not OS.get_cmdline_user_args().has("--map-original-lods-r16"):statistics["mesh_lods_r16"]=preload("res://scripts/map_lods_r16.gd").apply(self)
	for panel in _details_r8.panels:
		var box:AABB=panel.box
		for attachment in panel.get("attachments",[]):box=box.merge(attachment.mesh.get_aabb())
		box=box.grow(.015)
		for z in range(floori(box.position.z/8),floori(box.end.z/8)+1):
			for x in range(floori(box.position.x/8),floori(box.end.x/8)+1):
				var cell:=Vector2i(x,z)
				if not _panel_spawn_cells_r9.has(cell):_panel_spawn_cells_r9[cell]=[]
				_panel_spawn_cells_r9[cell].append(box)
	for floor_box in _details_r8.get("spawn_floors_r9",[]):
		var body:=StaticBody3D.new();body.name="AuthoredLobbyFloorR9";body.collision_layer=1;body.collision_mask=0;body.add_to_group("climbable");body.add_to_group("construction_terrain");add_child(body)
		var shape:=CollisionShape3D.new();var box:=BoxShape3D.new();box.size=Vector3(floor_box.size.x,maxf(.12,floor_box.size.y),floor_box.size.z);shape.shape=box;body.add_child(shape);body.position=floor_box.get_center()
	var span:=Vector3(float(_meta.solid_max[0])-_meta.solid_min[0],float(_meta.solid_max[1])-_meta.solid_min[1],float(_meta.solid_max[2])-_meta.solid_min[2]);var far_distance:float=maxf(2000,span.length()*1.5+200)
	for camera in [_game.player.xr_camera,_game.player.desktop_camera]:camera.far=far_distance
	statistics["camera_far_r9"]=far_distance
	# 1004_RENDER_ONLY_BEGIN: measured high-geometry maps; no physics changes.
	if not OS.get_cmdline_user_args().has("--archive-original-materials"):
		statistics["material_mips1004"]=preload("res://scripts/archive_materials1004.gd").apply(_geometry)
	# 1004_RENDER_ONLY_END
	# R23: no leaf colliders; distant tree crowns draw one leaf card in three.
	statistics["foliage_r23"]=preload("res://scripts/foliage_r23.gd").apply(_geometry,map_id)
	if map_id=="archive:alfa-aglae-souza":_alpha_walls_r17()
	if map_id=="archive:aftermath-light-vs-dark-gods-badbar":_safe_paths_r18()
	for body in _geometry.find_children("*","StaticBody3D",true,false):
		if not body.is_in_group("climbable"):_floor_exclusions.append(body.get_rid())
	_environment=Environment.new();_environment.background_mode=Environment.BG_COLOR;_environment.background_color=Color("9db6c6");_environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;_environment.ambient_light_color=Color("c5d5e2");_environment.ambient_light_energy=.8;_environment.tonemap_mode=Environment.TONE_MAPPER_FILMIC
	if "Moon" in str(_meta.get("template","")):_environment.background_color=Color("101a2b");_environment.ambient_light_energy=.6
	get_world_3d().environment=_environment
	var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-53,-28,0);sun.light_energy=1.0;sun.shadow_enabled=false;add_child(sun)
	var center:Array=_meta.center;spawn=global_position+Vector3(center[0],center[1],center[2])+Vector3.UP*2
	game.world.hide();game.active_sandbox_map=self;game.player.set_meta("archive_loading_r17",get_instance_id());game.player.global_position=spawn;game.player.reset_motion();_old_ammo=game.ammo_pickups.boxes.duplicate();game._begin_chapter();game.training.reset();_settle.call_deferred()
func _settle()->void:
	if not is_inside_tree() or not is_instance_valid(_game) or _game.active_sandbox_map!=self:return
	await get_tree().physics_frame
	if not is_instance_valid(self) or not is_inside_tree() or not is_instance_valid(_game) or _game.active_sandbox_map!=self:return
	var low:Array=_meta.solid_min;var high:Array=_meta.solid_max;var top=float(high[1])+5;var bottom=float(low[1])-5
	for row in _meta.get("recorded_spawns",[]):
		if not row is Array or row.size()!=3:continue
		var p=Vector3(float(row[0]),float(row[1]),float(row[2]))+global_position
		if not p.is_finite():continue
		var at=_floor(p+Vector3.UP*2,5.0)
		if at.is_finite() and not _outside(at) and _room(at):_append_unique(spawn_points,at+Vector3.UP*.12,1)
	var bounds_low:Array=_meta.bounds_min;var bounds_high:Array=_meta.bounds_max
	var min_x=maxf(float(low[0]),float(bounds_low[0])+.6);var max_x=minf(float(high[0]),float(bounds_high[0])-.6);var min_z=maxf(float(low[2]),float(bounds_low[2])+.6);var max_z=minf(float(high[2]),float(bounds_high[2])-.6)
	var width=max_x-min_x;var depth=max_z-min_z
	if width<=0 or depth<=0:load_error="No usable intersection of solid geometry and play bounds";_finish_relocation_r17();return
	for z in 19:
		for x in 19:
			var p=global_position+Vector3(min_x+width*(x+.5)/19.0,top,min_z+depth*(z+.5)/19.0)
			var at=_floor(p,top-bottom)
			if at.is_finite() and not _outside(at) and _room(at):loot_points.append(at+Vector3.UP*.6)
	if spawn_points.is_empty():
		spawn_origin="floor-grid fallback"
		var focus:Array=_meta.center;var center_point=global_position+Vector3(focus[0],focus[1],focus[2]);var preferred=loot_points.duplicate()
		preferred.sort_custom(func(a:Vector3,b:Vector3):return a.distance_squared_to(center_point)<b.distance_squared_to(center_point))
		for p in preferred:
			_append_unique(spawn_points,p-Vector3.UP*.48,2)
			if spawn_points.size()>=16:break
	if loot_points.is_empty():
		for p in spawn_points:loot_points.append(p+Vector3.UP*.48)
	if spawn_points.is_empty() or loot_points.is_empty():load_error="No floor/headroom-validated spawn or loot sites";_finish_relocation_r17();return
	_separate_spawns_r8()
	var native_match:Array=semantics_r14.validated_spawns("deathmatch");var native_warmup:Array=semantics_r14.validated_spawns("warmup")
	# A valid off-arena waiting hub never expands the deathmatch spawn bounds.
	native_match=native_match.filter(func(site:Dictionary):
		var point:Vector3=site.position-global_position
		return point.x>=float(bounds_low[0]) and point.x<=float(bounds_high[0]) and point.z>=float(bounds_low[2]) and point.z<=float(bounds_high[2]))
	if not native_match.is_empty():
		spawn_points.clear()
		for site in native_match:_append_unique(spawn_points,site.position,1.0)
		spawn=spawn_points[0];spawn_origin="native black ring/person markers"
	if not native_warmup.is_empty():warmup_spawn_r8=native_warmup[0].position
	statistics["native_deathmatch_spawns_r14"]=native_match.size();statistics["native_warmup_spawns_r14"]=native_warmup.size()
	statistics["match_spawns_r8"]=spawn_points.size();statistics["warmup_spawn_r8"]=str(warmup_spawn_r8-global_position)
	if magnum_only_r9():
		var center:=Vector3.ZERO
		for at in spawn_points:center+=at
		center/=spawn_points.size();fall_level_r9=center.y-20
		var floor:Vector3=_floor(center+Vector3.UP*6,12)
		center_respawn_r9=(floor if floor.is_finite() else spawn)+Vector3.UP*2.5
	var explore_game:bool=not _game.network_bridge.live() and _game.front_menu.exploration_spawn_r16=="game"
	_game.player.global_position=spawn if explore_game else warmup_spawn_r8;_finish_relocation_r17();_game.player.reset_motion();map_ready=true
	gameplay_r8=preload("res://scripts/archive_gameplay_r8.gd").new();add_child(gameplay_r8);gameplay_r8.setup(self,_details_r8)
	preload("res://scripts/archive_overview_r8.gd").capture(self)
	_place_loot();_game.mission_label.text=str(_meta.title)+" — LOCAL ARCHIVE TEST"
	preload("res://scripts/map_runtime_rules_r14.gd").apply(_game,map_id)
func _floor(from:Vector3,depth:float)->Vector3:
	var query=PhysicsRayQueryParameters3D.create(from,from-Vector3.UP*depth,1,_floor_exclusions);var hit=get_world_3d().direct_space_state.intersect_ray(query)
	return hit.position if not hit.is_empty() and hit.normal.y>.7 else Vector3.INF
func _finish_relocation_r17()->void:
	if is_instance_valid(_game) and is_instance_valid(_game.player) and int(_game.player.get_meta("archive_loading_r17",0))==get_instance_id():
		_game.player.remove_meta("archive_loading_r17");_game.player.restore_calibration_r17()
		if is_instance_valid(_game.telemetry):_game.telemetry.record_event("map_calibration_r17",{"map":map_id,"initialized":_game.player.vertical_combat_initialized,"baseline":_game.player.vertical_combat_world_baseline_m,"origin_lift":_game.player.xr_origin.position.y})
func _append_unique(points:Array[Vector3],point:Vector3,spacing:float)->void:
	for p in points:
		if p.distance_to(point)<spacing:return
	points.append(point)
func _room(point:Vector3)->bool:
	if not map_ready:
		var local:Vector3=point-global_position;var cell:=Vector2i(floori(local.x/8),floori(local.z/8));var body:=AABB(local+Vector3(-.3,.02,-.3),Vector3(.6,_game.player.standing_clearance_r18(),.6))
		for z in range(-1,2):
			for x in range(-1,2):
				for panel in _panel_spawn_cells_r9.get(cell+Vector2i(x,z),[]):
					if body.intersects(panel):return false
	return super._room(point) and not _hazard(point+Vector3.UP*.1) and not _hazard(point+Vector3.UP*.9) and not _hazard(point+Vector3.UP*1.7)
func _place_loot()->void:
	if not map_ready or loot_points.is_empty():return
	var inventory:Node=_game.weapon_inventory
	if magnum_only_r9():
		for id in inventory.pickups.keys():
			if _game.weapon.catalog_index(id)!=7:
				inventory.pickups[id].queue_free();inventory.pickups.erase(id)
		if not _game.network_bridge.live():
			inventory.owned.assign([7]);_game.weapon.selected=7;_game.weapon.is_holstered=false;_game.weapon._apply_selection()
	var ids:Array=inventory.pickups.keys();ids.sort();var i=0
	for id in ids:
		var prop:Node3D=inventory.pickups[id]
		if prop.has_meta("archive_site_r8"):continue
		prop.global_position=loot_points[(i*17)%loot_points.size()];inventory.review_positions[id]=prop.global_position;inventory._settle_pickup_id.call_deferred(prop.get_instance_id());i+=1
	for prop in _ammo:
		if is_instance_valid(prop):_game.ammo_pickups.boxes.erase(prop);prop.queue_free()
	_ammo.clear()
	for prop in _old_ammo:
		if is_instance_valid(prop):prop.hide()
	for k in 72:
		var box:Node3D=_game.ammo_pickups.spawn_box(_game.ammo_pickups.families[k%6],loot_points[(k*13+5)%loot_points.size()]);_ammo.append(box)
	for k in _game.field_consumables.pickups.size():_game.field_consumables.pickups[k].global_position=loot_points[(k*19+11)%loot_points.size()]
	if is_instance_valid(gameplay_r8):
		var missing=false
		for indices in gameplay_r8.cache_ids.values():
			for index in indices:
				if index>=_game.weapon.weapons.size() or not inventory.pickups.has(index):missing=true;break
		if missing:gameplay_r8.reset_loot()
func _separate_spawns_r8()->void:
	var ordered:Array=spawn_points.duplicate();ordered.sort_custom(func(a:Vector3,b:Vector3):return a.y<b.y)
	var median:float=ordered[ordered.size()/2].y
	var lobby:Array=ordered.filter(func(at:Vector3):return at.y>median+20 and safe_spawn_r8(at))
	if spawn_origin!="recorded":lobby.clear() # A tall roof found by the fallback grid is not an authored lobby.
	var match_sites:Array=ordered.filter(func(at:Vector3):return at.y<=median+20 and safe_spawn_r8(at))
	if not match_sites.is_empty():spawn_points.assign(match_sites)
	spawn=spawn_points[0]
	if not lobby.is_empty():warmup_spawn_r8=lobby[0]
	else:
		var center:Array=_meta.center;var focus:=global_position+Vector3(center[0],center[1],center[2]);var central:Array=spawn_points.duplicate();central.sort_custom(func(a:Vector3,b:Vector3):return a.distance_squared_to(focus)<b.distance_squared_to(focus));warmup_spawn_r8=central[0]
	var floors:Array=_details_r8.get("spawn_floors_r9",[])
	if not floors.is_empty():
		floors.sort_custom(func(a:AABB,b:AABB):return a.end.y>b.end.y)
		var floor_box:AABB=floors[0];var center:Vector3=floor_box.get_center();center.y=floor_box.end.y+.12
		if global_position.y+floor_box.end.y>median+20:
			var lobby_sites:Array=ordered.filter(func(at:Vector3):return floor_box.grow(.6).has_point(at-global_position) and safe_spawn_r8(at))
			warmup_spawn_r8=lobby_sites[0] if not lobby_sites.is_empty() else global_position+center
			var arena:Array=ordered.filter(func(at:Vector3):return at.y<global_position.y+floor_box.position.y-5 and safe_spawn_r8(at))
			if not arena.is_empty():spawn_points.assign(arena);spawn=spawn_points[0]
	statistics["match_spawns_r8"]=spawn_points.size();statistics["warmup_spawn_r8"]=str(warmup_spawn_r8-global_position)
func _outside(point:Vector3)->bool:
	var bridge:Node=_game.network_bridge
	var warmup:bool=not bridge.live() or bridge.net.stage in ["lobby","countdown"] or (bridge.net.stage=="loading" and bridge.net.loading_target=="lobby")
	if warmup and is_instance_valid(semantics_r14) and semantics_r14.warmup_contains(point):return false
	var launch:Node=_game.network_bridge.launch_r12
	if is_instance_valid(launch) and launch.owner_map==self and (launch.staging(point) or launch.authorized_flight(point)):return false
	var p=point-global_position;var low:Array=_meta.bounds_min;var high:Array=_meta.bounds_max;var solid_high:Array=_meta.solid_max
	var ceiling:float=maxf(float(solid_high[1])+100,launch.ceiling()-global_position.y if is_instance_valid(launch) and launch.owner_map==self else -INF)
	return p.y<float(_meta.solid_min[1])-25 or p.y>ceiling or (map_id!="archive:alfa-aglae-souza" and (p.x<float(low[0])-10 or p.x>float(high[0])+10 or p.z<float(low[2])-10 or p.z>float(high[2])+10))
func _physics_process(delta:float)->void:
	super._physics_process(delta)
	if not map_ready or not is_instance_valid(_game) or _game.front_end_active:return
	_hazard_clock+=delta
	if _hazard_clock<.15:return
	_hazard_clock=0
	var bridge:Node=_game.network_bridge
	if magnum_only_r9():
		if bridge.playing() and bridge.net.server:
			for id in bridge.net.poses.keys():
				if bridge.net.players.has(id) and bridge.net.players[id].life=="alive" and bridge.net.poses[id].root.origin.y<fall_level_r9:
					_fall_spawning_r9[id]=true;bridge.net._spawn(id);_fall_spawning_r9.erase(id);bridge.net._publish()
		elif not bridge.live() and _game.player.global_position.y<fall_level_r9:_game.player.global_position=center_respawn_r9;_game.player.reset_motion()
	if bridge.playing():
		if bridge.net.server:
			var changed=false
			for id in bridge.net.poses:
				if bridge.net.players.has(id) and bridge.net.players[id].life=="alive" and _hazard(bridge.net.poses[id].root.origin+Vector3.UP*.8):bridge.net._spawn(id);changed=true
			if changed:bridge.net._publish()
	elif _hazard(_game.player.global_position+Vector3.UP*.8):_game.player.global_position=spawn;_game.player.reset_motion()
func _hazard(point:Vector3)->bool:
	var launch:Node=_game.network_bridge.launch_r12
	if is_instance_valid(launch) and launch.owner_map==self and (launch.staging(point) or launch.authorized_flight(point)):return false
	var q=PhysicsPointQueryParameters3D.new();q.position=point;q.collision_mask=32;q.collide_with_areas=true;q.collide_with_bodies=false
	return not get_world_3d().direct_space_state.intersect_point(q,1).is_empty()

func magnum_only_r9()->bool:return map_id in ["archive:magnum-chaosvr","archive:magnum-3-chaosvr"]
func safe_spawn_r8(point:Vector3)->bool:
	if not point.is_finite() or _outside(point):return false
	var floor_point:Vector3=_floor(point+Vector3.UP*.6,1.0)
	return floor_point.is_finite() and absf(floor_point.y-point.y)<.35 and _room(floor_point)

func leave()->void:
	_finish_relocation_r17();super.leave()
func _exit_tree()->void:
	_finish_relocation_r17()

func _alpha_walls_r17()->void:
	var low:Array=_meta.bounds_min;var high:Array=_meta.bounds_max
	var bottom:float=float(_meta.solid_min[1])-30;var top:float=maxf(1500,float(_meta.solid_max[1])+500)
	var center:=Vector3((float(low[0])+float(high[0]))*.5,(bottom+top)*.5,(float(low[2])+float(high[2]))*.5)
	var width:float=float(high[0])-float(low[0]);var depth:float=float(high[2])-float(low[2])
	for side in 4:
		var body:=StaticBody3D.new();body.name="AlphaSolidBoundaryR17";body.collision_layer=1;body.collision_mask=0;body.set_meta("alpha_boundary_r17",true);add_child(body)
		var shape:=CollisionShape3D.new();var box:=BoxShape3D.new()
		body.position=center
		if side<2:
			box.size=Vector3(2,top-bottom,depth+4);body.position.x=float(low[0])-1 if side==0 else float(high[0])+1
		else:
			box.size=Vector3(width+4,top-bottom,2);body.position.z=float(low[2])-1 if side==2 else float(high[2])+1
		shape.shape=box;body.add_child(shape)
	statistics["alpha_solid_boundary_r17"]=true

func _safe_paths_r18()->void:
	var count=0
	for area in _geometry.find_children("*","Area3D",true,false):
		if not area.is_in_group("archive_killzone"):continue
		for child in area.get_children():
			if not child is CollisionShape3D or child.shape==null:continue
			var box:AABB=child.shape.get_debug_mesh().get_aabb()
			# Source ground ribbons occupy85..91m. Keep the separate211m lobby kill slab.
			if box.position.y<80 or box.end.y>96:continue
			child.disabled=true
			var body:=StaticBody3D.new();body.name="AftermathSafePathR18_"+str(count);body.collision_layer=1;body.collision_mask=0;body.add_to_group("climbable");body.add_to_group("construction_terrain");_geometry.add_child(body)
			var shape:=CollisionShape3D.new();shape.shape=child.shape;body.add_child(shape);shape.global_transform=child.global_transform;count+=1
	statistics["after_math_safe_paths_r18"]=count
