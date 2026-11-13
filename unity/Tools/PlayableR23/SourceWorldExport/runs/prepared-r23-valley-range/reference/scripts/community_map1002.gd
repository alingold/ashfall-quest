extends Node3D
## Original generic runtime adapter; all imported geometry is optional local-only data.
const Catalog=preload("res://scripts/community_catalog1002.gd")
const MAP_ID="community_local"
var spawn=Vector3.ZERO
var spawn_points:Array[Vector3]=[]
var loot_points:Array[Vector3]=[]
var map_ready=false
var statistics:Dictionary={}
var _game:Node
var _previous_environment:Environment
var _environment:Environment
var _old_ammo:Array=[]
var _ammo:Array=[]
var _refresh_pending=false
var _geometry:Node3D
var _meta:Dictionary
func enter(game:Node)->void:
	_game=game;name="LocalCommunityMap";position=Vector3(0,0,4800);_meta=Catalog.manifest();statistics=_meta.duplicate(true)
	_geometry=load(Catalog.ROOT+"map.scn").instantiate();add_child(_geometry)
	_previous_environment=get_world_3d().environment
	_environment=Environment.new();_environment.background_mode=Environment.BG_COLOR;_environment.background_color=Color("9db6c6");_environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;_environment.ambient_light_color=Color("c5d5e2");_environment.ambient_light_energy=.8;_environment.tonemap_mode=Environment.TONE_MAPPER_FILMIC
	get_world_3d().environment=_environment
	var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-53,-28,0);sun.light_energy=1.1;sun.shadow_enabled=false;add_child(sun)
	var recorded:Array=_meta.spawns;spawn=global_position+Vector3(recorded[0][0],recorded[0][1],recorded[0][2])
	game.world.hide();game.active_sandbox_map=self;game.player.global_position=spawn;game.player.reset_motion();_old_ammo=game.ammo_pickups.boxes.duplicate();game._begin_chapter();game.training.reset()
	_settle.call_deferred()
func _settle()->void:
	if not is_inside_tree() or not is_instance_valid(_game) or _game.active_sandbox_map!=self:return
	await get_tree().physics_frame
	if not is_instance_valid(self) or not is_inside_tree() or not is_instance_valid(_game) or _game.active_sandbox_map!=self:return
	for row in _meta.spawns:
		var point=global_position+Vector3(row[0],row[1],row[2]);var floor_point=_floor(point+Vector3.UP*2,10)
		if floor_point.is_finite() and _room(floor_point):spawn_points.append(floor_point+Vector3.UP*.12)
	# Deterministic broad distribution, avoiding narrow posts and blocked headroom.
	for z in range(-208,209,26):
		for x in range(-208,209,26):
			var point=_floor(global_position+Vector3(x,110,z),160)
			if point.is_finite() and _room(point):loot_points.append(point+Vector3.UP*.6)
	assert(not spawn_points.is_empty(),"No standable recorded spawn")
	spawn=spawn_points[0];_game.player.global_position=spawn;_game.player.reset_motion();map_ready=true
	_place_loot();_game.mission_label.text=str(_meta.title)+" — LOCAL COMMUNITY TEST"
func _floor(from:Vector3,depth:float)->Vector3:
	var hit=get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(from,from-Vector3.UP*depth,1))
	return hit.position if not hit.is_empty() and hit.normal.y>.7 else Vector3.INF
func _room(floor_point:Vector3)->bool:
	for offset in [Vector3(.4,0,0),Vector3(-.4,0,0),Vector3(0,0,.4),Vector3(0,0,-.4)]:
		var at=_floor(floor_point+offset+Vector3.UP*.6,1.2)
		if not at.is_finite() or absf(at.y-floor_point.y)>.3:return false
	var clearance:float=_game.player.standing_clearance_r18() if is_instance_valid(_game) else 1.95
	var shape=CapsuleShape3D.new();shape.radius=.3;shape.height=clearance
	var query=PhysicsShapeQueryParameters3D.new();query.shape=shape;query.transform=Transform3D(Basis.IDENTITY,floor_point+Vector3.UP*(clearance*.5+.02));query.collision_mask=1
	return get_world_3d().direct_space_state.intersect_shape(query,1).is_empty()
func refresh_loot()->void:
	if _refresh_pending:return
	_refresh_pending=true;_refresh.call_deferred()
func _refresh()->void:
	if not is_inside_tree() or not is_instance_valid(_game) or _game.active_sandbox_map!=self:return
	await get_tree().physics_frame
	if not is_instance_valid(self) or not is_inside_tree() or not is_instance_valid(_game) or _game.active_sandbox_map!=self:return
	if map_ready:_place_loot()
	_refresh_pending=false
func _place_loot()->void:
	if not map_ready or loot_points.is_empty():return
	var inventory:Node=_game.weapon_inventory;var ids:Array=inventory.pickups.keys();ids.sort();var i=0
	for id in ids:
		var prop:Node3D=inventory.pickups[id];prop.global_position=loot_points[(i*17)%loot_points.size()];inventory.review_positions[id]=prop.global_position;inventory._settle_pickup_id.call_deferred(prop.get_instance_id());i+=1
	for prop in _ammo:
		if is_instance_valid(prop):
			_game.ammo_pickups.boxes.erase(prop);prop.queue_free()
	_ammo.clear()
	for prop in _old_ammo:
		if is_instance_valid(prop):prop.hide()
	for cache in 12:
		var at=loot_points[(cache*23+5)%loot_points.size()]
		for category in 6:
			var box:Node3D=_game.ammo_pickups.spawn_box(_game.ammo_pickups.families[category],at+Vector3((category%3)*.55,0,floori(float(category)/3)*.55));_ammo.append(box)
	for k in _game.field_consumables.pickups.size():_game.field_consumables.pickups[k].global_position=loot_points[(k*19+11)%loot_points.size()]
func _physics_process(_delta:float)->void:
	if not map_ready or not is_instance_valid(_game) or _game.front_end_active:return
	var bridge:Node=_game.network_bridge
	if bridge.playing():
		if bridge.net.server:
			var recovered=false
			for id in bridge.net.poses:
				if bridge.net.players.has(id) and bridge.net.players[id].life=="alive" and _outside(bridge.net.poses[id].root.origin):bridge.net._spawn(id);recovered=true
			if recovered:bridge.net._publish()
		return
	if _outside(_game.player.global_position):
		_game.player.global_position=spawn;_game.player.reset_motion()
func _outside(point:Vector3)->bool:
	var launch:Node=_game.network_bridge.launch_r12
	if is_instance_valid(launch) and launch.owner_map==self and (launch.staging(point) or launch.authorized_flight(point)):return false
	var local=point-global_position
	var low:Array=_meta.get("bounds_min",[-450,0,-510]);var high:Array=_meta.get("bounds_max",[460,80,500])
	var ceiling:float=maxf(350,launch.ceiling()-global_position.y if is_instance_valid(launch) and launch.owner_map==self else -INF)
	return local.y < float(low[1])-25 or local.x<float(low[0])-5 or local.x>float(high[0])+5 or local.z<float(low[2])-5 or local.z>float(high[2])+5 or local.y>ceiling
func leave()->void:
	map_ready=false;set_physics_process(false)
	get_world_3d().environment=_previous_environment
	for prop in _ammo:
		if is_instance_valid(prop):
			_game.ammo_pickups.boxes.erase(prop);prop.queue_free()
	for prop in _old_ammo:
		if is_instance_valid(prop):prop.show()
	_ammo.clear()

