extends Node3D
## Hash-bound recovery of native authoring assemblies, shared by every archived map.
## Health is a position/rate provider only. The session authority applies actual healing.
const Catalog=preload("res://scripts/archive_catalog1003.gd")
const Components=preload("res://scripts/archive_semantics_r8.gd")
static var _catalog:Dictionary={}
var map:Node
var data:Dictionary={}
var stations_r18:Node
var weapon_sites_r17:Array[Vector3]=[]
var jump_pads:Array[Dictionary]=[]
var health_pads:Array[Dictionary]=[]
var marker_visuals:Node3D
var removed_visual_triangles=0
var removed_collision_triangles=0
var _cooldowns:Dictionary={}
var _marker_cells:Dictionary={}
var _marker_keys:Dictionary={}
var _collision_cells:Dictionary={}
var _selectors_by_key:Dictionary={}
var _visible=true
var _clock=0.0
var _warmup_sites:Array[Dictionary]=[]
var _collision_vertices:Dictionary={}
var _collision_component_count=0
var _previous_feet_r16:=Vector3.INF
var _next_heal_diagnostic_r17=0.0
var _last_heal_rate_r17=0.0
static func vector(value:Array)->Vector3:return Vector3(float(value[0]),float(value[1]),float(value[2]))
static func catalog()->Dictionary:
	if _catalog.is_empty():
		var value=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_semantics_r14.json"))
		if value is Dictionary:_catalog=value
	return _catalog
static func for_map(map_id:String,fingerprint:String)->Dictionary:
	var row:Dictionary=catalog().get("maps",{}).get(Catalog.slug(map_id),{})
	return row if not fingerprint.is_empty() and str(row.get("cache_fingerprint",""))==fingerprint else {}
func setup(owner_map:Node)->void:
	map=owner_map;name="MapSemanticsR14";data=for_map(map.map_id,str(map._meta.get("cache_fingerprint","")))
	marker_visuals=Node3D.new();marker_visuals.name="OriginalSpawnMarkersR14";add_child(marker_visuals)
	if data.is_empty():set_physics_process(false);return
	for kind in ["jump","health"]:
		var source_index=0
		for source in data.get(kind,[]):
			var row:Dictionary=source.duplicate(true);row.station_key_r18=str(map.map_id).sha256_text().substr(0,12)+":"+str(source_index);source_index+=1;row.center=map.global_position+vector(source.center);row.normal=vector(source.normal).normalized()
			if map._outside(row.center):continue
			if kind=="jump":jump_pads.append(row)
			else:health_pads.append(row)
	for category in ["marker_parts","decorative_collision_parts"]:
		for original in data.get(category,[]):
			var selector:Dictionary=original.duplicate(false);selector.visual_marker=category=="marker_parts"
			var key:String=str(selector.key);_marker_keys[key]=true
			var cell:=Vector3i((vector(selector.center)/4).floor());var bucket:String=key+":"+str(cell)
			if not _marker_cells.has(bucket):_marker_cells[bucket]=[]
			_marker_cells[bucket].append(selector)
			if not _selectors_by_key.has(key):_selectors_by_key[key]=[]
			_selectors_by_key[key].append(selector)
	var cache_data=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_caches_r17.json")) if FileAccess.file_exists("res://assets/data/map_caches_r17.json") else {}
	var recovered:Dictionary=cache_data.get("maps",{}).get(Catalog.slug(map.map_id),{})
	if recovered.get("cache_fingerprint","")==str(map._meta.get("cache_fingerprint","")):
		for site in recovered.get("sites",[]):weapon_sites_r17.append(vector(site))
		for original in recovered.get("marker_parts",[]):
			var selector:Dictionary=original.duplicate(false);selector.visual_marker=true
			var key:String=str(selector.key);_marker_keys[key]=true
			var cell:=Vector3i((vector(selector.center)/4).floor());var bucket:String=key+":"+str(cell)
			if not _marker_cells.has(bucket):_marker_cells[bucket]=[]
			_marker_cells[bucket].append(selector)
			if not _selectors_by_key.has(key):_selectors_by_key[key]=[]
			_selectors_by_key[key].append(selector)
	_extract_markers()
	map.statistics["semantic_coverage_r14"]=data.get("coverage",{}).duplicate(true)
	map.statistics["marker_visual_triangles_r14"]=removed_visual_triangles
	map.statistics["marker_collision_triangles_r14"]=removed_collision_triangles
	set_markers_visible(not map._game.network_bridge.live())
	stations_r18=preload("res://scripts/healing_stations_r18.gd").new();add_child(stations_r18);stations_r18.setup(self)
