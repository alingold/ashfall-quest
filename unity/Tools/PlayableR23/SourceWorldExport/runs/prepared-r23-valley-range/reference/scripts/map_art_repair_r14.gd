extends RefCounted
## Original, geometry-matched surface detail for source exports with no image/UV.
## This does not claim to reconstruct the unavailable original prop artwork.
const Base=preload("res://scripts/archive_semantics_r8.gd")
const Helper=preload("res://scripts/archive_semantics_r9.gd")
const Axes=preload("res://scripts/archive_prop_materials_r10.gd")
static var _materials:Dictionary={}
static var _profiles:Array=[]
static var _plans:Dictionary={}

static func _root(parents:Array[int],i:int)->int:
	while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
	return i

static func _groups(mesh:Mesh)->Dictionary:
	# No expensive door-outline classification is needed for this rendering pass.
	var faces:PackedVector3Array=mesh.get_faces();var parents:Array[int]=[];var ids:Dictionary={};var points:=PackedVector3Array();var roots:=PackedInt32Array()
	for i in range(0,faces.size(),3):
		var corners:=PackedInt32Array()
		for k in 3:
			var v=faces[i+k];var key=Vector3i((v*1000).round())
			if not ids.has(key):ids[key]=parents.size();parents.append(parents.size());points.append(v)
			corners.append(ids[key])
		for k in [1,2]:parents[_root(parents,corners[k])]=_root(parents,corners[0])
		roots.append(corners[0])
	var rows:Dictionary={}
	for i in points.size():
		var key=_root(parents,i)
		if not rows.has(key):rows[key]={"box":AABB(points[i],Vector3.ZERO),"triangles":0}
		rows[key].box=rows[key].box.expand(points[i])
	for i in roots.size():roots[i]=_root(parents,roots[i]);rows[roots[i]].triangles+=1
	return {"rows":rows,"triangle_roots":roots,"faces":faces}

static func _data()->Array:
	if _profiles.is_empty():
		var file=FileAccess.open("res://assets/data/map_art_audit_r14.json",FileAccess.READ)
		if file!=null:
			var data=JSON.parse_string(file.get_as_text())
			if data is Dictionary:
				_profiles=data.get("repair_profiles",[])
				for row in data.get("maps",[]):_plans[row.id]=row.get("repair_selectors",[])
	return _profiles

static func _material(kind:String)->ShaderMaterial:
	if _materials.has(kind):return _materials[kind]
	var shader:=Shader.new()
	shader.code="""shader_type spatial;
render_mode cull_disabled;
uniform bool wood=false;
void fragment(){
	vec2 p=UV;
	vec3 base=COLOR.rgb;
	float board=fract(p.x*10.0);
	float seam=1.0-smoothstep(.009,.025,min(board,1.0-board));
	float grain=.5+.5*sin(p.y*75.0+sin(p.x*93.0)*2.0);
	// Fade subpixel grain/seams instead of introducing distant VR shimmer.
	float fade=1.0-smoothstep(.018,.055,max(fwidth(p.x),fwidth(p.y)));
	if(wood){
		float knot=sin(p.y*18.0+sin(p.x*29.0)*1.8);
		ALBEDO=base*(.88+.12*grain*fade+.035*knot*fade)*(1.0-.48*seam*fade);
		ROUGHNESS=.95;
	}else{
		float rib=.5+.5*cos(p.x*6.2831853*10.0);
		float edge=1.0-smoothstep(.01,.025,min(p.y,1.0-p.y));
		ALBEDO=base*(.83+.17*smoothstep(.12,.5,rib)*fade)*(1.0-.35*edge);
		float lock=1.0-smoothstep(.008,.016,min(abs(p.x-.25),abs(p.x-.75)));
		ALBEDO=mix(ALBEDO,vec3(.3,.32,.31),lock*.65*fade);
		ROUGHNESS=.83;METALLIC=.35;
	}
}"""
	var material:=ShaderMaterial.new();material.shader=shader;material.set_shader_parameter("wood",kind=="wood_panels")
	material.resource_name="OriginalMatchedDetailR14_"+kind;_materials[kind]=material;return material

