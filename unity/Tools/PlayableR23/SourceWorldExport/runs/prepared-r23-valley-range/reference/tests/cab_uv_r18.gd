extends SceneTree
func _initialize():call_deferred("run")
func run():
 load("res://scripts/archive_catalog1003.gd").catalog()
 var index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r17.json"))
 var count=0
 for slug in ["alfa-aglae-souza","bigbabyjesuz-classic-kiki-80","classic-bw-187norvis","cont-cl-er-ssd-cjcoops","cont-cl-ssd-cjcoops","statmaster-classic-soulsmight"]:
  var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate()
  load("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  var plans=load(index.maps[slug].path).get_meta("plans");var found=0
  for path in plans:
   var view=scene.get_node(path);var arrays=view.mesh.surface_get_arrays(0);var indices=arrays[Mesh.ARRAY_INDEX]
   for part in plans[path].parts:
    if part.kind!="truck" or part.triangles.size()!=78:continue
    var box=AABB();var points=0
    for triangle in part.triangles:
     for k in 3:
      var at:int=indices[triangle*3+k] if indices!=null and not indices.is_empty() else triangle*3+k
      var point:Vector3=arrays[Mesh.ARRAY_VERTEX][at];box=AABB(point,Vector3.ZERO) if points==0 else box.expand(point);points+=1
    if box.get_center().distance_to(Vector3(-359.4039,36.3051,280.6427))>.25:continue
    var low=Vector3(INF,INF,INF);var high=Vector3(-INF,-INF,-INF)
    assert(part.uv.size()==234)
    for uv in part.uv:assert(uv.is_finite());low=low.min(uv);high=high.max(uv)
    assert(low.is_equal_approx(Vector3.ZERO) and high.is_equal_approx(Vector3.ONE))
    var subset=load("res://scripts/source_surface_subset_r18.gd")
    assert(subset.build(view.mesh,PackedInt32Array())==null)
    assert(subset.build(view.mesh,PackedInt32Array([-1]))==null)
    assert(subset.build(view.mesh,PackedInt32Array([arrays[Mesh.ARRAY_VERTEX].size()]))==null)
    found+=1
  assert(found==1)
  count+=1;scene.free();print("R18_CAB_UV_AND_SUBSET_REJECTION_PASS ",slug)
 assert(count==6);quit()
