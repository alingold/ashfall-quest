extends SceneTree
func _initialize():call_deferred("run")
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var scene=load("res://local_archive_r10/battlefield-paintball-gods-pitdevil.res").geometry.instantiate()
 var data:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://tests/fixtures/paintball_frame_measured_r18.json"));var helper=load("res://scripts/archive_score_surfaces_r18.gd")
 var physics=[]
 for shape in scene.find_children("*","CollisionShape3D",true,false):physics.append([shape,shape.shape,shape.transform])
 assert(helper.faces(scene,data.map,"invalid").is_empty())
 var rows=helper.faces(scene,data.map,data.cache_fingerprint)
 assert(rows.size()==1 and rows[0].get_faces().size()==6)
 var mesh:Mesh=rows[0];var arrays=mesh.surface_get_arrays(0)
 for point in arrays[Mesh.ARRAY_VERTEX]:
  assert(absf(point.x-float(data.aperture.plane_x))<.00001)
  assert(point.y>=float(data.aperture.y_min)-.00001 and point.y<=float(data.aperture.y_max)+.00001)
  assert(point.z>=float(data.aperture.z_min)-.00001 and point.z<=float(data.aperture.z_max)+.00001)
 for normal in arrays[Mesh.ARRAY_NORMAL]:assert(normal.dot(Vector3.RIGHT)>.9999)
 for uv in arrays[Mesh.ARRAY_TEX_UV]:assert(uv.x>=0 and uv.x<=1 and uv.y>=0 and uv.y<=1)
 assert(helper.faces(scene,data.map,data.cache_fingerprint)[0].get_faces()==mesh.get_faces())
 for saved in physics:assert(saved[0].shape==saved[1] and saved[0].transform==saved[2])
 var view=scene.get_node(data.source_frames[0].path);var old=view.mesh;var changed=old.surface_get_arrays(0);changed[Mesh.ARRAY_VERTEX][0]+=Vector3(.01,0,0);var edited=ArrayMesh.new();edited.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,changed);view.mesh=edited
 assert(helper.faces(scene,data.map,data.cache_fingerprint).is_empty())
 view.mesh=old
 print("R18_PAINTBALL_FRAME_PROBE_PASS exact aperture, +X normal, UVs, no collision mutation, fingerprint/hash rejection; native-verified production helper")
 scene.free();quit()
