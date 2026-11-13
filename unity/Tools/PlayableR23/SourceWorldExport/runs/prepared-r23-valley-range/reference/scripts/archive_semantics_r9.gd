extends RefCounted
const Base=preload("res://scripts/archive_semantics_r8.gd")
static func prepare(geometry:Node,map_id:String,source:Dictionary)->Dictionary:
	var official:bool=source.get("official",false);var materials:Dictionary=source.get("materials",{});var glass:Array=[]
	if map_id!="archive:vortex-sd-stumpdawg":
		for key in materials:
			var row:Dictionary=materials[key];var texture:String=str(row.get("c",""))
			if "glass" in str(row.get("name","")) or (key=="s48" and "demo-gamemap-" in texture and texture.ends_with("/s48_c.png")):glass.append(key)
	geometry.set_meta("glass_materials_r9",glass)
	var details:Dictionary=Base.prepare(geometry,true);var removed:Dictionary={};var pads:Array[Vector3]=[];var spawn_floors:Array=[];var screens:Array=[];var score_faces:Array=[];var signs:Array=[];var weapon_boards:Array=[]
	var glass_materials:Dictionary={}
	for panel in details.panels:
		if not panel.glass:continue
		var key:String=panel.material.resource_name
		if not glass_materials.has(key):
			var mat:=ShaderMaterial.new();mat.resource_name=key;mat.shader=preload("res://assets/materials/clear_glass_r10.gdshader");glass_materials[key]=mat
		panel.material=glass_materials[key]
	details.panels=_merge_glass_r10(details.panels)
	var pane_cells:Dictionary={}
	for panel in details.panels:
		if not panel.get("glass",false):continue
		var cell:=Vector3i((panel.box.get_center()/4).floor())
		if not pane_cells.has(cell):pane_cells[cell]=[]
		pane_cells[cell].append(panel)
	# Baked window frames can be exported as thin door-shaped pieces. Their pane owns the whole assembly.
	for panel in details.panels:
		if panel.glass:continue
		var cell:=Vector3i((panel.box.get_center()/4).floor())
		for x in range(-1,2):
			for y in range(-1,2):
				for z in range(-1,2):
					for pane in pane_cells.get(cell+Vector3i(x,y,z),[]):
						if panel.get("merged_glass_r10",false):continue
						var delta:Vector3=(pane.box.get_center()-panel.box.get_center()).abs();var difference:Vector3=(pane.box.size-panel.box.size).abs()
						if delta.length()>.3 or difference.y>.35 or maxf(difference.x,difference.z)>.4:continue
						if not pane.has("attachments"):pane.attachments=[]
						pane.attachments.append({"mesh":panel.mesh,"material":panel.material,"faces":panel.faces});pane.attachments.append_array(panel.get("attachments",[]));pane.box=pane.box.merge(panel.box);panel.merged_glass_r10=true;break
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		var material:Material=view.material_override
		if material==null or view.mesh.get_surface_count()==0:continue
		var key:String=material.resource_name;var row:Dictionary=materials.get(key,{})
		var pad_material:bool="jumppad" in str(row.get("c","")) or "jumppad" in str(row.get("name",""))
		var inspect:bool=key=="vc" or str(view.name)=="WalkThrough" or pad_material or (map_id=="archive:melipop-gods-poplightning" and key in ["t52","t64"])
		if not inspect:continue
		var groups:Dictionary=Base._components(view.mesh);var erase:Dictionary={};var arrays:Array=view.mesh.surface_get_arrays(0)
		for id in groups.rows:
			var part:Dictionary=groups.rows[id];var box:AABB=part.box;var size:Vector3=box.size
			# Measured central megatron, matched to this hash-bound source geometry. No gameplay tag is invented.
			if map_id=="archive:melipop-gods-poplightning" and key=="t52" and part.triangles==12 and box.get_center().distance_to(Vector3(-34.2385,216.251,56.477))<.2:
				score_faces=_screen_faces(groups,id)
			if map_id=="archive:melipop-gods-poplightning" and key=="t64" and part.triangles<=12 and size.y>1.5 and size.y<6 and maxf(size.x,size.z)>5 and box.get_center().y>190:
				var board:Dictionary=_extract(arrays,groups,id,material);var normal:Vector3=_surface_normal(board.faces)
				if absf(normal.y)<.1:weapon_boards.append({"box":box,"normal":normal})
			if pad_material and size.y<3 and minf(size.x,size.z)>.5 and maxf(size.x,size.z)<14:
				var center:Vector3=box.get_center();center.y=box.end.y;pads.append(center)
			if str(view.name)=="WalkThrough" and size.y<.45 and minf(size.x,size.z)>1:
				for at in source.get("spawns",[]):
					var spawn:=Vector3(at[0],at[1],at[2])
					if Rect2(Vector2(box.position.x,box.position.z),Vector2(size.x,size.z)).has_point(Vector2(spawn.x,spawn.z)) and absf(spawn.y-box.end.y)<1.2:
						spawn_floors.append(box);break
			if key=="vc":
				var cell:=Vector3i((box.get_center()/4).floor());var nearby:Array=[]
				for x in range(-1,2):
					for y in range(-1,2):
						for z in range(-1,2):nearby.append_array(pane_cells.get(cell+Vector3i(x,y,z),[]))
				for panel in nearby:
					var pane:AABB=panel.box;var distance:Vector3=(pane.get_center()-box.get_center()).abs()
					if distance.length()>.22 or minf(size.x,size.z)>.16 or size.x>pane.size.x+.15 or size.y>pane.size.y+.15 or size.z>pane.size.z+.15:continue
					if part.triangles>120:continue
					if not panel.has("attachments"):panel.attachments=[]
					panel.attachments.append(_extract(arrays,groups,id,material));erase[id]=true;break
			if str(view.name)=="WalkThrough" and map_id=="archive:melipop-gods-poplightning" and size.y>4 and maxf(size.x,size.z)>8:
				screens.append({"box":box,"triangles":part.triangles})
		if not erase.is_empty():_remove(view,groups,erase,removed)
	# Extracted frame collision belongs to its window, never left invisibly in the world.
	if not removed.is_empty():
		for shape in geometry.find_children("*","CollisionShape3D",true,false):
			if not shape.shape is ConcavePolygonShape3D:continue
			var faces:PackedVector3Array=shape.shape.get_faces();var keep:=PackedVector3Array()
			for i in range(0,faces.size(),3):
				if removed.has(Vector3i((((faces[i]+faces[i+1]+faces[i+2])/3)*10000).round())):continue
				keep.append(faces[i]);keep.append(faces[i+1]);keep.append(faces[i+2])
			if keep.size()!=faces.size():
				var replacement:=ConcavePolygonShape3D.new();replacement.backface_collision=true;replacement.set_faces(keep);shape.shape=replacement
	# A source door can have independent frame, leaf and handle components. One hit removes the assembly.
	var assembled:Array=[];var door_cells:Dictionary={}
	for panel in details.panels:
		if panel.get("merged_glass_r10",false):continue
		if panel.glass:assembled.append(panel);continue
		var center:Vector3=panel.box.get_center();var cell:=Vector3i(center.floor());var master:Dictionary={}
		for x in range(-1,2):
			for y in range(-1,2):
				for z in range(-1,2):
					for old in door_cells.get(cell+Vector3i(x,y,z),[]):
						if old.box.get_center().distance_to(center)<.38:master=old;break
		if master.is_empty():
			assembled.append(panel)
			if not door_cells.has(cell):door_cells[cell]=[]
			door_cells[cell].append(panel)
		else:
			if not master.has("attachments"):master.attachments=[]
			master.attachments.append({"mesh":panel.mesh,"material":panel.material,"faces":panel.faces});master.attachments.append_array(panel.get("attachments",[]));master.box=master.box.merge(panel.box)
	details.panels=assembled
	details.door_cut_r9=preload("res://scripts/archive_passage_cut_r9.gd").apply(geometry,assembled)
	var unique_pads:Array[Vector3]=[]
	for at in pads:
		var duplicate=false
		for old in unique_pads:
			if at.distance_to(old)<2.8:duplicate=true;break
		if not duplicate:unique_pads.append(at)
	var selected_boards:Array=[]
	for spawn in [Vector3(-222,197,-90),Vector3(125,201,228)]:
		var nearest:Dictionary={};var distance=60.0
		for board in weapon_boards:
			var d:float=board.box.get_center().distance_to(spawn)
			if d<distance:nearest=board;distance=d
		if not nearest.is_empty():
			var n:Vector3=nearest.normal
			if n.dot(spawn-nearest.box.get_center())<0:n=-n
			nearest.normal=n;nearest.floor_hint=spawn.y;selected_boards.append(nearest)
	details.jump_pads=unique_pads;details.spawn_floors_r9=spawn_floors;details.screen_candidates_r9=screens;details.frame_triangles_r9=removed.size();details.source_tags_present_r9=false;details.score_faces_r9=score_faces;details.signs_r9=signs;details.weapon_boards_r9=selected_boards
	return details