func set_markers_visible(show:bool)->void:
	_visible=show
	if is_instance_valid(marker_visuals):marker_visuals.visible=show
func _physics_process(delta:float)->void:
	_clock+=delta
	if not is_instance_valid(map) or not map.map_ready:return
	var show:bool=not map._game.network_bridge.live()
	if show!=_visible:set_markers_visible(show)
	var player:Node=map._game.player
	if player.input_enabled:try_launch(player)
	if _clock>=_next_heal_diagnostic_r17:
		_next_heal_diagnostic_r17=_clock+2.0
		var feet:Vector3=player.get_feet_position_r16();var rate:float=healing_rate_at(feet)
		if (rate>0 or _last_heal_rate_r17>0) and is_instance_valid(map._game.telemetry):map._game.telemetry.record_event("healing_pad_r17",{"map":map.map_id,"feet":str(feet-map.global_position),"rate":rate,"health":map._game.health,"shield":map._game.shield,"initialized":player.vertical_combat_initialized,"hosted":map._game.network_bridge.live()})
		_last_heal_rate_r17=rate
func _matching(key:String,center:Vector3,triangles:int)->int:
	var cell:=Vector3i((center/4).floor())
	for x in range(-1,2):
		for y in range(-1,2):
			for z in range(-1,2):
				for part in _marker_cells.get(key+":"+str(cell+Vector3i(x,y,z)),[]):
					var count_matches:bool=int(part.triangles)==triangles
					# Import float welding can join the intersecting two12-triangle cross bars.
					if not part.visual_marker and int(part.triangles)==12 and triangles==24:count_matches=true
					if count_matches and center.distance_squared_to(vector(part.center))<.000016:return 1 if part.visual_marker else 2
	return 0
func _has_selector(key:String,box:AABB)->bool:
	for part in _selectors_by_key.get(key,[]):
		if box.grow(.01).has_point(vector(part.center)):return true
	return false
