extends SceneTree
## Actual READY diagnostic only: no generated displays, geometry/material edits or production hooks.
var game:Node
func _initialize():call_deferred("run")
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 game=load("res://scripts/main.gd").new();root.add_child(game)
 game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
 var deadline=Time.get_ticks_msec()+90000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready:break
 var map=game.active_sandbox_map;assert(is_instance_valid(map) and map.map_ready)
 var candidates=[Vector3(85.50193,30.25,79.11807),Vector3(42.38192,28.69290,82.50241),Vector3(42.38192,30.89763,82.50246),Vector3(42.78363,27,27.88192),Vector3(49.22964,27,27.88192),Vector3(36.24113,27,27.88192)]
 var rows=[]
 for center in candidates:
  var parts=[];var chunks={};var aperture=AABB()
  for view in map._geometry.find_children("*","MeshInstance3D",true,false):
   if view.mesh==null or view.mesh.get_surface_count()==0 or view.material_override==null or view.material_override.resource_name not in ["p8ad59c41","pd959969c"]:continue
   var groups=preload("res://scripts/map_art_repair_r14.gd")._groups(view.mesh)
   for part in groups.rows.values():
    if part.box.get_center().distance_to(center)>.2 or part.triangles not in [12,16,20]:continue
    parts.append({"material":view.material_override.resource_name,"triangles":part.triangles,"bounds":str(part.box)})
    var path=str(map._geometry.get_path_to(view));chunks[path]={"sha256":preload("res://scripts/map_art_r19.gd").digest(view.mesh),"transform":str(view.transform),"layers":view.layers,"groups":view.get_groups()}
    if part.triangles==12:aperture=part.box
  print("R19_ALPHA_LIVE_FRAME ",center," parts=",parts," chunks=",chunks)
  assert(parts.size()==4,"Complete four-part source frame survives actual READY")
  rows.append({"center":str(center),"parts":parts,"chunks":chunks,"aperture_bounds":str(aperture),"status":"Diagnostic source geometry only; no scoreboard enabled"})
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/alpha-live-score-frame-candidates-r19.json",FileAccess.WRITE)
 file.store_string(JSON.stringify({"cache_fingerprint":map._meta.cache_fingerprint,"map_origin":str(map.global_position),"frames":rows},"\t"));file.close()
 print("R19_ALPHA_LIVE_FRAMES_PASS count=",rows.size()," production_scoreboards=",map.find_children("AuthoredWorldScoreR9","Node3D",true,false).size())
 game._quitting=true;game.queue_free()
 for i in 8:await process_frame
 quit()