static func _merge_glass_r10(panels:Array)->Array:
	var result:Array=[];var cells:Dictionary={}
	for panel in panels:
		if not panel.glass:result.append(panel);continue
		var cell:=Vector3i(panel.box.get_center().floor());var normal:Vector3=_surface_normal(panel.faces);var master:Dictionary={}
		for x in range(-1,2):
			for y in range(-1,2):
				for z in range(-1,2):
					for old in cells.get(cell+Vector3i(x,y,z),[]):
						if not master.is_empty():continue
						if old.box.get_center().distance_to(panel.box.get_center())>.2 or (old.box.size-panel.box.size).length()>.4:continue
						if absf(_surface_normal(old.faces).dot(normal))<.98:continue
						master=old
		if master.is_empty():
			result.append(panel)
			if not cells.has(cell):cells[cell]=[]
			cells[cell].append(panel)
		else:
			if not master.has("attachments"):master.attachments=[]
			master.attachments.append({"mesh":panel.mesh,"material":panel.material,"faces":panel.faces});master.attachments.append_array(panel.get("attachments",[]));master.box=master.box.merge(panel.box)
	return result
static func _surface_normal(faces:PackedVector3Array)->Vector3:
	var best=0.0;var normal:=Vector3.BACK
	for i in range(0,faces.size(),3):
		var cross:Vector3=(faces[i+1]-faces[i]).cross(faces[i+2]-faces[i]);var area:float=cross.length_squared()
		if area>best:best=area;normal=cross.normalized()
	return normal
