extends SceneTree
const Art=preload("res://scripts/map_art_r19.gd")
func _initialize():call_deferred("run")
func triangles(scene:Node)->Dictionary:
 var result={}
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null:continue
  var f=view.mesh.get_faces()
  for i in range(0,f.size(),3):
   var key=PackedVector3Array([f[i],f[i+1],f[i+2]]).to_byte_array().hex_encode();result[key]=int(result.get(key,0))+1
 return result
func normals(scene:Node,paths:Dictionary)->Dictionary:
 var result={}
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if not paths.has(str(scene.get_path_to(view))) and not str(view.name).begins_with("OriginalAssemblyR19_"):continue
  if view.mesh==null or view.mesh.get_surface_count()==0:continue
  var a=view.mesh.surface_get_arrays(0);var v=a[Mesh.ARRAY_VERTEX];var n=a[Mesh.ARRAY_NORMAL]
  if n==null:continue
  for i in v.size():
   var key=PackedVector3Array([v[i]]).to_byte_array().hex_encode()
   if not result.has(key):result[key]=[]
   result[key].append(n[i])
 return result
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 var index=JSON.parse_string(FileAccess.get_file_as_string(Art.INDEX))
 for slug in index.maps:
  var row=index.maps[slug];var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();root.add_child(scene)
  preload("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  preload("res://scripts/map_art_r17.gd").new().apply(scene,"archive:"+slug,row.fingerprint)
  var plans:Dictionary=load(row.path).get_meta("plans")
  var before=triangles(scene);var source_normals=normals(scene,plans);var identities=[];var bodies=[]
  for node in scene.find_children("*","CollisionObject3D",true,false):bodies.append([node,node.transform,node.collision_layer,node.collision_mask,node.get_groups()])
  for node in scene.find_children("*","CollisionShape3D",true,false):identities.append([node,node.shape,node.transform])
  var render=[]
  for node in scene.find_children("*","MeshInstance3D",true,false):render.append([node,node.transform,node.layers,node.get_groups(),node.cast_shadow])
  var helper=Art.new();assert(helper.apply(scene,"archive:"+slug,"bad").is_empty())
  var stats=helper.apply(scene,"archive:"+slug,row.fingerprint)
  assert(stats.guard_skips==0)
  for kind in row.statistics:assert(stats[kind]==int(row.statistics[kind]))
  assert(triangles(scene)==before,"Exact ordered source faces must survive R19")
  var after_normals=normals(scene,plans)
  for key in after_normals:
   for n in after_normals[key]:
    var nearest=INF
    for original in source_normals[key]:nearest=minf(nearest,n.distance_to(original))
    assert(nearest<.0002,"Source normal must survive within octahedral encoding precision")
  for item in identities:assert(item[0].shape==item[1] and item[0].transform==item[2])
  for item in bodies:assert(item[0].transform==item[1] and item[0].collision_layer==item[2] and item[0].collision_mask==item[3] and item[0].get_groups()==item[4])
  for item in render:assert(item[0].transform==item[1] and item[0].layers==item[2] and item[0].get_groups()==item[3] and item[0].cast_shadow==item[4])
  var count=scene.find_children("*","MeshInstance3D",true,false).size()
  assert(helper.apply(scene,"archive:"+slug,row.fingerprint)==stats)
  assert(scene.find_children("*","MeshInstance3D",true,false).size()==count)
  print("R19_INVARIANTS_PASS ",slug," ",stats)
  scene.free()
 # Mutate one selected chunk on a fresh Alpha instance; reject it, retaining other guards.
 var slug="alfa-aglae-souza";var row=index.maps[slug]
 var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();root.add_child(scene)
 preload("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
 preload("res://scripts/map_art_r17.gd").new().apply(scene,"archive:"+slug,row.fingerprint)
 var plans=load(row.path).get_meta("plans");var view=scene.get_node(plans.keys()[0]);var arrays=view.mesh.surface_get_arrays(0)
 arrays[Mesh.ARRAY_VERTEX][0]+=Vector3(.01,0,0);var mesh=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays);view.mesh=mesh
 assert(Art.new().apply(scene,"archive:"+slug,row.fingerprint).guard_skips==1)
 scene.free();print("R19_MUTATION_GUARD_PASS");quit()
