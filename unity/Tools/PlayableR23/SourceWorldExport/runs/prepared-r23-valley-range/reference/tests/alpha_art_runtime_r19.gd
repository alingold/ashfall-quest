extends SceneTree
var game:Node
func _initialize():call_deferred("run")
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 game=load("res://scripts/main.gd").new();root.add_child(game)
 game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
 var deadline=Time.get_ticks_msec()+90000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready:break
 var map=game.active_sandbox_map
 assert(is_instance_valid(map) and map.map_ready,"Actual Alpha READY timed out")
 print("R19_ALPHA_ACTUAL_STATISTICS ",JSON.stringify(map.statistics))
 var counts={}
 for node in map._geometry.find_children("OriginalMatchedR17_*","MeshInstance3D",true,false):
  var kind=str(node.name).trim_prefix("OriginalMatchedR17_")
  counts[kind]=int(counts.get(kind,0))+1
 print("R19_ALPHA_ACTUAL_DETAIL_MESHES ",JSON.stringify(counts))
 print("R19_ALPHA_ACTUAL_SPAWN ",map.spawn," warmup=",map.warmup_spawn_r8)
 print("R19_ALPHA_ACTUAL_SCORE_BOARDS ",map.find_children("AuthoredWorldScoreR9","Node3D",true,false).size())
 if map.statistics.has("art_r19"):
  var stats:Dictionary=map.statistics.art_r19
  assert(stats.guard_skips==0 and stats.sign==16 and stats.deck==6 and stats.rail==10 and stats.steel==10 and stats.wheel==3)
  assert(map._geometry.find_children("OriginalAssemblyR19_*","MeshInstance3D",true,false).size()==stats.draw_chunks)
  print("R19_ALPHA_PRODUCTION_COHORT_PASS ",JSON.stringify(stats))
 if OS.get_cmdline_user_args().has("--capture-r19"):
  game.set_process(false)
  game.player.set_process(false)
  game.player.set_physics_process(false)
  for node in [game.combat_hud,game.ui_layer,game.vr_label,game.vr_cue,game.weapon,game.body_visual]:node.hide()
  var camera=Camera3D.new();root.add_child(camera);camera.current=true;camera.far=5000
  var views=[
   ["cab-a",Vector3(-359.4039,36.3051,280.6427),Vector3(-10,5,10)],
   ["cab-b",Vector3(-368.38675,36.3048,303.1438),Vector3(-10,5,10)],
   ["van",Vector3(57.50595,25.4323,60.957),Vector3(9,4,9)]]
  # Derive front-facing capture basis from the actual loaded replacement plate.
  var sign_center=Vector3(49.3792,43.62725,154.69998);var sign_normal=Vector3.ZERO
  for node in map._geometry.find_children("OriginalAssemblyR19_sign*","MeshInstance3D",true,false):
   var a=node.mesh.surface_get_arrays(0);var vertices=a[Mesh.ARRAY_VERTEX];var indices=a[Mesh.ARRAY_INDEX];var normals=a[Mesh.ARRAY_NORMAL]
   for tri in node.mesh.get_faces().size()/3:
    var at:int=indices[tri*3] if indices!=null and not indices.is_empty() else tri*3
    var p:Vector3=vertices[at];var n:Vector3=normals[at]
    var middle=Vector3.ZERO
    for k in 3:middle+=vertices[indices[tri*3+k] if indices!=null and not indices.is_empty() else tri*3+k]/3.0
    if absf((sign_center-p).dot(n))<.005 and middle.distance_to(sign_center)<8:
     sign_normal=-n.normalized();break # Source sign normals point into the back-to-back frame.
   if sign_normal.length_squared()>.5:break
  assert(sign_normal.length_squared()>.5,"Live sign seed front basis must be found")
  var sign_right=Vector3.UP.cross(sign_normal).normalized()
  print("R19_SIGN_CAPTURE_BASIS center=",sign_center," normal=",sign_normal," right=",sign_right)
  views.append(["sign-head-on",sign_center,sign_normal*20])
  views.append(["sign-oblique",sign_center,sign_normal*20+sign_right*8+Vector3.UP*2])
  var lobby:Vector3=map.warmup_spawn_r8-map.global_position
  var lobby_label=3
  for bearing in [Vector3(1,0,0),Vector3(-1,0,0),Vector3(0,0,1),Vector3(0,0,-1)]:
   views.append(["lobby-"+str(lobby_label),lobby+bearing*18+Vector3.UP*4,-bearing*18+Vector3.UP*-2]);lobby_label+=1
  for view in views:
   var target:Vector3=map.global_position+view[1]
   camera.global_position=target+view[2];camera.look_at(target)
   for i in 10:await process_frame
   await RenderingServer.frame_post_draw
   var path=OS.get_cmdline_user_args()[0]+"/R19-AFTER-UV2-"+view[0]+".png"
   assert(root.get_texture().get_image().save_png(path)==OK)
   print("R19_CAPTURE ",path," camera=",camera.global_position," target=",target)
  for node in map._geometry.find_children("*","MeshInstance3D",true,false):
   if node.mesh==null or node.mesh.get_surface_count()==0:continue
   var center:Vector3=node.mesh.get_aabb().get_center()
   if str(node.name).begins_with("OriginalMatchedR17_") or center.distance_to(Vector3(-359.4039,36.3051,280.6427))<25:
    var h=HashingContext.new();h.start(HashingContext.HASH_SHA256);h.update(node.mesh.get_faces().to_byte_array())
    print("R19_LIVE_MESH ",map._geometry.get_path_to(node)," sha256=",h.finish().hex_encode()," center=",center," triangles=",node.mesh.get_faces().size()/3)
 game._quitting=true;game.queue_free()
 for i in 8:await process_frame
 quit()
