extends SceneTree
## Effective shader U must increase toward exterior camera-right. Source normals face inward.
func _initialize():call_deferred("run")
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 var index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r19.json"));var total=0
 assert(FileAccess.get_file_as_string("res://assets/materials/assembly_r19.gdshader").contains("vec2(1.-UV.x,UV.y)"))
 for slug in index.maps:
  var row=index.maps[slug];var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate()
  preload("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  preload("res://scripts/map_art_r17.gd").new().apply(scene,"archive:"+slug,row.fingerprint)
  var stats=preload("res://scripts/map_art_r19.gd").new().apply(scene,"archive:"+slug,row.fingerprint);assert(stats.guard_skips==0)
  var centers=[]
  for node in scene.find_children("OriginalAssemblyR19_sign*","MeshInstance3D",true,false):
   var v=node.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
   for i in range(0,v.size(),6):
    var center=Vector3.ZERO
    for k in 6:center+=v[i+k]/6.0
    centers.append(center)
  for node in scene.find_children("OriginalAssemblyR19_sign*","MeshInstance3D",true,false):
   var a=node.mesh.surface_get_arrays(0);var v=a[Mesh.ARRAY_VERTEX];var uv=a[Mesh.ARRAY_TEX_UV];var normals=a[Mesh.ARRAY_NORMAL]
   for i in range(0,v.size(),3):
    var center=Vector3.ZERO;var start=i-i%6
    for k in 6:center+=v[start+k]/6.0
    var outward=-normals[i].normalized();var paired=false
    for other in centers:
     var delta:Vector3=center-other
     if delta.length()>.2 and delta.length()<10 and delta.normalized().dot(outward)>.999:paired=true;break
    assert(paired,"Opposite billboard plate establishes the exterior side independently of inward source normals")
    var duv1=Vector2(-(uv[i+1].x-uv[i].x),uv[i+1].y-uv[i].y)
    var duv2=Vector2(-(uv[i+2].x-uv[i].x),uv[i+2].y-uv[i].y)
    var determinant=duv1.x*duv2.y-duv2.x*duv1.y;assert(absf(determinant)>.1)
    var right=((v[i+1]-v[i])*duv2.y-(v[i+2]-v[i])*duv1.y)/determinant
    assert(right.normalized().dot(Vector3.UP.cross(outward).normalized())>.999,"Readable exterior text U orientation")
    total+=1
  scene.free()
 assert(total==400);print("R19_SIGN_FRONT_U_ORIENTATION_PASS triangles=",total," plates=",total/2);quit()
