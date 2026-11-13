extends SceneTree
const Helper=preload("res://scripts/archive_score_surfaces_r19.gd")
func _initialize():call_deferred("run")
func run():
 var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate()
 var fingerprint=catalog.manifest("archive:alfa-aglae-souza").cache_fingerprint
 var source=[]
 for view in scene.find_children("*","MeshInstance3D",true,false):source.append([view,view.mesh,view.transform,view.layers,view.get_groups()])
 assert(Helper.faces(scene,"archive:alfa-aglae-souza","invalid").is_empty())
 var rows=Helper.faces(scene,"archive:alfa-aglae-souza",fingerprint);assert(rows.size()==3)
 for i in 3:
  var a=rows[i].surface_get_arrays(0);var v=a[Mesh.ARRAY_VERTEX];var uv=a[Mesh.ARRAY_TEX_UV]
  assert(v.size()==6 and rows[i].get_aabb().size.y>10.3)
  assert((v[2]-v[1]).normalized().dot(Vector3.RIGHT)>.999)
  for p in v:assert(absf(p.z-27.881924)<.001 and p.y>21.832 and p.y<32.168)
  for n in a[Mesh.ARRAY_NORMAL]:assert(n.distance_to(Vector3.BACK)<.0001)
  for u in uv:assert(u.x>=0 and u.x<=1 and u.y>=0 and u.y<=1)
  assert(Helper.faces(scene,"archive:alfa-aglae-souza",fingerprint)[i].get_faces()==rows[i].get_faces())
 for saved in source:assert(saved[0].mesh==saved[1] and saved[0].transform==saved[2] and saved[0].layers==saved[3] and saved[0].get_groups()==saved[4])
 var view=scene.get_node("Draw_world_p8ad59c41_0_-1");var a=view.mesh.surface_get_arrays(0)
 a[Mesh.ARRAY_VERTEX][0]+=Vector3(.01,0,0);var changed=ArrayMesh.new();changed.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,a);view.mesh=changed
 assert(Helper.faces(scene,"archive:alfa-aglae-souza",fingerprint).is_empty())
 scene.free();print("R19_ALPHA_SCORE_APERTURES_AND_GUARDS_PASS faces=3 source meshes unchanged; bad fingerprint/mutated chunk rejected");quit()
