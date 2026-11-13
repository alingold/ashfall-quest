extends SceneTree
const Harness=preload("res://tools/test_map_fixtures_r18.gd")
const F=preload("res://scripts/map_fixtures_r18.gd")
func _initialize():call_deferred("run")
func public_lods(mesh:Mesh)->ArrayMesh:
 var importer=ImporterMesh.new()
 for i in mesh.get_surface_count():importer.add_surface(Mesh.PRIMITIVE_TRIANGLES,mesh.surface_get_arrays(i))
 importer.generate_lods(60.0,0.0,[])
 var result=ArrayMesh.new()
 for surface in importer.get_surface_count():
  var a=mesh.surface_get_arrays(surface);var lods={};var count=a[Mesh.ARRAY_INDEX].size() if a[Mesh.ARRAY_INDEX]!=null else a[Mesh.ARRAY_VERTEX].size()
  for level in importer.get_surface_lod_count(surface):
   var ids=importer.get_surface_lod_indices(surface,level)
   if ids.size()>=3 and ids.size()<count:lods[importer.get_surface_lod_size(surface,level)]=ids
  result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,a,[],lods)
 return result
func run():
 var C=preload("res://scripts/archive_catalog1003.gd");C.catalog()
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://assets/data/hatch_backing_r21"))
 var index={"schema":1,"scope":"Exact octagonal backing patches, plus public ImporterMesh index LODs for affected wall/frame chunks. No private renderer data or runtime clipping.","maps":{}}
 for slug in F.catalog().maps:
  var data=F.catalog().maps[slug]
  if data.get("tower_gate_grates",[]).is_empty():continue
  var original=load("res://local_archive_r10/"+slug+".res")
  var map=Harness.DummyMap.new();map.map_id="archive:"+slug;map._geometry=original.geometry.instantiate();map._meta={"cache_fingerprint":original.source_fingerprint};map._details_r8=original.details.duplicate(false);root.add_child(map);map.add_child(map._geometry)
  preload("res://scripts/map_art_repair_r14.gd").apply(map._geometry,map.map_id)
  preload("res://scripts/map_art_r17.gd").new().apply(map._geometry,map.map_id,original.source_fingerprint)
  preload("res://scripts/map_art_r19.gd").new().apply(map._geometry,map.map_id,original.source_fingerprint)
  var semantics=preload("res://scripts/map_semantics_r14.gd").new();map.add_child(semantics);map.semantics_r14=semantics;semantics.setup(map);semantics.set_physics_process(false)
  var stats=F.apply(map);assert(stats.status=="ok")
  var result=preload("res://tools/cut_hatch_backing_r21.gd").apply(map);assert(result.status=="ok",JSON.stringify(result))
  var plans:Dictionary=map.get_meta("hatch_bake_plans_r21")
  for plan in plans.meshes:plan.mesh=public_lods(plan.mesh)
  result["public_lod_meshes"]=plans.meshes.size()
  var resource=Resource.new();resource.set_meta("plans",plans);resource.set_meta("statistics",result)
  var path="res://assets/data/hatch_backing_r21/"+slug+".res"
  assert(ResourceSaver.save(resource,path,ResourceSaver.FLAG_COMPRESS)==OK)
  index.maps[slug]={"path":path,"sha256":FileAccess.get_sha256(path),"fingerprint":original.source_fingerprint}
  print("R21_BACKING_BAKED ",slug," ",JSON.stringify(result));map.free();await process_frame
 var f=FileAccess.open("res://assets/data/hatch_backing_r21.json",FileAccess.WRITE);f.store_string(JSON.stringify(index,"  "));f.close();quit()
