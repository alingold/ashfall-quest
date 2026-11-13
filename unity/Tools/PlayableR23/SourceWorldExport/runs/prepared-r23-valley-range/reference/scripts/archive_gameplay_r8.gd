extends Node3D
## Map-owned functional content, activated near players and registered with existing authoritative item systems.
var map:Node
var game:Node
var sites:Array[Vector3]=[]
var cache_ids:Dictionary={}
var active_sites:Dictionary={}
var pads:Array[Vector3]=[]
var boards:Array[Label3D]=[]
var _clock=0.0
var _launch_cooldown=0.0
var _rack_generation=-1
var _panels_r8:Dictionary={}
var _broken_count_r8=-1
var display_ids_r9:Array[int]=[]
var _weapon_boards_r9:Array=[]
func setup(owner_map:Node,details:Dictionary)->void:
	map=owner_map;game=map._game;name="ArchiveGameplayR8"
	_weapon_boards_r9=preload("res://scripts/map_weapon_boards_r18.gd").merge(details.get("weapon_boards_r9",[]),map.map_id,str(map._meta.get("cache_fingerprint","")))
	var cache_sites:Array=details.weapon_sites.duplicate()
	if is_instance_valid(map.semantics_r14):
		for site in map.semantics_r14.weapon_sites_r17:
			var duplicate=false
			for old in cache_sites:
				if site.distance_squared_to(old)<1:duplicate=true;break
			if not duplicate:cache_sites.append(site)
	map.statistics["recovered_cache_sites_r17"]=cache_sites.size()-details.weapon_sites.size()
	for local in cache_sites:
		var at:Vector3=map._floor(map.global_position+local+Vector3.UP*1.1,4.0)
		if at.is_finite() and not map._outside(at) and not map._hazard(at+Vector3.UP*.5):sites.append(at+Vector3.UP*.65)
	var unresolved_pads=0
	for local in details.jump_pads:
		var floor:Vector3=map._floor(map.global_position+local+Vector3.UP*.6,6)
		if not floor.is_finite() or map._outside(floor):unresolved_pads+=1;continue
		var duplicate=false
		for old in pads:
			if old.distance_to(floor)<2.8:duplicate=true;break
		if not duplicate:pads.append(floor)
	map.statistics["unresolved_source_pads_r10"]=unresolved_pads
	var batch_panels:Array=[]
	for i in details.panels.size():
		var panel:=preload("res://scripts/archive_panel_r8.gd").new();map.add_child(panel);panel.setup(map,str(i),details.panels[i]);panel.set_physics_process(false)
		batch_panels.append(panel)
		var center:Vector3=map.global_position+details.panels[i].box.get_center();var cell:=Vector2i(floori(center.x/12),floori(center.z/12))
		if not _panels_r8.has(cell):_panels_r8[cell]=[]
		_panels_r8[cell].append(panel)
	if not OS.get_cmdline_user_args().has("--original-panels-r17"):
		var batcher=preload("res://scripts/archive_panel_batches_r17.gd").new();map.add_child(batcher);batcher.setup(map,batch_panels)
	# Official environments carry scenery, not original match loot entities. Their standable grid becomes real caches.
	if sites.is_empty():
		for i in range(0,map.loot_points.size(),3):sites.append(map.loot_points[i])
	map.statistics["supplemental_cache_sites_r18"]=preload("res://scripts/sparse_loot_r18.gd").add_sites(map,sites)
	map.statistics["source_jump_pads_r9"]=pads.size()
	if is_instance_valid(map.semantics_r14) and not map.semantics_r14.jump_pads.is_empty():pads.clear()
	if map.map_id in ["archive:classic-bigbox-vr","archive:phoenix-bigbox-vr"] and pads.is_empty() and map.semantics_r14.jump_pads.is_empty():_restore_launch_pads()
	map.statistics["weapon_cache_sites_r8"]=sites.size();map.statistics["functional_jump_pads_r8"]=pads.size()
	map.statistics["functional_jump_pads_r14"]=pads.size()+map.semantics_r14.jump_pads.size()
	var score_rows:Array=details.get("score_faces_r9",[]).duplicate()
	score_rows.append_array(preload("res://scripts/archive_score_surfaces_r18.gd").faces(map._geometry,map.map_id,str(map._meta.get("cache_fingerprint",""))))
	score_rows.append_array(preload("res://scripts/archive_score_surfaces_r19.gd").faces(map._geometry,map.map_id,str(map._meta.get("cache_fingerprint",""))))
	if not score_rows.is_empty():
		var scoreboard:=preload("res://scripts/archive_world_score_r9.gd").new();map.add_child(scoreboard);scoreboard.setup(game,score_rows)
	activate_near([map.warmup_spawn_r8,map.spawn],true)
	_make_weapon_boards_r9()
