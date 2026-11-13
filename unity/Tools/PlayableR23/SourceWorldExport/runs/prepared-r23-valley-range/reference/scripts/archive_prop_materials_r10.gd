extends RefCounted
## Original detail for positively matched low-detail archive shapes; no original art is overwritten.
const Base=preload("res://scripts/archive_semantics_r8.gd")
const Helper=preload("res://scripts/archive_semantics_r9.gd")
static var _reference:PackedVector3Array
static func _axes(vertices:PackedVector3Array)->Dictionary:
	var mean:=Vector3.ZERO
	for p in vertices:mean+=p
	mean/=vertices.size()
	var xx=0.0;var zz=0.0;var xz=0.0
	for p in vertices:
		var d:Vector3=p-mean;xx+=d.x*d.x;zz+=d.z*d.z;xz+=d.x*d.z
	var angle:float=.5*atan2(2*xz,xx-zz);var x:=Vector3(cos(angle),0,sin(angle));var z:=Vector3.UP.cross(x)
	return _project(vertices,x,z)
static func _project(vertices:PackedVector3Array,x:Vector3,z:Vector3)->Dictionary:
	var projected:=PackedVector3Array();var box:=AABB()
	for p in vertices:
		var q:=Vector3(p.dot(x),p.y,p.dot(z));projected.append(q)
		box=AABB(q,Vector3.ZERO) if projected.size()==1 else box.expand(q)
	var normalized:=PackedVector3Array()
	for p in projected:normalized.append((p-box.position)/box.size.max(Vector3.ONE*.0001))
	return {"points":normalized,"size":box.size}
static func _reference_points()->PackedVector3Array:
	if not _reference.is_empty():return _reference
	var scene:Node=load("res://local_archive/maps/alfa-aglae-souza/converted/map.scn").instantiate()
	for view in scene.find_children("*","MeshInstance3D",true,false):
		if view.material_override==null or view.material_override.resource_name!="vc":continue
		var groups:Dictionary=Base._components(view.mesh)
		for id in groups.rows:
			var part:Dictionary=groups.rows[id]
			if part.triangles==98 and part.box.get_center().distance_to(Vector3(-41.716,21.82875,11.37975))<.02:
				var faces:PackedVector3Array=Helper._extract(view.mesh.surface_get_arrays(0),groups,id,view.material_override).faces
				_reference=_axes(faces).points;break
	scene.free();return _reference
static func _van(vertices:PackedVector3Array)->Dictionary:
	var reference:PackedVector3Array=_reference_points();var row:Dictionary=_axes(vertices);var points:PackedVector3Array=row.points
	if row.size.y<.8 or row.size.y>4 or row.size.x<2 or row.size.x>12:return {}
	for sx in [false,true]:
		for sz in [false,true]:
			var result:=PackedVector3Array();var accepted=true
			for point in points:
				var p:=Vector3(1-point.x if sx else point.x,point.y,1-point.z if sz else point.z);var distance=INF
				for known in reference:distance=minf(distance,p.distance_squared_to(known))
				if distance>.0001:accepted=false;break
				result.append(p)
			if accepted:return {"points":result}
	return {}
static func _container(vertices:PackedVector3Array)->Dictionary:
	var best:Dictionary={};var area=INF
	for i in range(0,vertices.size(),3):
		for j in 3:
			var edge:Vector3=vertices[i+j]-vertices[i+(j+1)%3]
			if absf(edge.y)>.01 or edge.length()<1:continue
			var x:Vector3=edge.normalized();var row:Dictionary=_project(vertices,x,Vector3.UP.cross(x));var size:Vector3=row.size
			if size.x*size.z<area:area=size.x*size.z;best=row
	if best.is_empty():return {}
	if best.size.x<best.size.z:
		for i in best.points.size():best.points[i]=Vector3(best.points[i].z,best.points[i].y,best.points[i].x)
		best.size=Vector3(best.size.z,best.size.y,best.size.x)
	# Confirm the full-size 12.2 m low-detail container, not arbitrary gray walls/boxes.
	if (best.size-Vector3(12.2007,2.9986,2.6332)).abs().length()>.075:return {}
	return best
static func apply(geometry:Node)->Dictionary:
	var matched={"vans":0,"containers":0,"wheel_parts":0};var materials:Dictionary={}
	var tires:=StandardMaterial3D.new();tires.albedo_color=Color("171c21");tires.roughness=.94;materials.tires=tires
	for kind in ["vans","containers"]:
		var material:=ShaderMaterial.new();material.shader=load("res://assets/materials/archive_van_r10.gdshader" if kind=="vans" else "res://assets/materials/archive_container_r10.gdshader");materials[kind]=material
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		if view.material_override==null or view.material_override.resource_name!="vc":continue
		var groups:Dictionary=Base._components(view.mesh);var arrays:Array=view.mesh.surface_get_arrays(0);var erased:Dictionary={};var tools:Dictionary={};var vans:Array=[]
		for id in groups.rows:
			var part:Dictionary=groups.rows[id]
			if part.triangles not in [12,98]:continue
			if part.box.size.y<.8 or part.box.size.y>4.1:continue
			var extracted:Dictionary=Helper._extract(arrays,groups,id,view.material_override);var kind:String="vans" if part.triangles==98 else "containers"
			var row:Dictionary=_van(extracted.faces) if kind=="vans" else _container(extracted.faces)
			if row.is_empty():continue
			if kind=="vans":vans.append(part.box)
			if not tools.has(kind):tools[kind]=SurfaceTool.new();tools[kind].begin(Mesh.PRIMITIVE_TRIANGLES)
			for i in extracted.faces.size():
				var p:Vector3=row.points[i];tools[kind].set_uv(Vector2(p.x,p.y));tools[kind].set_uv2(Vector2(p.z,0));tools[kind].add_vertex(extracted.faces[i])
			erased[id]=true;matched[kind]+=1
		for id in groups.rows:
			var part:Dictionary=groups.rows[id]
			if part.triangles!=25 or part.box.size.y>.95:continue
			var wheel=false
			for box in vans:
				if box.grow(.65).has_point(part.box.get_center()) and part.box.end.y<box.position.y+.55:wheel=true;break
			if not wheel:continue
			if not tools.has("tires"):tools.tires=SurfaceTool.new();tools.tires.begin(Mesh.PRIMITIVE_TRIANGLES)
			for p in Helper._extract(arrays,groups,id,view.material_override).faces:tools.tires.add_vertex(p)
			erased[id]=true;matched.wheel_parts+=1
		if erased.is_empty():continue
		# Rendering-only extraction: all original collision vertices and layers remain unchanged.
		Helper._remove(view,groups,erased,{})
		for kind in tools:
			tools[kind].generate_normals();var mesh:=MeshInstance3D.new();mesh.name="RecoveredDetailR10_"+kind;mesh.mesh=tools[kind].commit();mesh.material_override=materials[kind];view.get_parent().add_child(mesh);mesh.owner=geometry
	return matched
