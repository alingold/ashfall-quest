extends SceneTree
func _initialize():call_deferred("run")
func run():
 var game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:melipop-gods-poplightning";game._start_from_title()
 while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
 var map=game.active_sandbox_map
 var board=map.find_child("AuthoredWorldScoreR9",true,false)
 assert(board!=null,"Source scoreboard must activate")
 assert(board.faces.size()==4)
 for i in board.faces.size():
  var face=board.faces[i]
  assert(face.mesh==map._details_r8.score_faces_r9[i],"Original four face resource/position/UV identities must be preserved")
  assert(not face.material_override.no_depth_test and face.material_override.render_priority==100)
  assert(face.material_override.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA)
  assert(face.material_override.depth_draw_mode==BaseMaterial3D.DEPTH_DRAW_ALWAYS)
  assert(face.is_visible_in_tree())
  assert(face.material_override.albedo_texture==board.viewport.get_texture())
  assert(face.global_transform==map.global_transform)
  var arrays=face.mesh.surface_get_arrays(0)
  assert(arrays[Mesh.ARRAY_TEX_UV].size()==6)
  for uv in arrays[Mesh.ARRAY_TEX_UV]:assert(uv.x>=0 and uv.x<=1 and uv.y>=0 and uv.y<=1)
 print("R18_REAL_MAP_SCORE_ACTIVATION_PASS faces=4 map_origin=",map.global_position," active=",game.network_bridge.net.active)
 game._quitting=true;quit()