func _extract_markers()->void:
	if _marker_keys.is_empty():return
	var removed:Dictionary={};var geometry:Node3D=map._geometry
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		var material:Material=view.material_override
		if material==null or view.mesh==null or view.mesh.get_surface_count()!=1:continue
		var key:String=material.resource_name
		if not _marker_keys.has(key):continue
		var relative:Transform3D=geometry.global_transform.affine_inverse()*view.global_transform
		if not _has_selector(key,relative*view.mesh.get_aabb()):continue
		var groups:Dictionary=Components._components(view.mesh);var erase:Dictionary={};var collision_only:Dictionary={}
		for id in groups.rows:
			var part:Dictionary=groups.rows[id]
			var matched:int=_matching(key,relative*part.box.get_center(),int(part.triangles))
			if matched==1:erase[id]=true
			elif matched==2:collision_only[id]=true
		if erase.is_empty() and collision_only.is_empty():continue
		var arrays:Array=view.mesh.surface_get_arrays(0);var old:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array();var keep:=PackedInt32Array()
		var extracted:=SurfaceTool.new();extracted.begin(Mesh.PRIMITIVE_TRIANGLES)
		var component_ids:Dictionary={};var component_vertices:Dictionary={}
		for i in groups.triangle_roots.size():
			var cut:bool=erase.has(groups.triangle_roots[i])
			var collision_cut:bool=cut or collision_only.has(groups.triangle_roots[i])
			var component:int=-1
			if collision_cut:
				var id:int=groups.triangle_roots[i]
				if not component_ids.has(id):component_ids[id]=_collision_component_count;_collision_component_count+=1
				component=component_ids[id]
			for corner in 3:
				var index:int=old[i*3+corner] if not old.is_empty() else i*3+corner
				if collision_cut:
					var point:Vector3=relative*arrays[Mesh.ARRAY_VERTEX][index];var bin:=Vector3i((point*250).round());var tag:String=str(component)+":"+str(bin)
					if not component_vertices.has(tag):
						component_vertices[tag]=true
						if not _collision_vertices.has(bin):_collision_vertices[bin]=[]
						_collision_vertices[bin].append({"point":point,"component":component})
				if not cut:keep.append(index);continue
				if arrays[Mesh.ARRAY_NORMAL]!=null:extracted.set_normal(arrays[Mesh.ARRAY_NORMAL][index])
				if arrays[Mesh.ARRAY_TEX_UV]!=null:extracted.set_uv(arrays[Mesh.ARRAY_TEX_UV][index])
				if arrays[Mesh.ARRAY_COLOR]!=null:extracted.set_color(arrays[Mesh.ARRAY_COLOR][index])
				extracted.add_vertex(arrays[Mesh.ARRAY_VERTEX][index])
			if cut or collision_only.has(groups.triangle_roots[i]):
				var center:Vector3=relative*((groups.faces[i*3]+groups.faces[i*3+1]+groups.faces[i*3+2])/3)
				var cross:Vector3=(relative.basis*(groups.faces[i*3+1]-groups.faces[i*3])).cross(relative.basis*(groups.faces[i*3+2]-groups.faces[i*3]))
				var bin:=Vector3i((center*250).round())
				if not removed.has(bin):removed[bin]=[]
				removed[bin].append({"center":center,"normal":cross.normalized(),"area":cross.length()})
				if cut:removed_visual_triangles+=1
		if erase.is_empty():continue
		var mesh:=ArrayMesh.new()
		if not keep.is_empty():arrays[Mesh.ARRAY_INDEX]=keep;mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
		view.mesh=mesh
		var visual:=MeshInstance3D.new();visual.name="OriginalMarkerChunk_"+str(marker_visuals.get_child_count());visual.mesh=extracted.commit();visual.material_override=material;marker_visuals.add_child(visual,true);visual.global_transform=view.global_transform
	var occupied:Dictionary={}
	for bucket in removed.values():
		for triangle in bucket:occupied[Vector3i((triangle.center/4).floor())]=true
	for cell in occupied:
		for x in range(-1,2):
			for y in range(-1,2):
				for z in range(-1,2):_collision_cells[cell+Vector3i(x,y,z)]=true
	# Remove the exact same symbol faces from collision, including the humanoid statue.
	# Render/collision chunks have independent vertex quantization. Require coincident
	# centroids, normals AND areas; nearby floors and walls never match by position alone.
	for collision in geometry.find_children("*","CollisionShape3D",true,false):
		if not collision.shape is ConcavePolygonShape3D:continue
		var relative:Transform3D=geometry.global_transform.affine_inverse()*collision.global_transform
		var faces:PackedVector3Array=collision.shape.get_faces();var keep:=PackedVector3Array()
		for i in range(0,faces.size(),3):
			var center:Vector3=relative*((faces[i]+faces[i+1]+faces[i+2])/3)
			var cross:Vector3=(relative.basis*(faces[i+1]-faces[i])).cross(relative.basis*(faces[i+2]-faces[i]))
			if _removed_triangle(removed,center,cross.normalized(),cross.length()) or (_collision_cells.has(Vector3i((center/4).floor())) and _same_removed_component(relative*faces[i],relative*faces[i+1],relative*faces[i+2])):removed_collision_triangles+=1;continue
			keep.append(faces[i]);keep.append(faces[i+1]);keep.append(faces[i+2])
		if keep.size()==faces.size():continue
		if keep.is_empty():collision.disabled=true;continue
		var replacement:=ConcavePolygonShape3D.new();replacement.backface_collision=true;replacement.set_faces(keep);collision.shape=replacement
func _removed_triangle(removed:Dictionary,center:Vector3,normal:Vector3,area:float)->bool:
	if not _collision_cells.has(Vector3i((center/4).floor())):return false
	var cell:=Vector3i((center*250).round())
	for x in range(-1,2):
		for y in range(-1,2):
			for z in range(-1,2):
				for triangle in removed.get(cell+Vector3i(x,y,z),[]):
					if center.distance_squared_to(triangle.center)>.0045*.0045:continue
					if absf(normal.dot(triangle.normal))<.985:continue
					if absf(area-float(triangle.area))>maxf(area,float(triangle.area))*.12+.0001:continue
					return true
	return false
func _vertex_components(point:Vector3)->Dictionary:
	var result:Dictionary={};var bin:=Vector3i((point*250).round())
	for x in range(-1,2):
		for y in range(-1,2):
			for z in range(-1,2):
				for row in _collision_vertices.get(bin+Vector3i(x,y,z),[]):
					if point.distance_squared_to(row.point)<=.0045*.0045:result[row.component]=true
	return result
