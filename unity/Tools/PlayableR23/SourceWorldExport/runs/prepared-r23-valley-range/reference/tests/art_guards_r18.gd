extends SceneTree
func _initialize():call_deferred("run")
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r17.json"))
 for slug in index.maps:
  var row=index.maps[slug]
  assert(FileAccess.get_sha256(row.path)==row.sha256)
  assert(row.cache_fingerprint==catalog.manifest("archive:"+slug).cache_fingerprint)
  var plans=load(row.path).get_meta("plans")
  assert(not plans.is_empty())
  for plan in plans.values():assert(not plan.parts.is_empty())
 var slug="alfa-aglae-souza"
 var scene=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();root.add_child(scene)
 load("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
 var plans=load(index.maps[slug].path).get_meta("plans")
 var view=scene.get_node(plans.keys()[0]);var arrays=view.mesh.surface_get_arrays(0)
 var indices=arrays[Mesh.ARRAY_INDEX]
 var at:int=indices[0] if indices!=null and not indices.is_empty() else 0
 arrays[Mesh.ARRAY_VERTEX][at]+=Vector3(.01,0,0)
 var changed=ArrayMesh.new();changed.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays);view.mesh=changed
 var stats=load("res://scripts/map_art_r17.gd").new().apply(scene,"archive:"+slug,index.maps[slug].cache_fingerprint)
 assert(stats.guard_skips==1,"Changed chunk must fail closed")
 print("R18_ART_RESOURCE_HASHES_AND_MUTATION_GUARD_PASS maps=",index.maps.size())
 scene.free();quit()

