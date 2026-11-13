extends SceneTree
const ID="archive:alfa-aglae-souza"
var game:Node
func _initialize():call_deferred("run")
func ready(stage:String="")->bool:
 var deadline=Time.get_ticks_msec()+60000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not game.network_bridge._preparing and (stage.is_empty() or game.network_bridge.net.stage==stage):return true
 return false
func check_board()->Node:
 var map=game.active_sandbox_map;var board=map.find_child("AuthoredWorldScoreR9",true,false)
 assert(board!=null and board.faces.size()==3)
 assert(map.find_children("AuthoredWorldScoreR9","Node3D",true,false).size()==1)
 assert(map._details_r8.get("score_faces_r9",[]).is_empty(),"Local rows must not mutate cached pack details")
 var face=board.faces[0];var center:Vector3=face.mesh.get_aabb().get_center()
 assert(center.distance_to(Vector3(36.24113,27,27.88192))<.01)
 assert(face.global_transform==map.global_transform)
 assert(not face.material_override.no_depth_test and face.material_override.render_priority==100)
 assert(face.layers==map._geometry.get_node("Draw_world_p8ad59c41_0_-1").layers)
 return board
func release_game(mesh_ref:WeakRef,viewport_ref:WeakRef):
 game.network_bridge.net.close(false)
 load("res://scripts/cyber_bird_registration.gd").unload(game)
 for i in 8:await process_frame
 assert(mesh_ref.get_ref()==null and viewport_ref.get_ref()==null,"Generated display mesh/viewport must unload")
 game._quitting=true;game.queue_free()
 for i in 5:await process_frame
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var cached=load("res://local_archive_r10/alfa-aglae-souza.res")
 assert(cached.details.get("score_faces_r9",[]).is_empty())
 game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map=ID;game._start_from_title()
 assert(await ready())
 var board=check_board();var mesh_ref=weakref(board.faces[0].mesh);var viewport_ref=weakref(board.viewport)
 assert(not game.network_bridge.net.active)
 print("R19_ALPHA_READY_DISPLAY_PASS")
 await release_game(mesh_ref,viewport_ref)
 assert(cached.details.get("score_faces_r9",[]).is_empty())
 game=load("res://scripts/main.gd").new();root.add_child(game);var net=game.network_bridge.net
 assert(net.host("Paintball score authority",25926,"score-r18")==OK)
 net.request("rules",{"map":ID,"mode":"respawn","teams":2,"zone_enabled":false,"time_limit":120,"score_limit":25,"protection":0.0,"catalog":true})
 assert(await ready("lobby"));board=check_board();net.request("start");assert(await ready("match"));assert(check_board()==board)
 net._add_player(2,"Paintball victim","");net.players[1].team=0;net.players[2].team=1
 net.players[2].life="alive";net.players[2].loaded=true;net.players[2].protection_until=0
 net.hurt(2,10000,1,false,true,0,"fixture")
 var state=load("res://scripts/world_score_state_r18.gd").snapshot(board.canvas.game.network_bridge.net)
 assert(state.active and state.scores_by_id[0]==1 and state.scores_by_id[1]==0 and state.cap==25)
 assert(state.seconds>=110 and state.seconds<=120)
 print("R19_ALPHA_HOSTED_AUTHORITY_PASS score=",state.scores_by_id," cap=",state.cap," seconds=",state.seconds)
 mesh_ref=weakref(board.faces[0].mesh);viewport_ref=weakref(board.viewport)
 await release_game(mesh_ref,viewport_ref)
 assert(cached.details.get("score_faces_r9",[]).is_empty())
 print("R19_ALPHA_PRODUCTION_ACTIVATION_AND_UNLOAD_PASS")
 quit()
