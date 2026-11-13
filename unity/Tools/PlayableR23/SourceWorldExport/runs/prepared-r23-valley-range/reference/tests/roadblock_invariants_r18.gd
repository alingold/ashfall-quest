extends SceneTree
func _initialize():call_deferred("run")
func add_triangle(rows:Dictionary,arrays:Array,triangle:int):
 var indices=arrays[Mesh.ARRAY_INDEX];var key=""
 for k in 3:
  var at:int=indices[triangle*3+k] if indices!=null and not indices.is_empty() else triangle*3+k
  key+=PackedVector3Array([arrays[Mesh.ARRAY_VERTEX][at],arrays[Mesh.ARRAY_NORMAL][at]]).to_byte_array().hex_encode()
  for slot in [Mesh.ARRAY_TEX_UV,Mesh.ARRAY_TEX_UV2]:key+=PackedVector2Array([arrays[slot][at] if arrays[slot]!=null else Vector2.ZERO]).to_byte_array().hex_encode()
  key+=PackedColorArray([arrays[Mesh.ARRAY_COLOR][at]]).to_byte_array().hex_encode()
 rows[key]=int(rows.get(key,0))+1
func all_triangles(scene:Node)->Dictionary:
 var rows={}
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null:continue
  var triangles:PackedVector3Array=view.mesh.get_faces()
  for i in range(0,triangles.size(),3):
   var key=PackedVector3Array([triangles[i],triangles[i+1],triangles[i+2]]).to_byte_array().hex_encode();rows[key]=int(rows.get(key,0))+1
 return rows
func identities(scene:Node)->Array:
 var rows=[]
 for node in scene.find_children("*","CollisionShape3D",true,false):rows.append([node.get_path(),node.transform,node.shape,node.get_groups()])
 for node in scene.find_children("*","CollisionObject3D",true,false):rows.append([node.get_path(),node.transform,node.collision_layer,node.collision_mask,node.get_groups()])
 for node in scene.find_children("*","MeshInstance3D",true,false):rows.append([node.get_path(),node.transform,node.layers,node.get_groups()])
 return rows
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r17.json"))
 var tested=0;var total=0
 for slug in index.maps:
  var row=index.maps[slug];var expected:int=int(row.statistics.get("roadblock",0))
  if expected==0:continue
  assert(FileAccess.get_sha256(row.path)==row.sha256)
  var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();root.add_child(scene)
  load("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  var before=all_triangles(scene);var original_nodes=identities(scene)
  var source_roadblocks={};var plans=load(row.path).get_meta("plans")
  for path in plans:
   var arrays=scene.get_node(path).mesh.surface_get_arrays(0)
   for part in plans[path].parts:
    if part.kind!="roadblock":continue
    for triangle in part.triangles:add_triangle(source_roadblocks,arrays,triangle)
  var helper=load("res://scripts/map_art_r17.gd").new()
  assert(helper.apply(scene,"archive:"+slug,"changed-fingerprint").is_empty())
  var stats=helper.apply(scene,"archive:"+slug,row.cache_fingerprint)
  assert(stats.roadblock==expected and stats.guard_skips==0)
  assert(all_triangles(scene)==before,"All ordered source triangles must survive")
  for original in original_nodes:
   var node=scene.get_node(original[0])
   assert(node.transform==original[1])
   if node is CollisionShape3D:assert(node.shape==original[2] and node.get_groups()==original[3])
   elif node is CollisionObject3D:assert(node.collision_layer==original[2] and node.collision_mask==original[3] and node.get_groups()==original[4])
   else:assert(node.layers==original[2] and node.get_groups()==original[3])
  var detailed_roadblocks={};var chunks=0
  for view in scene.find_children("*","MeshInstance3D",true,false):
   if view.material_override==null or view.material_override.resource_name!="OriginalSurfaceR17_roadblock":continue
   chunks+=1;var arrays=view.mesh.surface_get_arrays(0)
   for i in view.mesh.get_faces().size()/3:add_triangle(detailed_roadblocks,arrays,i)
  if detailed_roadblocks!=source_roadblocks:
   for key in source_roadblocks:
    if not detailed_roadblocks.has(key):print("SOURCE_ONLY ",key);break
   for key in detailed_roadblocks:
    if not source_roadblocks.has(key):print("DETAIL_ONLY ",key);break
   quit(1);return
  assert(helper.apply(scene,"archive:"+slug,row.cache_fingerprint)==stats)
  print("R18_ROADBLOCK_INVARIANTS_PASS ",slug," parts=",expected," chunks=",chunks)
  tested+=1;total+=expected;scene.free()
 assert(tested==9 and total==243)
 print("R18_ROADBLOCK_TOTAL maps=",tested," parts=",total)
 quit()
