extends RefCounted
## Split welded trim at recognized door bounds. Intact geometry is preserved; the in-aperture pieces belong to the breakable.
static func _key(a:Vector3,b:Vector3,c:Vector3)->Vector3i:return Vector3i((((a+b+c)/3)*10000).round())
static func _normal(faces:PackedVector3Array)->Vector3:
	var area=0.0;var result:=Vector3.BACK
	for i in range(0,faces.size(),3):
		var cross:Vector3=(faces[i+1]-faces[i]).cross(faces[i+2]-faces[i])
		if cross.length_squared()>area:area=cross.length_squared();result=cross.normalized()
	return result
static func _lerp(a:Dictionary,b:Dictionary,t:float)->Dictionary:
	var row={"p":a.p.lerp(b.p,t)}
	if a.has("n"):row.n=a.n.lerp(b.n,t).normalized()
	if a.has("uv"):row.uv=a.uv.lerp(b.uv,t)
	if a.has("c"):row.c=a.c.lerp(b.c,t)
	return row
static func _split(poly:Array,normal:Vector3,distance:float)->Array:
	var inside:Array=[];var outside:Array=[]
	for i in poly.size():
		var a:Dictionary=poly[i];var b:Dictionary=poly[(i+1)%poly.size()];var da:float=a.p.dot(normal)-distance;var db:float=b.p.dot(normal)-distance
		if da<=.000001:inside.append(a)
		else:outside.append(a)
		if (da<-.000001 and db>.000001) or (da>.000001 and db<-.000001):
			var point:Dictionary=_lerp(a,b,da/(da-db));inside.append(point);outside.append(point)
	return [inside,outside]
static func _carve(poly:Array,planes:Array)->Array:
	var current:Array=poly;var outside:Array=[]
	for plane in planes:
		if current.size()<3:return [outside,[]]
		var split:Array=_split(current,plane[0],plane[1]);current=split[0]
		if split[1].size()>=3:outside.append(split[1])
	return [outside,current]
static func _emit(tool:SurfaceTool,poly:Array,faces:PackedVector3Array)->void:
	for i in range(1,poly.size()-1):
		if (poly[i].p-poly[0].p).cross(poly[i+1].p-poly[0].p).length_squared()<.000000000001:continue
		for row in [poly[0],poly[i],poly[i+1]]:
			if row.has("n"):tool.set_normal(row.n)
			if row.has("uv"):tool.set_uv(row.uv)
			if row.has("c"):tool.set_color(row.c)
			tool.add_vertex(row.p);faces.append(row.p)
