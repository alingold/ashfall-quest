extends SceneTree
const Harness=preload("res://tools/test_map_fixtures_r18.gd")
const F=preload("res://scripts/map_fixtures_r18.gd")
const Passage=preload("res://scripts/archive_panel_r8.gd")
var checks=0
var failures=[]
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func _initialize():call_deferred("run")
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 var maps=0;var tested=0
 for slug in F.catalog().maps:
  var data=F.catalog().maps[slug]
  if data.get("tower_gate_grates",[]).is_empty():continue
  var original=load("res://local_archive_r10/"+slug+".res")
  var old_count=original.details.panels.size()
  var map=Harness.DummyMap.new();map.map_id="archive:"+slug;map._geometry=original.geometry.instantiate();map._meta={"cache_fingerprint":original.source_fingerprint};map._details_r8=original.details.duplicate(false);root.add_child(map);map.add_child(map._geometry)
  var rejected=F.apply_tower_grates(map._geometry,map._details_r8,map.map_id,"wrong")
  check(not rejected.supported and map._details_r8.panels.size()==old_count,slug+" wrong fingerprint makes no cuts")
  preload("res://scripts/map_art_repair_r14.gd").apply(map._geometry,map.map_id)
  preload("res://scripts/map_art_r17.gd").new().apply(map._geometry,map.map_id,original.source_fingerprint)
  preload("res://scripts/map_art_r19.gd").new().apply(map._geometry,map.map_id,original.source_fingerprint)
  var semantics=preload("res://scripts/map_semantics_r14.gd").new();map.add_child(semantics);map.semantics_r14=semantics;semantics.setup(map);semantics.set_physics_process(false)
  var stats=F.apply(map)
  check(stats.get("status","")=="ok",slug+" complete extraction "+JSON.stringify(stats))
  check(stats.get("extracted_openings",0)==18 and stats.get("removed_visual_triangles",0)==2168 and stats.get("removed_collision_triangles",0)==2168,slug+" old368 plus new1800 triangles removed from render and collision")
  check(original.details.panels.size()==old_count,slug+" cached details remain immutable")
  if stats.get("status","")!="ok":map.free();continue
  var backing=preload("res://scripts/hatch_backing_r21.gd").apply(map)
  check(backing.get("status","")=="ok",slug+" backing cut "+JSON.stringify(backing))
  var center=Vector3.ZERO
  for spec in data.tower_gate_grates:
   if str(spec.opening_id).begins_with("mid_"):center+=F.vector(spec.center)/10
  var rows=[]
  for row in map._details_r8.panels:
   if row.get("fixture_kind_r18","")!="tower_mid_hatch":continue
   var panel=Passage.new();map.add_child(panel);panel.setup(map,"hatch"+str(rows.size()),row);panel.set_physics_process(false);rows.append(panel)
  check(rows.size()==10,slug+" ten separate authoritative breakable bodies")
  await physics_frame;await physics_frame
  for panel in rows:
   var at:Vector3=panel.box.get_center();var normal=at-center;normal.y=0;normal=normal.normalized()
   var query=PhysicsRayQueryParameters3D.create(at+normal*5,at-normal*5,1)
   var before=map.get_world_3d().direct_space_state.intersect_ray(query)
   check(not before.is_empty() and before.collider==panel,slug+" closed hatch blocks shot "+panel.key)
   panel.take_damage(1)
   await physics_frame;await physics_frame
   var after=map.get_world_3d().direct_space_state.intersect_ray(query)
   check(after.is_empty(),slug+" destroyed hatch opens full passage "+panel.key+str(after.get("position","")))
   var shape=CapsuleShape3D.new();shape.height=1.7;shape.radius=.28
   var sweep=PhysicsShapeQueryParameters3D.new();sweep.shape=shape;sweep.collision_mask=1;sweep.transform=Transform3D(Basis.IDENTITY,at+normal*4+Vector3.UP*.4);sweep.motion=-normal*8
   var fraction=map.get_world_3d().direct_space_state.cast_motion(sweep)
   check(fraction[0]>.99,slug+" player capsule passes opening "+panel.key+str(fraction))
   tested+=1
  var again=F.apply_tower_grates(map._geometry,map._details_r8,map.map_id,original.source_fingerprint)
  check(again.get("idempotent_skip",false) and map._details_r8.panels.size()==old_count+18,slug+" repeat extraction is idempotent")
  maps+=1;map.free();await process_frame
  print("R21_HATCH_MAP ",slug," tested=10")
 check(maps==9 and tested==90,"nine original variants and ninety middle hatches")
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/TOWER-HATCHES-R21.json",FileAccess.WRITE);f.store_string(JSON.stringify({"checks":checks,"failures":failures,"maps":maps,"hatches":tested,"headset":false},"  "));f.close()
 print("R21_HATCHES checks=",checks," failures=",failures.size());quit(failures.size())