func _same_removed_component(a:Vector3,b:Vector3,c:Vector3)->bool:
	# Collision mesh optimization may flip a quad's diagonal. All three vertices must
	# still belong to the same recovered source component; a nearby wall cannot match.
	var first:Dictionary=_vertex_components(a)
	if first.is_empty():return false
	var second:Dictionary=_vertex_components(b)
	if second.is_empty():return false
	var third:Dictionary=_vertex_components(c)
	for id in first:
		if second.has(id) and third.has(id):return true
	return false
func validated_spawns(kind:String)->Array[Dictionary]:
	var result:Array[Dictionary]=[]
	for source in data.get(kind,[]):
		var hint:Vector3=map.global_position+vector(source.center)
		# These authoring symbols can float several metres above their intended terrain.
		# Keep their authored X/Z and bounded vertical search, then require actual floor/headroom.
		var floor:Vector3=map._floor(hint+Vector3.UP*1.5,8.0)
		if not floor.is_finite() or floor.y<hint.y-6.0 or floor.y>hint.y+1.0:continue
		var point:Vector3=floor+Vector3.UP*.12
		# Original waiting decks can sit beyond the combat playfield. Require their real
		# floor/headroom; the map/session may allow only these bounded waiting regions.
		if kind=="warmup":
			if not map._room(floor):continue
		else:
			if not map.safe_spawn_r8(point):continue
		result.append({"position":point,"forward":vector(source.forward),"source":"native "+kind+" ring/arrow assembly"})
	if kind=="warmup":_warmup_sites=result
	return result
func warmup_contains(point:Vector3)->bool:
	for row in _warmup_sites:
		var delta:Vector3=point-row.position
		if absf(delta.y)<=4.0 and Vector2(delta.x,delta.z).length_squared()<=8.0*8.0:return true
	return false
func contains_face(row:Dictionary,point:Vector3,max_front:float,min_front:float=-.18)->bool:
	var delta:Vector3=point-row.center;var signed:float=delta.dot(row.normal)
	return signed>=min_front and signed<=max_front and (delta-row.normal*signed).length_squared()<=pow(float(row.radius),2)
func healing_rate_at(point:Vector3)->float:
	var result=0.0
	for row in health_pads:
		if not contains_face(row,point,.75,-.20):continue
		# A pad cannot heal through a wall or a roof. The authored face and feet must have clear contact.
		if not _clear_contact(row,point):continue
		result=maxf(result,float(row.health_per_second))
	return result
func _clear_contact(row:Dictionary,point:Vector3)->bool:
	if not is_inside_tree():return true
	var start:Vector3=row.center+row.normal*.20
	var end:Vector3=point+row.normal*.12
	if start.distance_squared_to(end)<.01:return true
	var query:=PhysicsRayQueryParameters3D.create(start,end,1);query.hit_back_faces=true
	return get_world_3d().direct_space_state.intersect_ray(query).is_empty()
func try_launch(player:Node)->bool:
	if bool(player.get_meta("ghost_r13",false)):return false
	var feet:Vector3=player.get_feet_position_r16() if player.has_method("get_feet_position_r16") else player.global_position
	var previous:Vector3=_previous_feet_r16;_previous_feet_r16=feet
	for i in jump_pads.size():
		if _clock<float(_cooldowns.get(i,-1.0)):continue
		var row:Dictionary=jump_pads[i];var contact:=feet
		if not contains_face(row,contact,.75,-.28):
			# Swept foot contacts keep fast/low-frame-rate pad chains from tunneling.
			if not previous.is_finite() or previous.distance_squared_to(feet)>225:continue
			var segment:=feet-previous
			if segment.length_squared()<.000001:continue
			contact=previous+segment*clampf((row.center-previous).dot(segment)/segment.length_squared(),0,1)
			if not contains_face(row,contact,.75,-.28):continue
		if not _clear_contact(row,contact):continue
		if not player.launch_from_pad_r10(float(row.get("speed",45.0)),row.normal):continue
		_cooldowns[i]=_clock+1.2
		var voice:=AudioStreamPlayer3D.new();add_child(voice);voice.global_position=row.center;voice.stream=load("res://assets/audio/sfx/jump_pad_r8.wav");voice.bus="SFX";voice.volume_db=-5;voice.max_distance=40;voice.finished.connect(voice.queue_free);voice.play()
		return true
	return false