func _restore_launch_pads()->void:
	var mat:=StandardMaterial3D.new();mat.albedo_color=Color("e76211");mat.metallic=.35;mat.roughness=.65;mat.emission_enabled=true;mat.emission=Color("e65e10");mat.emission_energy_multiplier=.35
	var inset:=StandardMaterial3D.new();inset.albedo_color=Color("252c32");inset.roughness=.8
	for point in map.loot_points:
		var occupied=false
		for at in pads:
			if point.distance_to(at)<150:occupied=true;break
		if occupied:continue
		# Keep pads away from the cache center and reject slopes/walls across the entire disc.
		var at:Vector3=map._floor(point+Vector3(4,1,4),3)
		if not at.is_finite():continue
		if not map.safe_spawn_r8(at+Vector3.UP*.12):continue
		var query:=PhysicsRayQueryParameters3D.create(at+Vector3.UP*.3,at+Vector3.UP*25,1)
		if not get_world_3d().direct_space_state.intersect_ray(query).is_empty():continue
		var flat=true
		for edge in 8:
			var rim:Vector3=at+Vector3(cos(TAU*edge/8)*1.8,0,sin(TAU*edge/8)*1.8);var floor:Vector3=map._floor(rim+Vector3.UP*1.5,3)
			if not floor.is_finite() or absf(floor.y-at.y)>.06 or map._outside(floor):flat=false;break
			var side:=PhysicsRayQueryParameters3D.create(at+Vector3.UP*.3,rim+Vector3.UP*.3,1)
			if not get_world_3d().direct_space_state.intersect_ray(side).is_empty():flat=false;break
		if not flat:continue
		pads.append(at)
		var visual:=MeshInstance3D.new();visual.name="FunctionalJumpPadR8";var disc:=CylinderMesh.new();disc.top_radius=1.55;disc.bottom_radius=1.65;disc.height=.08;disc.radial_segments=8;visual.mesh=disc;visual.material_override=mat;map.add_child(visual);visual.global_position=at+Vector3.UP*.055
		var face:=MeshInstance3D.new();var inner:=CylinderMesh.new();inner.top_radius=1.30;inner.bottom_radius=1.30;inner.height=.012;inner.radial_segments=8;face.mesh=inner;face.material_override=inset;visual.add_child(face);face.position.y=.048
		var arrow:=Label3D.new();arrow.text="⇧";arrow.font_size=100;arrow.pixel_size=.016;arrow.rotation_degrees.x=-90;arrow.modulate=Color("ff7c21");visual.add_child(arrow);arrow.position.y=.062
		if pads.size()>=12:break
	map.statistics["replacement_jump_pads_r9"]=pads.size()
func reset_loot()->void:
	cache_ids.clear();active_sites.clear();display_ids_r9.clear();activate_near([game.player.global_position,map.warmup_spawn_r8,map.spawn],true);_make_weapon_boards_r9()
func _make_weapon_boards_r9()->void:
	var previous=get_node_or_null("WeaponWallRacksR18")
	if previous!=null:
		for index in previous.ids:
			if game.weapon_inventory.pickups.has(index):game.weapon_inventory.pickups[index].queue_free();game.weapon_inventory.pickups.erase(index)
		remove_child(previous);previous.queue_free()
	display_ids_r9.clear()
	if _weapon_boards_r9.is_empty():return
	var racks=preload("res://scripts/weapon_wall_rack_r18.gd").new();add_child(racks);racks.setup(map,_weapon_boards_r9,not game.network_bridge.live() or game.network_bridge.net.server);display_ids_r9.assign(racks.ids)
	map.statistics["mounted_red_board_guns_r18"]=racks.mounts
	if game.network_bridge.playing() and not racks.ids.is_empty():game.network_bridge.register_archive_loot_r8(racks.ids)
