extends RefCounted
## Recover only recognizable baked authoring symbols. Original packed cache stays read-only.
static func _root(parent:Array[int],index:int)->int:
	while parent[index]!=index:
		parent[index]=parent[parent[index]];index=parent[index]
	return index
static func _components(mesh:Mesh)->Dictionary:
	var faces:PackedVector3Array=mesh.get_faces();var parents:Array[int]=[];var ids:Dictionary={};var points:Array[Vector3]=[];var tris:Array[int]=[]
	for i in range(0,faces.size(),3):
		var corners:Array[int]=[]
		for k in 3:
			var v:Vector3=faces[i+k];var key:Vector3i=Vector3i((v*1000).round())
			if not ids.has(key):ids[key]=parents.size();parents.append(parents.size());points.append(v)
			corners.append(ids[key])
		for k in [1,2]:parents[_root(parents,corners[k])]=_root(parents,corners[0])
		tris.append(corners[0])
	var rows:Dictionary={}
	for i in points.size():
		var key:int=_root(parents,i)
		if not rows.has(key):rows[key]={"box":AABB(points[i],Vector3.ZERO),"triangles":0}
		rows[key].box=rows[key].box.expand(points[i])
	for i in tris:rows[_root(parents,i)].triangles+=1
	# A rotated door must not be rejected because both world X/Z bounds look wide.
	var candidates:Dictionary={}
	for key in rows:
		var row:Dictionary=rows[key];var box:AABB=row.box
		if row.triangles in [2,4,10,12] and box.size.y>1.7 and box.size.y<3 and maxf(box.size.x,box.size.z)<2.1:candidates[key]=[]
	for i in points.size():
		var key:int=_root(parents,i)
		if candidates.has(key):candidates[key].append(points[i])
	for key in candidates:
		var vertices:Array=candidates[key];var door=false
		for a in mini(vertices.size(),12):
			for b in range(a+1,mini(vertices.size(),16)):
				var edge:Vector3=vertices[b]-vertices[a];edge.y=0
				if edge.length()<.03:continue
				var right:Vector3=edge.normalized();var normal:=Vector3(-right.z,0,right.x);var low:=Vector2(INF,INF);var high:=Vector2(-INF,-INF)
				for point in vertices:
					var at:=Vector2(point.dot(right),point.dot(normal));low=low.min(at);high=high.max(at)
				var extent:Vector2=high-low
				if maxf(extent.x,extent.y)>.7 and maxf(extent.x,extent.y)<1.8 and minf(extent.x,extent.y)<.18:door=true;break
			if door:break
		rows[key]["door_outline_r9"]=door
	for i in tris.size():tris[i]=_root(parents,tris[i])
	return {"rows":rows,"triangle_roots":tris,"faces":faces}
static func _ring(box:AABB,triangles:int)->bool:
	return triangles>=150 and triangles<=192 and box.size.y<.08 and minf(box.size.x,box.size.z)>2.8 and maxf(box.size.x,box.size.z)<3.6
static func _question(box:AABB,triangles:int)->bool:
	var thin:float=minf(box.size.x,box.size.z);var width:float=maxf(box.size.x,box.size.z)
	return (triangles in [107,112] and absf(box.size.y-.39)<.015 and thin<.07 and width<.30) or (triangles==28 and absf(box.size.y-.1)<.015 and thin<.07 and width<.12)
static func _door(box:AABB,triangles:int)->bool:
	return triangles in [2,4,10,12] and box.size.y>1.7 and box.size.y<3.0 and minf(box.size.x,box.size.z)<.18 and maxf(box.size.x,box.size.z)>.7 and maxf(box.size.x,box.size.z)<1.8
