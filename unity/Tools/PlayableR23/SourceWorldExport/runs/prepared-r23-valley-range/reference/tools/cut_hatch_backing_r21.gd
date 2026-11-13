extends RefCounted
## Some exported tower facades keep a solid wall behind the authored hatch.
## Clip only the measured octagonal passage out of those planes, and attach that
## exact patch to its breakable hatch. Floors, frame surrounds and other walls stay.
const F=preload("res://scripts/map_fixtures_r18.gd")
const EPS=.00001
static func _mix(a:Dictionary,b:Dictionary,t:float)->Dictionary:
 var r={}
 for key in a:
  if key=="p" or key=="n":r[key]=a[key].lerp(b[key],t)
  elif key=="uv" or key=="uv2" or key=="c":r[key]=a[key].lerp(b[key],t)
  elif key=="t":r[key]=a[key].lerp(b[key],t)
 return r
static func _split(poly:Array,plane:Plane,xf:Transform3D)->Array:
 var inside=[];var outside=[]
 for i in poly.size():
  var a:Dictionary=poly[i];var b:Dictionary=poly[(i+1)%poly.size()]
  var da=plane.distance_to(xf*a.p);var db=plane.distance_to(xf*b.p)
  var ai=da<=EPS;var bi=db<=EPS
  if ai:inside.append(a)
  else:outside.append(a)
  if ai!=bi:
   var v=_mix(a,b,clampf(da/(da-db),0,1));inside.append(v);outside.append(v)
 return [inside,outside]
static func _area(poly:Array)->float:
 var result=0.0
 if poly.size()<3:return 0.0
 for i in range(1,poly.size()-1):result+=(poly[i].p-poly[0].p).cross(poly[i+1].p-poly[0].p).length()*.5
 return result
static func _subtract(poly:Array,volume:Dictionary,xf:Transform3D)->Dictionary:
 var inside=poly;var outside=[]
 for plane in volume.planes:
  var pieces=_split(inside,plane,xf)
  if _area(pieces[1])>EPS:outside.append(pieces[1])
  inside=pieces[0]
  if inside.size()<3:return {"cut":false}
 if _area(inside)<=EPS:return {"cut":false}
 return {"cut":true,"inside":inside,"outside":outside}
static func _vertex(a:Array,id:int)->Dictionary:
 var row={"p":a[Mesh.ARRAY_VERTEX][id]}
 if a[Mesh.ARRAY_NORMAL]!=null:row.n=a[Mesh.ARRAY_NORMAL][id]
 if a[Mesh.ARRAY_TEX_UV]!=null:row.uv=a[Mesh.ARRAY_TEX_UV][id]
 if a[Mesh.ARRAY_TEX_UV2]!=null:row.uv2=a[Mesh.ARRAY_TEX_UV2][id]
 if a[Mesh.ARRAY_COLOR]!=null:row.c=a[Mesh.ARRAY_COLOR][id]
 if a[Mesh.ARRAY_TANGENT]!=null:row.t=Vector4(a[Mesh.ARRAY_TANGENT][id*4],a[Mesh.ARRAY_TANGENT][id*4+1],a[Mesh.ARRAY_TANGENT][id*4+2],a[Mesh.ARRAY_TANGENT][id*4+3])
 return row
static func _append(poly:Array,a:Array,indices:PackedInt32Array)->void:
 for i in range(1,poly.size()-1):
  for row in [poly[0],poly[i],poly[i+1]]:
   indices.append(a[Mesh.ARRAY_VERTEX].size());a[Mesh.ARRAY_VERTEX].append(row.p)
   if row.has("n"):a[Mesh.ARRAY_NORMAL].append(row.n.normalized())
   if row.has("uv"):a[Mesh.ARRAY_TEX_UV].append(row.uv)
   if row.has("uv2"):a[Mesh.ARRAY_TEX_UV2].append(row.uv2)
   if row.has("c"):a[Mesh.ARRAY_COLOR].append(row.c)
   if row.has("t"):
    var t:Vector3=Vector3(row.t.x,row.t.y,row.t.z).normalized();a[Mesh.ARRAY_TANGENT].append_array(PackedFloat32Array([t.x,t.y,t.z,row.t.w]))