func activate_near(players:Array,initial:bool=false)->void:
	var bridge:Node=game.network_bridge
	if bridge.live() and not bridge.net.server:return
	var added:Array[int]=[];var activated=0
	for i in sites.size():
		if active_sites.has(i):continue
		var close=false
		for point in players:
			if sites[i].distance_squared_to(point)<(35.0 if initial else 65.0)* (35.0 if initial else 65.0):close=true;break
		if not close:continue
		active_sites[i]=true;var random:=RandomNumberGenerator.new();random.seed=absi(str(map.map_id).hash())+i*7907
		var ids:Array[int]=[];var chosen:Array[int]=[];var melee=0;var explosives=0
		for slot in 6:
			var pool:Array=[7] if map.magnum_only_r9() else game.weapon.ACTIVE_WEAPON_INDICES.filter(func(id):return not chosen.has(id) and (melee==0 or id not in [14,15,16,29]) and (explosives==0 or id not in [26,27,28]))
			var catalog:int=pool[random.randi_range(0,pool.size()-1)];chosen.append(catalog)
			if catalog in [14,15,16,29]:melee+=1
			if catalog in [26,27,28]:explosives+=1
			var stars:int=1+int(random.randf()>.45)+int(random.randf()>.75)+int(random.randf()>.91)+int(random.randf()>.98)
			var index:int=game.weapon.create_instance(catalog,stars,true);var point:Vector3=sites[i]
			for radius in [1.05,.65,.3]:
				var desired:Vector3=sites[i]+Vector3(cos(TAU*slot/6)*radius,0,sin(TAU*slot/6)*radius)
				var floor:Vector3=map._floor(desired+Vector3.UP*.7,2.8)
				if not floor.is_finite() or map._outside(floor):continue
				desired.y=floor.y+.65
				var query:=PhysicsRayQueryParameters3D.create(sites[i]+Vector3.UP*.5,desired,1)
				if get_world_3d().direct_space_state.intersect_ray(query).is_empty():point=desired;break
			game.weapon_inventory._spawn_pickup(index,point);game.weapon_inventory.pickups[index].set_meta("archive_site_r8",i);ids.append(index);added.append(index)
		cache_ids[i]=ids;activated+=1
		if activated>=(12 if initial else 2):break
	if bridge.playing() and not added.is_empty():bridge.register_archive_loot_r8(added)
func _physics_process(delta:float)->void:
	_launch_cooldown=maxf(0,_launch_cooldown-delta)
	if not map.map_ready or game.front_end_active:return
	if _launch_cooldown<=0 and game.player.input_enabled:
		for at in pads:
			var offset:Vector3=game.player.global_position-at
			if Vector2(offset.x,offset.z).length()<2.5 and offset.y>=-.4 and offset.y<1.2 and game.player.velocity.y<4:
				if not game.player.launch_from_pad_r10(45.0):continue
				_launch_cooldown=1.2
				var voice:=AudioStreamPlayer3D.new();add_child(voice);voice.global_position=at;voice.stream=load("res://assets/audio/sfx/jump_pad_r8.wav");voice.bus="SFX";voice.volume_db=-5;voice.max_distance=40;voice.finished.connect(voice.queue_free);voice.play();break
	_clock+=delta
	if _clock<.2:return
	_clock=0
	var players:Array=[game.player.global_position];var net:Node=game.network_bridge.net
	if net.active and net.server:
		for pose in net.poses.values():players.append(pose.root.origin)
	if net.active and _broken_count_r8!=net.archive_broken_r8.size():
		_broken_count_r8=net.archive_broken_r8.size()
		for group in _panels_r8.values():
			for panel in group:
				var broken:bool=net.archive_broken_r8.has(panel.key)
				if broken!=panel._broken:panel._apply(broken)
	var seen:Dictionary={}
	for point in players:
		var cell:=Vector2i(floori(point.x/12),floori(point.z/12))
		for z in range(-1,2):
			for x in range(-1,2):
				var key:Vector2i=cell+Vector2i(x,z)
				if seen.has(key):continue
				seen[key]=true
				for panel in _panels_r8.get(key,[]):panel._physics_process(.2)
	activate_near(players)
