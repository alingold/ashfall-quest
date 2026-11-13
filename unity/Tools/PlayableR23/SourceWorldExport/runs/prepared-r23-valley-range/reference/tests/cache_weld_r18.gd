extends SceneTree
const Components=preload("res://scripts/archive_semantics_r8.gd")
var game:Node
func _initialize():call_deferred("run")
func vec(v:Vector3)->Array:return [v.x,v.y,v.z]
func run():
 var slug="alfa-aglae-souza"
 for arg in OS.get_cmdline_user_args():
  if arg.begins_with("--audit-map="):slug=arg.trim_prefix("--audit-map=")
 var id="archive:"+slug;game=load("res://scripts/main.gd").new();root.add_child(game)
 game.player.set_physics_process(false)
 var net:Node=game.network_bridge.net
 if net.host("Isolated marker audit",25900+absi(slug.hash())%100,"r18-audit")!=OK:quit(2);return
 net.request("rules",{"map":id,"mode":"respawn","teams":2,"zone_enabled":false})
 var deadline:int=Time.get_ticks_msec()+120000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if net.stage=="lobby" and is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not game.network_bridge._preparing:break
 var map:Node=game.active_sandbox_map
 if not is_instance_valid(map) or not map.map_ready:quit(3);return
 var catalog:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_caches_r17.json"));var sites:Array=catalog.maps[slug].sites
 var rows:Array=[];var total_components=0;var total_triangles=0
 for view in map._geometry.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null or view.material_override==null or view.material_override.resource_name!="p2a61c272":continue
  var groups:Dictionary=Components._components(view.mesh);var relative:Transform3D=map.global_transform.affine_inverse()*view.global_transform
  for component in groups.rows.values():
   total_components+=1;total_triangles+=int(component.triangles)
   var box:AABB=relative*component.box
   if box.size.y>.12 or maxf(box.size.x,box.size.z)<2.6 or maxf(box.size.x,box.size.z)>4.1 or int(component.triangles)<10:continue
   var nearest=INF;var site_at=Vector3.INF
   for source in sites:
    var point=Vector3(source[0],source[1],source[2]);var distance:float=point.distance_to(box.get_center())
    if distance<nearest:nearest=distance;site_at=point
   rows.append({"view":str(map._geometry.get_path_to(view)),"triangles":component.triangles,"center":vec(box.get_center()),"size":vec(box.size),"nearest_source_cache_distance":nearest,"nearest_source_cache":vec(site_at),"visible":view.is_visible_in_tree()})
 var floor_hits=0
 for part in catalog.maps[slug].marker_parts:
  if not part.has("source_members_r18"):continue
  var at=Vector3(part.center[0],part.center[1],part.center[2])+map.global_position
  var floor:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(at+Vector3.UP*.3,at-Vector3.UP*2,1))
  if not floor.is_empty() and floor.normal.y>.8:floor_hits+=1
 var targeted_remaining=0
 for part in catalog.maps[slug].marker_parts:
  if not part.has("source_members_r18"):continue
  var at=Vector3(part.center[0],part.center[1],part.center[2])
  for present in rows:
   var center=Vector3(present.center[0],present.center[1],present.center[2])
   if int(present.triangles)==int(part.triangles) and center.distance_to(at)<.0045:targeted_remaining+=1
 var result={"targeted_remaining":targeted_remaining,"underlying_floor_probes":floor_hits,"cache_catalog_sha256":FileAccess.get_sha256("res://assets/data/map_caches_r17.json"),"map":id,"cache_fingerprint":map._meta.cache_fingerprint,"hosted":net.active,"remaining_red_components":total_components,"remaining_red_triangles":total_triangles,"flat_marker_sized_candidates":rows,"removed_visual_triangles":map.semantics_r14.removed_visual_triangles,"removed_collision_triangles":map.semantics_r14.removed_collision_triangles,"source_cache_candidates":sites.size(),"functional_cache_sites":map.gameplay_r8.sites.size(),"scope":"Read-only runtime geometry audit in a fresh isolated hosted map. Material key and flat bounds are candidates, not proof of marker intent or a complete all-map fix. No source or build changed."}
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RED-MARKER-AUDIT-R18-"+slug+".json",FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close()
 print("RED_MARKER_AUDIT_R18 remaining_red_components=",total_components," marker_sized_candidates=",rows.size()," removed_visual=",map.semantics_r14.removed_visual_triangles)
 net.close(false);game._quitting=true;game.queue_free();for i in 6:await process_frame
 quit(0 if targeted_remaining==0 and floor_hits==2 else 1)