static func _cut(poly:Array,volumes:Array,xf:Transform3D)->Dictionary:
 var pieces=[poly];var removed={};var normal:Vector3=((xf*poly[1].p)-(xf*poly[0].p)).cross((xf*poly[2].p)-(xf*poly[0].p)).normalized()
 var box=AABB(xf*poly[0].p,Vector3.ZERO)
 for row in poly:box=box.expand(xf*row.p)
 for i in volumes.size():
  var v=volumes[i]
  if not box.intersects(v.bounds) or absf(normal.dot(v.normal))<.8:continue
  var next=[]
  for piece in pieces:
   var cut=_subtract(piece,v,xf)
   if cut.cut:
    if not removed.has(i):removed[i]=[]
    removed[i].append(cut.inside);next.append_array(cut.outside)
   else:next.append(piece)
  pieces=next
 return {"pieces":pieces,"removed":removed}
static func apply(map:Node)->Dictionary:
 if map.has_meta("hatch_backing_r21"):return map.get_meta("hatch_backing_r21")
 var record=F.for_map(map.map_id,str(map._meta.get("cache_fingerprint","")))
 var volumes=[];var center=Vector3.ZERO
 for source in record.get("tower_gate_grates",[]):
  if not str(source.opening_id).begins_with("mid_"):continue
  var frame=AABB();var started=false
  for part in source.parts:
   if part.key not in ["s14","s15"]:continue
   var b=F.aabb_from_min_max(part.min,part.max);frame=b if not started else frame.merge(b);started=true
  if not started:return {"status":"missing_frame","apertures":0}
  center+=frame.get_center()/10
  volumes.append({"id":str(source.opening_id),"center":frame.get_center(),"render_area":0.0,"collision_area":0.0,"attachments":[]})
 if volumes.is_empty():return {"status":"no_matching_hatches","apertures":0}
 if volumes.size()!=10:return {"status":"incomplete_hatches","apertures":0}
 for v in volumes:
  var n:Vector3=v.center-center;n.y=0;n=n.normalized();var tangent=Vector3(n.z,0,-n.x);v.normal=n;v.planes=[]
  for i in 8:
   var axis:Vector3=tangent*cos(PI*i/4)+Vector3.UP*sin(PI*i/4)
   v.planes.append(Plane(axis,axis.dot(v.center)+3.5))
  v.planes.append(Plane(n,n.dot(v.center)+.5));v.planes.append(Plane(-n,-n.dot(v.center)+3.0))
  v.bounds=AABB(v.center-Vector3.ONE*8,Vector3.ONE*16)
 var meshes=[];var shapes=[]
 for view in map._geometry.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null or view.mesh.get_surface_count()!=1:continue
  var xf=F._relative_transform(map._geometry,view);var bounds:AABB=xf*view.mesh.get_aabb();var near=[]
  for v in volumes:
   if bounds.intersects(v.bounds):near.append(v)
  if near.is_empty():continue
  var a=view.mesh.surface_get_arrays(0);var ix:PackedInt32Array=a[Mesh.ARRAY_INDEX] if a[Mesh.ARRAY_INDEX]!=null else PackedInt32Array(range(a[Mesh.ARRAY_VERTEX].size()))
  var out=a.duplicate(true);var kept=PackedInt32Array();var changed=false;var patches={}
  for tri in ix.size()/3:
   var poly=[_vertex(a,ix[tri*3]),_vertex(a,ix[tri*3+1]),_vertex(a,ix[tri*3+2])]
   var cut=_cut(poly,volumes,xf)
   if cut.removed.is_empty():kept.append_array(PackedInt32Array([ix[tri*3],ix[tri*3+1],ix[tri*3+2]]));continue
   changed=true
   for piece in cut.pieces:_append(piece,out,kept)
   for index in cut.removed:
    if not patches.has(index):patches[index]=[]
    patches[index].append_array(cut.removed[index])
  if not changed:continue
  out[Mesh.ARRAY_INDEX]=kept
  var mesh=ArrayMesh.new()
  if not kept.is_empty():mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,out)
  meshes.append({"node":view,"mesh":mesh})
  for index in patches:
   var arrays=a.duplicate(true)
   for i in arrays.size():
    if arrays[i]!=null:arrays[i].clear()
   var indices=PackedInt32Array()
   for poly in patches[index]:volumes[index].render_area+=_area(poly);_append(poly,arrays,indices)
   arrays[Mesh.ARRAY_INDEX]=indices
   # Authored map chunks have identity transforms; transform the patch explicitly
   # so the breakable attachment stays in map coordinates if a chunk is translated.
   for i in arrays[Mesh.ARRAY_VERTEX].size():arrays[Mesh.ARRAY_VERTEX][i]=xf*arrays[Mesh.ARRAY_VERTEX][i]
   if arrays[Mesh.ARRAY_NORMAL]!=null:
    for i in arrays[Mesh.ARRAY_NORMAL].size():arrays[Mesh.ARRAY_NORMAL][i]=(xf.basis*arrays[Mesh.ARRAY_NORMAL][i]).normalized()
   var patch=ArrayMesh.new();patch.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
   var material:Material=view.material_override if view.material_override!=null else view.mesh.surface_get_material(0)
   volumes[index].attachments.append({"mesh":patch,"material":material,"faces":patch.get_faces(),"source_view":str(map._geometry.get_path_to(view))})
 for node in map._geometry.find_children("*","CollisionShape3D",true,false):
  if not node.shape is ConcavePolygonShape3D:continue
  var xf=F._relative_transform(map._geometry,node);var bounds=F._shape_bounds(node.shape,xf);var near=false
  for v in volumes:
   if bounds.intersects(v.bounds):near=true;break
  if not near:continue
  var faces:PackedVector3Array=node.shape.get_faces();var kept=PackedVector3Array();var changed=false
  for tri in faces.size()/3:
   var cut=_cut([{"p":faces[tri*3]},{"p":faces[tri*3+1]},{"p":faces[tri*3+2]}],volumes,xf)
   if cut.removed.is_empty():kept.append_array(PackedVector3Array([faces[tri*3],faces[tri*3+1],faces[tri*3+2]]));continue
   changed=true
   for poly in cut.pieces:
    for i in range(1,poly.size()-1):kept.append_array(PackedVector3Array([poly[0].p,poly[i].p,poly[i+1].p]))
   for index in cut.removed:
    for poly in cut.removed[index]:volumes[index].collision_area+=_area(poly)
  if changed:shapes.append({"node":node,"faces":kept})
 for v in volumes:
  if v.render_area<1 or v.collision_area<1 or absf(v.render_area-v.collision_area)>.15:return {"status":"incomplete_backing","apertures":0,"opening":v.id,"render_area":v.render_area,"collision_area":v.collision_area}
 var serialized_meshes=[];var serialized_shapes=[]
 for plan in meshes:
  serialized_meshes.append({"path":str(map._geometry.get_path_to(plan.node)),"sha256":_digest(plan.node.mesh.get_faces()),"mesh":plan.mesh,"transform":plan.node.transform})
 # The new hatch cuts also alter these frame chunks. Bake native public index
 # LODs for them rather than discarding the existing distance-detail policy.
 for view in map._geometry.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null or view.material_override==null or view.material_override.resource_name not in ["s14","s15"]:continue
  serialized_meshes.append({"path":str(map._geometry.get_path_to(view)),"sha256":_digest(view.mesh.get_faces()),"mesh":view.mesh,"transform":view.transform})
 for plan in shapes:serialized_shapes.append({"path":str(map._geometry.get_path_to(plan.node)),"sha256":_digest(plan.node.shape.get_faces()),"faces":plan.faces,"transform":plan.node.transform})
 for plan in meshes:plan.node.mesh=plan.mesh
 for plan in shapes:
  var shape=plan.node.shape.duplicate();shape.set_faces(plan.faces);plan.node.shape=shape
 for row in map._details_r8.panels:
  for v in volumes:
   if str(row.get("opening_id_r18",""))==v.id:
    row.attachments=row.get("attachments",[]).duplicate();row.attachments.append_array(v.attachments)
 var attachment_plans={}
 for v in volumes:
  var parts=[]
  for attachment in v.attachments:
   parts.append({"mesh":attachment.mesh,"source_view":attachment.source_view})
  attachment_plans[v.id]=parts
 map.set_meta("hatch_bake_plans_r21",{"meshes":serialized_meshes,"shapes":serialized_shapes,"attachments":attachment_plans})
 var stats={"status":"ok","apertures":volumes.size(),"mesh_chunks":meshes.size(),"collision_chunks":shapes.size(),"areas":volumes.map(func(v):return [v.render_area,v.collision_area])}
 map.set_meta("hatch_backing_r21",stats);return stats

static func _digest(faces:PackedVector3Array)->String:
 var h=HashingContext.new();h.start(HashingContext.HASH_SHA256);h.update(faces.to_byte_array());return h.finish().hex_encode()
