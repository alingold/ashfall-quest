extends SceneTree
var game:Node
func _initialize():call_deferred("run")
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game)
 game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
 while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
 var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
 var rows:Array=[]
 for view in map._geometry.find_children("*","MeshInstance3D",true,false):
  var mat:Material=view.material_override
  if mat==null or view.mesh.get_surface_count()==0:continue
  var key=mat.resource_name
  if key not in ["vc","p2a61c272"]:continue
  var groups:Dictionary=preload("res://scripts/map_art_repair_r14.gd")._groups(view.mesh)
  var arrays:Array=view.mesh.surface_get_arrays(0);var colors=arrays[Mesh.ARRAY_COLOR];var idx=arrays[Mesh.ARRAY_INDEX];var first:Dictionary={}
  for i in groups.triangle_roots.size():
   var id=groups.triangle_roots[i]
   if not first.has(id):first[id]=i
  for id in groups.rows:
   var part:Dictionary=groups.rows[id];var box:AABB=part.box
   if part.triangles>3500:continue
   var color=Color.WHITE
   if colors!=null and not colors.is_empty():color=colors[idx[first[id]*3] if idx!=null and not idx.is_empty() else first[id]*3]
   rows.append({"path":str(map._geometry.get_path_to(view)),"key":key,"id":id,"triangles":part.triangles,"center":[box.get_center().x,box.get_center().y,box.get_center().z],"size":[box.size.x,box.size.y,box.size.z],"color":[color.r,color.g,color.b]})
 var pads:Array=[]
 for row in map.semantics_r14.health_pads:
  var floor:Vector3=map._floor(row.center+Vector3.UP*2,5)
  pads.append({"center":str(row.center-map.global_position),"floor":str(floor-map.global_position),"rate":map.semantics_r14.healing_rate_at(floor+Vector3.UP*.12) if floor.is_finite() else -1})
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/INSPECT-R17.json",FileAccess.WRITE);f.store_string(JSON.stringify({"parts":rows,"stats":map.statistics,"pads":pads},"	"));f.close()
 if DisplayServer.get_name()!="headless":
  var meta:Dictionary=map._meta;var camera:Camera3D=game.player.desktop_camera;var focus:Vector3=map.global_position+Vector3(meta.center[0],meta.center[1],meta.center[2]);camera.global_position=focus+Vector3(120,140,180);camera.look_at(focus,Vector3.UP)
  for i in 20:await process_frame
  await RenderingServer.frame_post_draw
  get_root().get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/NATIVE-R17-Alpha-overview.png")
  print("R17_NATIVE triangles=",Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)," calls=",Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)," vram=",Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED))
 game._quitting=true;game.queue_free();await process_frame;quit()