static func _panel(faces:PackedVector3Array)->Dictionary:
	# Shipping-container end faces retain a distinctive exact 2.63269 x 2.99868 m quad.
	# Match physical dimensions and planarity, never arbitrary gray/white rectangles.
	if faces.size()!=6:return {}
	var box:=AABB(faces[0],Vector3.ZERO)
	for p in faces:box=box.expand(p)
	if absf(box.size.y-2.99868)>.004:return {}
	var width=sqrt(box.size.x*box.size.x+box.size.z*box.size.z)
	if absf(width-2.63269)>.005:return {}
	var axis:=Vector3.ZERO
	for a in faces:
		for b in faces:
			var edge:Vector3=b-a
			if absf(edge.y)<.002 and edge.length()>2.6:axis=edge.normalized();break
		if axis.length_squared()>.5:break
	if axis.length_squared()<.5:return {}
	var normal=Vector3.UP.cross(axis)
	for p in faces:
		if absf((p-faces[0]).dot(normal))>.002:return {}
	var result:Dictionary=Axes._project(faces,axis,normal)
	return result

static func _wood(faces:PackedVector3Array,color:Color)->Dictionary:
	# Exact reference point cloud plus original brown vertex color prevents painting
	# authored solid-color walls, doors, signs, terrain, or ambiguous props.
	if Vector3(color.r,color.g,color.b).distance_to(Vector3(101,73,51)/255.0)>.012:return {}
	var row:Dictionary=Axes._axes(faces)
	for profile in _data():
		if int(profile.triangles)*3!=faces.size():continue
		var size=Vector3(profile.size[0],profile.size[1],profile.size[2])
		if (row.size-size).length()>.03:continue
		for sx in [false,true]:
			for sz in [false,true]:
				var accepted=true
				for q in row.points:
					var point=Vector3(1-q.x if sx else q.x,q.y,1-q.z if sz else q.z);var nearest=INF
					for known in profile.points:nearest=minf(nearest,point.distance_squared_to(Vector3(known[0],known[1],known[2])))
					if nearest>.0001:accepted=false;break
				if accepted:return row
	return {}

static func _remove(view:MeshInstance3D,groups:Dictionary,erase:Dictionary)->void:
	var arrays:Array=view.mesh.surface_get_arrays(0);var old:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array();var keep:=PackedInt32Array()
	for i in groups.triangle_roots.size():
		if erase.has(groups.triangle_roots[i]):continue
		for k in 3:keep.append(old[i*3+k] if not old.is_empty() else i*3+k)
	var mesh:=ArrayMesh.new()
	if not keep.is_empty():arrays[Mesh.ARRAY_INDEX]=keep;mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	view.mesh=mesh

