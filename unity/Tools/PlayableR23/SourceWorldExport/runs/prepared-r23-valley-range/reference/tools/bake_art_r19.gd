extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
const R19=preload("res://scripts/map_art_r19.gd")
func _initialize():call_deferred("run")
func vec(a)->Vector3:return Vector3(a[0],a[1],a[2])
func run():
 Catalog.catalog()
 var image=Image.load_from_file("res://assets/art_r19/freight.png");image.generate_mipmaps()
 assert(image.compress(Image.COMPRESS_ETC2)==OK,"Bake shared mobile texture")
 assert(ResourceSaver.save(ImageTexture.create_from_image(image),"res://assets/art_r19/freight_texture.res",ResourceSaver.FLAG_COMPRESS)==OK)
 var selectors=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_selectors_r19.json"));var index={}
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://assets/data/art_r19"))
 for slug in selectors.maps:
  var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate()
  preload("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  var fingerprint=Catalog.manifest("archive:"+slug).cache_fingerprint
  preload("res://scripts/map_art_r17.gd").new().apply(scene,"archive:"+slug,fingerprint)
  var plans={};var stats={}
  for view in scene.find_children("*","MeshInstance3D",true,false):
   if view.mesh==null or view.mesh.get_surface_count()!=1 or view.material_override==null:continue
   if view.material_override.resource_name!="vc" and not str(view.name).contains("s39"):continue
   var groups=preload("res://scripts/map_art_repair_r14.gd")._groups(view.mesh);var parts=[]
   for id in groups.rows:
    var component=groups.rows[id]
    var accepted=false
    for candidate in selectors.maps[slug]:
     if accepted:break
     var joined=false
     if candidate.kind=="wheel" and component.triangles==50:
      for other in selectors.maps[slug]:
       if other.kind!="wheel" or other==candidate:continue
       if component.box.get_center().distance_to((vec(candidate.center)+vec(other.center))*.5)<.025 and absf(vec(candidate.center).distance_to(vec(other.center))-1.21687)<.01:joined=true;break
     if not joined and (component.triangles!=int(candidate.triangles) or component.box.get_center().distance_to(vec(candidate.center))>.025):continue
     accepted=true
     var ids=PackedInt32Array();var uv=PackedVector3Array()
     var axis=vec(candidate.x);var depth=vec(candidate.z);var low=vec(candidate.lo);var size=vec(candidate.size).max(Vector3.ONE*.0001)
     if joined:
      # Two exact raw wheel profiles share axle vertices after conversion.
      # yz remains the original circular cross section; x is irrelevant to rubber/rim shading.
      low.y=component.box.position.y;size.y=component.box.size.y
     if candidate.kind in ["sign","banner"]:
      var normal=Vector3.ZERO
      for tri in groups.triangle_roots.size():
       if groups.triangle_roots[tri]!=id:continue
       var p0:Vector3=groups.faces[tri*3];var p1:Vector3=groups.faces[tri*3+1];var p2:Vector3=groups.faces[tri*3+2]
       normal=(p2-p0).cross(p1-p0).normalized();break
      axis=Vector3.UP.cross(normal).normalized();depth=normal
      var bounds=AABB(Vector3(component.box.get_center().dot(axis),component.box.position.y,0),Vector3.ZERO)
      var projected=PackedVector3Array()
      for tri in groups.triangle_roots.size():
       if groups.triangle_roots[tri]==id:
        for k in 3:
         var p:Vector3=groups.faces[tri*3+k];projected.append(Vector3(p.dot(axis),p.y,p.dot(depth)))
      bounds=AABB(projected[0],Vector3.ZERO)
      for p in projected:bounds=bounds.expand(p)
      low=bounds.position;size=bounds.size.max(Vector3.ONE*.0001)
     for tri in groups.triangle_roots.size():
      if groups.triangle_roots[tri]!=id:continue
      ids.append(tri)
      for k in 3:
       var p:Vector3=groups.faces[tri*3+k];uv.append((Vector3(p.dot(axis),p.y,p.dot(depth))-low)/size)
     parts.append({"kind":candidate.kind,"triangles":ids,"uv":uv});stats[candidate.kind]=int(stats.get(candidate.kind,0))+1
   if not parts.is_empty():plans[str(scene.get_path_to(view))]={"sha256":R19.digest(view.mesh),"parts":parts}
  var resource=Resource.new();resource.set_meta("plans",plans)
  var path="res://assets/data/art_r19/"+slug+".res"
  assert(ResourceSaver.save(resource,path,ResourceSaver.FLAG_COMPRESS)==OK)
  index[slug]={"path":path,"sha256":FileAccess.get_sha256(path),"fingerprint":fingerprint,"statistics":stats}
  print("R19_BAKED ",slug," ",stats);scene.free()
 var file=FileAccess.open("res://assets/data/art_index_r19.json",FileAccess.WRITE);file.store_string(JSON.stringify({"maps":index},"\t"));file.close();quit()
