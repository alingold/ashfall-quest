extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
const Art=preload("res://scripts/map_art_repair_r14.gd")
func _initialize():call_deferred("run")
func vector(a:Array)->Vector3:return Vector3(a[0],a[1],a[2])
func digest(mesh:Mesh)->String:
 var h=HashingContext.new();h.start(HashingContext.HASH_SHA256);h.update(mesh.get_faces().to_byte_array());return h.finish().hex_encode()
func run():
 var root_path=ProjectSettings.globalize_path("res://assets/data/art_r17/");var output_path=ProjectSettings.globalize_path("res://assets/data/art_index_r17.json")
 DirAccess.make_dir_recursive_absolute(root_path)
 var source=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_art_r17.json"));var index:Dictionary={}
 Catalog.catalog()
 for slug in source.maps:
  var row:Dictionary=source.maps[slug]
  if row.parts.is_empty():continue
  var buckets:Dictionary={}
  for part in row.parts:
   var cell=Vector3i((vector(part.center)/8).floor())
   if not buckets.has(cell):buckets[cell]=[]
   buckets[cell].append(part)
  var scene:Node=load("res://local_archive_r10/"+str(slug)+".res").geometry.instantiate();Art.apply(scene,"archive:"+str(slug))
  var plans:Dictionary={};var stats:Dictionary={}
  for view in scene.find_children("*","MeshInstance3D",true,false):
   if view.material_override==null or view.material_override.resource_name!="vc" or view.mesh.get_surface_count()==0:continue
   var groups:Dictionary=Art._groups(view.mesh);var selected:Dictionary={}
   for id in groups.rows:
    var component:Dictionary=groups.rows[id];var center:Vector3=component.box.get_center();var cell=Vector3i((center/8).floor());var found:Dictionary={}
    for x in range(-1,2):
     for y in range(-1,2):
      for z in range(-1,2):
       for part in buckets.get(cell+Vector3i(x,y,z),[]):
        if (int(part.triangles)==component.triangles and center.distance_squared_to(vector(part.center))<.025*.025) or (part.kind=="van" and int(part.triangles)==62 and component.triangles==98 and center.distance_squared_to(vector(part.center))<.2*.2) or (part.kind=="truck" and int(part.triangles)==44 and component.triangles==78 and center.distance_squared_to(vector(part.center))<.25*.25):found=part;break
    if not found.is_empty():selected[id]=found
   if selected.is_empty():continue
   var arrays:Array=view.mesh.surface_get_arrays(0);var ix=arrays[Mesh.ARRAY_INDEX];var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX];var parts:Array=[]
   for id in selected:
    var part:Dictionary=selected[id];var ids=PackedInt32Array();var uv=PackedVector3Array();var x=vector(part.x);var z=vector(part.z);var lo=vector(part.lo);var size=vector(part.size).max(Vector3.ONE*.0001)
    for i in groups.triangle_roots.size():
     if groups.triangle_roots[i]!=id:continue
     ids.append(i)
     for k in 3:
      var v:Vector3=vertices[ix[i*3+k] if ix!=null and not ix.is_empty() else i*3+k]
      uv.append((Vector3(v.dot(x),v.y,v.dot(z))-lo)/size)
    # The split raw cab joins adjacent faces during conversion. Fit all joined faces.
    if part.kind=="truck" and int(part.triangles)==44:
     var bounds=AABB(uv[0],Vector3.ZERO)
     for point in uv:bounds=bounds.expand(point)
     for i in uv.size():uv[i]=(uv[i]-bounds.position)/bounds.size.max(Vector3.ONE*.0001)
    parts.append({"kind":part.kind,"triangles":ids,"uv":uv});stats[part.kind]=int(stats.get(part.kind,0))+1
   plans[str(scene.get_path_to(view))]={"sha256":digest(view.mesh),"parts":parts}
  scene.free()
  var resource=Resource.new();resource.set_meta("plans",plans);resource.set_meta("statistics",stats)
  var path:String=root_path.path_join(str(slug)+".res");var error:int=ResourceSaver.save(resource,path,ResourceSaver.FLAG_COMPRESS)
  if error!=OK:push_error("Save failed "+str(error));quit(1);return
  index[slug]={"path":"res://assets/data/art_r17/"+str(slug)+".res","sha256":FileAccess.get_sha256(path),"cache_fingerprint":row.cache_fingerprint,"statistics":stats};print("ART_R17_BAKED ",slug," ",stats)
 var f=FileAccess.open(output_path,FileAccess.WRITE);f.store_string(JSON.stringify({"schema":1,"maps":index},"\t"));f.close();print("ART_R17_COMPLETE ",index.size());quit()