static func prepare(geometry:Node,_map_id:String="",capture:Array=[],dry:bool=false)->Dictionary:
	var stats={"container_end_panels":0,"wood_panels":0,"draw_chunks":0}
	if geometry.has_meta("art_repaired_r14"):return geometry.get_meta("art_repaired_r14")
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		if view.material_override==null or view.material_override.resource_name!="vc" or view.mesh==null or view.mesh.get_surface_count()==0:continue
		var arrays:Array=view.mesh.surface_get_arrays(0);var colors:PackedColorArray=arrays[Mesh.ARRAY_COLOR]
		if colors.is_empty():continue
		var groups:Dictionary=_groups(view.mesh);var erase:Dictionary={};var tools:Dictionary={};var selectors:Array=[]
		for id in groups.rows:
			var part:Dictionary=groups.rows[id]
			if part.triangles not in [2,28]:continue
			if part.triangles==2 and (absf(part.box.size.y-2.99868)>.004 or absf(sqrt(part.box.size.x*part.box.size.x+part.box.size.z*part.box.size.z)-2.63269)>.005):continue
			if part.triangles==28 and absf(part.box.size.y-2.414785)>.03:continue
			var extracted:Dictionary=Helper._extract(arrays,groups,id,view.material_override)
			var ea:Array=extracted.mesh.surface_get_arrays(0);var source_colors:PackedColorArray=ea[Mesh.ARRAY_COLOR]
			if source_colors.is_empty():continue
			var row:Dictionary=_panel(extracted.faces) if part.triangles==2 else _wood(extracted.faces,source_colors[0])
			if row.is_empty():continue
			var kind:String="container_end_panels" if part.triangles==2 else "wood_panels"
			var triangle_ids:Array=[];var uv:Array=[]
			for i in groups.triangle_roots.size():
				if groups.triangle_roots[i]==id:triangle_ids.append(i)
			for p in row.points:uv.append([snappedf(p.x,.00001),snappedf(p.y,.00001)])
			selectors.append({"kind":kind,"triangles":triangle_ids,"uv":uv})
			if not tools.has(kind):tools[kind]=SurfaceTool.new();tools[kind].begin(Mesh.PRIMITIVE_TRIANGLES)
			for i in extracted.faces.size():
				var point:Vector3=row.points[i];tools[kind].set_color(source_colors[i]);tools[kind].set_uv(Vector2(point.x,point.y));tools[kind].add_vertex(extracted.faces[i])
			erase[id]=true;stats[kind]+=1
		if erase.is_empty():continue
		capture.append({"node":str(geometry.get_path_to(view)),"vertex_count":arrays[Mesh.ARRAY_VERTEX].size(),"index_count":arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX]!=null else 0,"parts":selectors})
		if dry:stats.draw_chunks+=tools.size();continue
		# Only rendering is replaced. Static collision, climb groups and layers persist.
		_remove(view,groups,erase)
		for kind in tools:
			tools[kind].generate_normals();var mesh:=MeshInstance3D.new();mesh.name="MatchedArtR14_"+kind+"_"+str(stats.draw_chunks);mesh.mesh=tools[kind].commit();mesh.material_override=_material(kind);mesh.transform=view.transform;view.get_parent().add_child(mesh,true);mesh.owner=geometry;stats.draw_chunks+=1
	geometry.set_meta("art_repaired_r14",stats);return stats

static func apply(geometry:Node,map_id:String="")->Dictionary:
	var stats={"container_end_panels":0,"wood_panels":0,"draw_chunks":0,"guards_skipped":0}
	if geometry.has_meta("art_repaired_r14"):return geometry.get_meta("art_repaired_r14")
	_data()
	for plan in _plans.get(map_id,[]):
		var view=geometry.get_node_or_null(NodePath(plan.node))
		if not view is MeshInstance3D or view.mesh==null or view.mesh.get_surface_count()!=1 or view.material_override==null or view.material_override.resource_name!="vc":stats.guards_skipped+=1;continue
		var arrays:Array=view.mesh.surface_get_arrays(0);var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX];var indices:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array();var colors:PackedColorArray=arrays[Mesh.ARRAY_COLOR]
		if vertices.size()!=int(plan.vertex_count) or indices.size()!=int(plan.index_count) or colors.size()!=vertices.size():stats.guards_skipped+=1;continue
		var erase:Dictionary={};var tools:Dictionary={}
		for part in plan.parts:
			var kind:String=part.kind
			if not tools.has(kind):tools[kind]=SurfaceTool.new();tools[kind].begin(Mesh.PRIMITIVE_TRIANGLES)
			var offset=0
			for triangle in part.triangles:
				var tri:int=int(triangle);erase[tri]=true
				for k in 3:
					var at:int=indices[tri*3+k] if not indices.is_empty() else tri*3+k
					tools[kind].set_color(colors[at]);tools[kind].set_uv(Vector2(part.uv[offset][0],part.uv[offset][1]));tools[kind].add_vertex(vertices[at]);offset+=1
			stats[kind]+=1
		var roots:=PackedInt32Array();roots.resize(indices.size()/3 if not indices.is_empty() else vertices.size()/3)
		for i in roots.size():roots[i]=i
		_remove(view,{"triangle_roots":roots},erase)
		for kind in tools:
			tools[kind].generate_normals();var mesh:=MeshInstance3D.new();mesh.name="MatchedArtR14_"+kind+"_"+str(stats.draw_chunks);mesh.mesh=tools[kind].commit();mesh.material_override=_material(kind);mesh.transform=view.transform;view.get_parent().add_child(mesh,true);mesh.owner=geometry;stats.draw_chunks+=1
	geometry.set_meta("art_repaired_r14",stats);return stats
