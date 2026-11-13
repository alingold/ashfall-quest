extends SceneTree
func _initialize():call_deferred("run")
func run():
 load("res://scripts/archive_catalog1003.gd").catalog()
 var slug="melipop-gods-poplightning"
 var resource=load("res://local_archive_r10/"+slug+".res")
 var geometry=resource.geometry.instantiate();root.add_child(geometry)
 var details=load("res://scripts/archive_semantics_r9.gd").prepare(geometry,"archive:"+slug,{})
 print("SCORE_FIXTURE faces=",details.score_faces_r9.size()," candidates=",details.screen_candidates_r9.size())
 for mesh in details.score_faces_r9:
  print("SCORE_FACE box=",mesh.get_aabb()," uv=",mesh.surface_get_arrays(0)[Mesh.ARRAY_TEX_UV])
 geometry.free();quit()