static func apply(geometry:Node,panels:Array)->Dictionary:
	var entries:Array=[];var cells:Dictionary={};var replacements:Dictionary={};var changed=0;var fragments=0
	for panel in panels:
		var normal:Vector3=_normal(panel.faces)
		if absf(normal.y)>.1:continue
		var right:Vector3=Vector3.UP.cross(normal).normalized();var low:=Vector2(INF,INF);var high:=Vector2(-INF,-INF)
		for point in panel.faces:
			var at:=Vector2(point.dot(right),point.y);low=low.min(at);high=high.max(at)
		var middle:float=panel.box.get_center().dot(normal);var index:int=entries.size();var box:AABB=panel.box.grow(.36)
		entries.append({"panel":panel,"box":box,"planes":[[right,high.x+.003],[-right,-low.x+.003],[Vector3.UP,high.y+.003],[Vector3.DOWN,-low.y+.003],[normal,middle+.32],[-normal,-middle+.32]]})
		for z in range(floori(box.position.z/12),floori(box.end.z/12)+1):
			for x in range(floori(box.position.x/12),floori(box.end.x/12)+1):
				var cell:=Vector2i(x,z)
				if not cells.has(cell):cells[cell]=[]
				cells[cell].append(index)
	if entries.is_empty():return {"triangles":0,"fragments":0}
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		if view.mesh==null or view.mesh.get_surface_count()!=1 or view.material_override==null or view.material_override.resource_name!="vc":continue
		if not view.transform.is_equal_approx(Transform3D.IDENTITY):continue
		var arrays:Array=view.mesh.surface_get_arrays(0);var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX];var indices:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array();var count:int=indices.size() if not indices.is_empty() else vertices.size();var keep:=PackedInt32Array();var new_polys:Array=[];var tools:Dictionary={};var view_changed=false
		for i in range(0,count,3):
			var ids:Array[int]=[]
			for k in 3:ids.append(indices[i+k] if not indices.is_empty() else i+k)
			var a:Vector3=vertices[ids[0]];var b:Vector3=vertices[ids[1]];var c:Vector3=vertices[ids[2]];var box:=AABB(a,Vector3.ZERO).expand(b).expand(c);var candidates:Dictionary={}
			for z in range(floori(box.position.z/12),floori(box.end.z/12)+1):
				for x in range(floori(box.position.x/12),floori(box.end.x/12)+1):
					for index in cells.get(Vector2i(x,z),[]):
						if entries[index].box.intersects(box.grow(.0001)):candidates[index]=true
			if candidates.is_empty():keep.append_array(ids);continue
			var triangle:Array=[]
			for id in ids:
				var row:Dictionary={"p":vertices[id]}
				if arrays[Mesh.ARRAY_NORMAL]!=null:row.n=arrays[Mesh.ARRAY_NORMAL][id]
				if arrays[Mesh.ARRAY_TEX_UV]!=null:row.uv=arrays[Mesh.ARRAY_TEX_UV][id]
				if arrays[Mesh.ARRAY_COLOR]!=null:row.c=arrays[Mesh.ARRAY_COLOR][id]
				triangle.append(row)
			var pieces:Array=[triangle];var cut=false
			for index in candidates:
				var remaining:Array=[]
				for piece in pieces:
					var split:Array=_carve(piece,entries[index].planes);var inside:Array=split[1]
					if inside.size()<3:remaining.append(piece);continue
					var area=0.0
					for k in range(1,inside.size()-1):area+=(inside[k].p-inside[0].p).cross(inside[k+1].p-inside[0].p).length()
					if area<.000001:remaining.append(piece);continue
					if not tools.has(index):
						var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES);tools[index]={"tool":tool,"faces":PackedVector3Array()}
					_emit(tools[index].tool,inside,tools[index].faces);fragments+=1;cut=true;remaining.append_array(split[0])
				pieces=remaining
			if not cut:keep.append_array(ids);continue
			view_changed=true;changed+=1;var collision:=PackedVector3Array()
			for piece in pieces:
				new_polys.append(piece)
				for k in range(1,piece.size()-1):collision.append_array(PackedVector3Array([piece[0].p,piece[k].p,piece[k+1].p]))
			replacements[_key(a,b,c)]=collision
		if not view_changed:continue
		var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES);var ignored:=PackedVector3Array()
		for i in range(0,keep.size(),3):
			var poly:Array=[]
			for k in 3:
				var id:int=keep[i+k];var row:Dictionary={"p":vertices[id]}
				if arrays[Mesh.ARRAY_NORMAL]!=null:row.n=arrays[Mesh.ARRAY_NORMAL][id]
				if arrays[Mesh.ARRAY_TEX_UV]!=null:row.uv=arrays[Mesh.ARRAY_TEX_UV][id]
				if arrays[Mesh.ARRAY_COLOR]!=null:row.c=arrays[Mesh.ARRAY_COLOR][id]
				poly.append(row)
			_emit(tool,poly,ignored)
		for poly in new_polys:_emit(tool,poly,ignored)
		tool.index();view.mesh=tool.commit()
		for index in tools:
			var panel:Dictionary=entries[index].panel
			if not panel.has("attachments"):panel.attachments=[]
			panel.attachments.append({"mesh":tools[index].tool.commit(),"material":view.material_override,"faces":tools[index].faces})
	for shape in geometry.find_children("*","CollisionShape3D",true,false):
		if not shape.shape is ConcavePolygonShape3D:continue
		var old:PackedVector3Array=shape.shape.get_faces();var clean:=PackedVector3Array();var dirty=false
		for i in range(0,old.size(),3):
			var key:Vector3i=_key(old[i],old[i+1],old[i+2])
			if replacements.has(key):clean.append_array(replacements[key]);dirty=true
			else:clean.append_array(PackedVector3Array([old[i],old[i+1],old[i+2]]))
		if dirty:
			var replacement:=ConcavePolygonShape3D.new();replacement.backface_collision=true;replacement.set_faces(clean);shape.shape=replacement
	return {"triangles":changed,"fragments":fragments}