static func prepare(geometry:Node,classic:bool)->Dictionary:
	var sites:Array[Vector3]=[];var pads:Array[Vector3]=[];var panels:Array=[];var centers:Dictionary={};var marker_meshes=0;var removed=0
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		var material:Material=view.material_override
		if material==null:continue
		var red:bool=material.resource_name=="p2a61c272"
		var glass:bool=material.resource_name in geometry.get_meta("glass_materials_r9",["s48","t54","t132"]) or "glass" in material.resource_name.to_lower()
		var doors:bool=classic and material.resource_name=="vc"
		if not red and not glass and not doors:continue
		var groups:Dictionary=_components(view.mesh);var erase:Dictionary={}
		for key in groups.rows:
			var row:Dictionary=groups.rows[key];var box:AABB=row.box;var count:int=int(row.triangles)
			if red and _ring(box,count):sites.append(view.transform*box.get_center());erase[key]=true
			elif red and _question(box,count):erase[key]=true
			elif glass and maxf(box.size.x,box.size.z)>.6 and maxf(box.size.x,box.size.z)<=8 and box.size.y>=.9 and box.size.y<5 and count<=48:
				panels.append({"node":view,"component":key,"groups":groups,"box":box,"glass":true});erase[key]=true
			elif doors and (_door(box,count) or row.get("door_outline_r9",false)):
				panels.append({"node":view,"component":key,"groups":groups,"box":box,"glass":false});erase[key]=true
		if erase.is_empty():continue
		var arrays:Array=view.mesh.surface_get_arrays(0);var old:PackedInt32Array=arrays[Mesh.ARRAY_INDEX];var keep=PackedInt32Array()
		for i in groups.triangle_roots.size():
			if erase.has(groups.triangle_roots[i]):
				var center:Vector3=(groups.faces[i*3]+groups.faces[i*3+1]+groups.faces[i*3+2])/3.0;centers[Vector3i((center*10000).round())]=true;removed+=1
			else:
				for k in 3:keep.append(old[i*3+k] if not old.is_empty() else i*3+k)
		var clean:=ArrayMesh.new()
		if not keep.is_empty():arrays[Mesh.ARRAY_INDEX]=keep;clean.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
		for panel in panels:
			if panel.node!=view or panel.has("mesh"):continue
			var indices=PackedInt32Array();var faces=PackedVector3Array()
			for i in groups.triangle_roots.size():
				if groups.triangle_roots[i]!=panel.component:continue
				for k in 3:indices.append(old[i*3+k] if not old.is_empty() else i*3+k);faces.append(groups.faces[i*3+k])
			var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES)
			for index in indices:
				if arrays[Mesh.ARRAY_NORMAL]!=null and arrays[Mesh.ARRAY_NORMAL].size()>index:tool.set_normal(arrays[Mesh.ARRAY_NORMAL][index])
				if arrays[Mesh.ARRAY_TEX_UV]!=null and arrays[Mesh.ARRAY_TEX_UV].size()>index:tool.set_uv(arrays[Mesh.ARRAY_TEX_UV][index])
				if arrays[Mesh.ARRAY_COLOR]!=null and arrays[Mesh.ARRAY_COLOR].size()>index:tool.set_color(arrays[Mesh.ARRAY_COLOR][index])
				tool.add_vertex(arrays[Mesh.ARRAY_VERTEX][index])
			panel.mesh=tool.commit();panel.faces=faces;panel.material=material
		view.set_meta("review_mesh_r8",view.mesh);view.mesh=clean;marker_meshes+=1
	# Remove matching symbol faces from collision too; their invisible question marks must not remain grabbable.
	if not centers.is_empty():
		for shape in geometry.find_children("*","CollisionShape3D",true,false):
			if not shape.shape is ConcavePolygonShape3D:continue
			var faces:PackedVector3Array=shape.shape.get_faces();var clean=PackedVector3Array();var changed=false
			for i in range(0,faces.size(),3):
				var key:=Vector3i((((faces[i]+faces[i+1]+faces[i+2])/3.0)*10000).round())
				if centers.has(key):changed=true
				else:clean.append(faces[i]);clean.append(faces[i+1]);clean.append(faces[i+2])
			if changed:
				var replacement:=ConcavePolygonShape3D.new();replacement.backface_collision=true;replacement.set_faces(clean);shape.shape=replacement
	for panel in panels:panel.erase("node");panel.erase("groups");panel.erase("component")
	return {"weapon_sites":sites,"jump_pads":pads,"panels":panels,"marker_meshes":marker_meshes,"removed_symbol_triangles":removed}
