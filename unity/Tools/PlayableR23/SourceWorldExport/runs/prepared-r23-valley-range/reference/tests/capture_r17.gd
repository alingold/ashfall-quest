extends SceneTree
var game:Node
func _initialize():call_deferred("run")
func shot(camera:Camera3D,position:Vector3,target:Vector3,name:String):
 camera.global_position=position;camera.look_at(target,Vector3.UP)
 for i in 8:await process_frame
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/NATIVE-R17-"+name+".png")
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
 while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
 var map:Node=game.active_sandbox_map;game.player.set_physics_process(false);var camera:Camera3D=game.player.desktop_camera;var origin:Vector3=map.global_position
 var van:=origin+Vector3(57.506,25.4323,60.957);await shot(camera,van+Vector3(7,3,7),van,"street-van")
 var truck:=origin+Vector3(-368.38675,36.3048,303.1438);await shot(camera,truck+Vector3(10,4,9),truck+Vector3(0,0,-3),"street-truck")
 var fence:=origin+Vector3(-109.7578,24.5091,-67.7024);await shot(camera,fence+Vector3(4,1.4,-6),fence,"wood-fence")
 var junction:=origin+Vector3(-225.573,20.515,-56.076);await shot(camera,junction+Vector3(18,14,19),junction,"road-junction")
 var found=false
 for body in map.find_children("*","StaticBody3D",true,false):
  if not body.has_meta("archive_breakable_r8") or not body.glass_r10:continue
  var face:PackedVector3Array=body.view.mesh.get_faces()
  if face.size()<3:continue
  var normal:Vector3=(face[1]-face[0]).cross(face[2]-face[0]).normalized();normal.y=0
  if normal.length()<.5:continue
  normal=normal.normalized();var center:Vector3=body.to_global(body.box.get_center())
  for sign in [-1,1]:
   var candidate:Vector3=center+normal*float(sign)*2.5;var floor:Vector3=map._floor(candidate+Vector3.UP*1.5,5)
   if not floor.is_finite():continue
   var eye:Vector3=floor+Vector3.UP*1.65
   var overhead:=PhysicsRayQueryParameters3D.create(eye,eye+Vector3.UP*5,1);var hit:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(overhead)
   if hit.is_empty():continue
   var ahead:=PhysicsRayQueryParameters3D.create(eye,eye+(center-eye).normalized()*1.8,1)
   if not map.get_world_3d().direct_space_state.intersect_ray(ahead).is_empty():continue
   await shot(camera,eye,Vector3(center.x,eye.y-.1,center.z),"interior");found=true;break
  if found:break
 print("NATIVE_R17_CAPTURES interior=",found," art=",map.statistics.art_r17," wood_panels=",map.statistics.wood_panels_r17)
 game._quitting=true;game.queue_free();await process_frame;quit(0 if found else 1)
