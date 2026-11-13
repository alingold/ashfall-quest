extends SceneTree
func _initialize():call_deferred("run")
func run():
 var game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:melipop-gods-poplightning";game._start_from_title()
 while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
 var map=game.active_sandbox_map;var board=map.find_child("AuthoredWorldScoreR9",true,false);var face:MeshInstance3D=board.faces[0]
 var center:Vector3=face.to_global(face.mesh.get_aabb().get_center());var normal:Vector3=face.global_basis*face.mesh.surface_get_arrays(0)[Mesh.ARRAY_NORMAL][0];var camera=center+normal*39
 var hits=[]
 for view in map.find_children("*","MeshInstance3D",true,false):
  if not view.is_visible_in_tree() or view.layers==0 or view.mesh==null:continue
  var material=view.material_override
  if not material is ShaderMaterial or material.shader==null or not material.shader.resource_path.ends_with("force_field_r9.gdshader"):continue
  var vertices:PackedVector3Array=view.mesh.get_faces();var a:Vector3=view.to_local(camera);var b:Vector3=view.to_local(center)
  for i in range(0,vertices.size(),3):
   var hit=Geometry3D.segment_intersects_triangle(a,b,vertices[i],vertices[i+1],vertices[i+2])
   if hit==null:continue
   var point:Vector3=view.to_global(hit)
   hits.append({"path":str(map.get_path_to(view)),"material":material.resource_name,"shader":material.shader.resource_path,"triangle":i/3,"local_hit":[point.x-map.global_position.x,point.y-map.global_position.y,point.z-map.global_position.z],"distance_from_camera":camera.distance_to(point),"distance_to_board":center.distance_to(point)})
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/score-field-overlap-r18.json",FileAccess.WRITE);file.store_string(JSON.stringify({"map":map.map_id,"camera":str(camera),"board_center":str(center),"hits":hits,"field_shader_sha256":FileAccess.get_sha256("res://assets/materials/force_field_r9.gdshader")},"\t"));file.close()
 print("R18_SCORE_FIELD_OVERLAP ",JSON.stringify(hits))
 game._quitting=true;quit()