static func _screen_faces(groups:Dictionary,id:int)->Array:
	var planes:Dictionary={}
	for i in groups.triangle_roots.size():
		if groups.triangle_roots[i]!=id:continue
		var a:Vector3=groups.faces[i*3];var b:Vector3=groups.faces[i*3+1];var c:Vector3=groups.faces[i*3+2];var normal:Vector3=(b-a).cross(c-a).normalized()
		if absf(normal.y)>.1:continue
		if normal.dot(a-groups.rows[id].box.get_center())<0:normal=-normal
		var key:=Vector3i((normal*100).round())
		if not planes.has(key):planes[key]={"normal":normal,"faces":PackedVector3Array()}
		planes[key].faces.append_array(PackedVector3Array([a,b,c]))
	var result:Array=[]
	for plane in planes.values():
		var normal:Vector3=plane.normal;var right:Vector3=Vector3.UP.cross(normal).normalized();var low:=Vector2(INF,INF);var high:=Vector2(-INF,-INF)
		for point in plane.faces:
			var uv:=Vector2(point.dot(right),point.y);low=low.min(uv);high=high.max(uv)
		var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES)
		for point in plane.faces:
			var uv:Vector2=(Vector2(point.dot(right),point.y)-low)/(high-low);uv.y=1-uv.y;tool.set_uv(uv);tool.set_normal(normal);tool.add_vertex(point+normal*.04)
		result.append(tool.commit())
	return result
static func _extract(arrays:Array,groups:Dictionary,id:int,material:Material)->Dictionary:
	var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES);var old:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array();var faces:=PackedVector3Array()
	for i in groups.triangle_roots.size():
		if groups.triangle_roots[i]!=id:continue
		for k in 3:
			var index:int=old[i*3+k] if not old.is_empty() else i*3+k
			if arrays[Mesh.ARRAY_NORMAL]!=null:tool.set_normal(arrays[Mesh.ARRAY_NORMAL][index])
			if arrays[Mesh.ARRAY_TEX_UV]!=null:tool.set_uv(arrays[Mesh.ARRAY_TEX_UV][index])
			if arrays[Mesh.ARRAY_COLOR]!=null:tool.set_color(arrays[Mesh.ARRAY_COLOR][index])
			var point:Vector3=arrays[Mesh.ARRAY_VERTEX][index];tool.add_vertex(point);faces.append(point)
	return {"mesh":tool.commit(),"material":material,"faces":faces}
static func _remove(view:MeshInstance3D,groups:Dictionary,erase:Dictionary,removed:Dictionary)->void:
	var arrays:Array=view.mesh.surface_get_arrays(0);var old:PackedInt32Array=arrays[Mesh.ARRAY_INDEX];var keep:=PackedInt32Array()
	for i in groups.triangle_roots.size():
		if erase.has(groups.triangle_roots[i]):removed[Vector3i((((groups.faces[i*3]+groups.faces[i*3+1]+groups.faces[i*3+2])/3)*10000).round())]=true
		else:
			for k in 3:keep.append(old[i*3+k] if not old.is_empty() else i*3+k)
	var mesh:=ArrayMesh.new()
	if not keep.is_empty():arrays[Mesh.ARRAY_INDEX]=keep;mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	view.mesh=mesh
