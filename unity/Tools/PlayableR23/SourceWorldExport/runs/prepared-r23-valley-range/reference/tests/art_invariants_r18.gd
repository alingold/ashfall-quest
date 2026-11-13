extends SceneTree
func _initialize():call_deferred("run")
func faces(root_node:Node)->Dictionary:
 var rows={}
 for view in root_node.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null:continue
  var triangles:PackedVector3Array=view.mesh.get_faces()
  for i in range(0,triangles.size(),3):
   var key=str(triangles[i])+str(triangles[i+1])+str(triangles[i+2]);rows[key]=int(rows.get(key,0))+1
 return rows
func collisions(root_node:Node)->Array:
 var rows=[]
 for shape in root_node.find_children("*","CollisionShape3D",true,false):rows.append([shape.get_path(),shape.transform,shape.shape])
 return rows
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r17.json"))
 for slug in ["alfa-aglae-souza","bigbabyjesuz-classic-kiki-80","classic-bw-187norvis","cont-cl-er-ssd-cjcoops","cont-cl-ssd-cjcoops","statmaster-classic-soulsmight"]:
  var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();root.add_child(scene)
  load("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  var before=faces(scene);var physics=collisions(scene)
  var art=load("res://scripts/map_art_r17.gd").new()
  assert(art.apply(scene,"archive:"+slug,"invalid-fingerprint").is_empty())
  var stats=art.apply(scene,"archive:"+slug,index.maps[slug].cache_fingerprint)
  assert(stats.guard_skips==0)
  assert(stats.truck==8)
  assert(faces(scene)==before,"Ordered source triangles must be preserved exactly")
  assert(collisions(scene)==physics,"Collision objects and transforms must be untouched")
  assert(art.apply(scene,"archive:"+slug,index.maps[slug].cache_fingerprint)==stats)
  print("R18_ART_INVARIANTS_PASS ",slug," ",stats)
  scene.free()
 quit()


