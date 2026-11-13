extends SceneTree
## Same physical source camera/host fixture for baseline and current source. Native requires fresh coordinator grant and --capture-r21.
var game:Node
var camera:Camera3D
var capture=false
var prefix="R21-TRUCK"
func _initialize():call_deferred("run")
func ready(stage:String)->bool:
 var deadline=Time.get_ticks_msec()+70000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not game.network_bridge._preparing and game.network_bridge.net.stage==stage:return true
 return false
func shot(label:String,at:Vector3,target:Vector3):
 camera.global_position=at;camera.look_at(target)
 print("R19_HOSTED_CAMERA ",label," eye=",at," target=",target)
 if not capture:return
 for i in 8:await process_frame
 await RenderingServer.frame_post_draw
 var path=OS.get_cmdline_user_args()[0]+"/"+prefix+"-"+label+".png"
 assert(not FileAccess.file_exists(path),"Preserve all earlier evidence; select a fresh prefix for a rerun")
 assert(root.get_texture().get_image().save_png(path)==OK)
func run():
 capture=OS.get_cmdline_user_args().has("--capture-r21")
 for argument in OS.get_cmdline_user_args():
  if argument.begins_with("--evidence-prefix="):prefix=argument.trim_prefix("--evidence-prefix=")
 assert(prefix in ["R21-BASEFINAL","R21-TRUCK"],"Use explicit immutable evidence cohort")
 preload("res://scripts/archive_catalog1003.gd").catalog()
 game=load("res://scripts/main.gd").new();root.add_child(game);var net=game.network_bridge.net
 assert(net.host("R19 source frame proof",25928,"r19-proof")==OK)
 net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":120,"score_limit":25,"protection":0.0,"catalog":true})
 assert(await ready("lobby"));net.request("start");assert(await ready("match"))
 net._add_player(2,"Frame proof opponent","");net.players[1].team=0;net.players[2].team=1
 net.players[2].life="alive";net.players[2].loaded=true;net.players[2].protection_until=0
 net.hurt(2,10000,1,false,true,0,"fixture")
 var map=game.active_sandbox_map;var board=map.find_child("AuthoredWorldScoreR9",true,false)
 assert(board!=null and board.faces.size()==3)
 var state=preload("res://scripts/world_score_state_r18.gd").snapshot(net)
 assert(state.active and state.scores_by_id[0]==1 and state.cap==25 and state.seconds>90)
 print("R19_HOSTED_CAPTURE_AUTHORITY ",JSON.stringify(state))
 print("R19_HOSTED_CAPTURE_ART ",JSON.stringify(map.statistics.art_r19))
 game.set_process(false);game.player.set_process(false);game.player.set_physics_process(false)
 for node in [game.combat_hud,game.ui_layer,game.vr_label,game.vr_cue,game.weapon,game.body_visual]:node.hide()
 camera=Camera3D.new();root.add_child(camera);camera.current=true;camera.far=5000
 board._process(1)
 var origin:Vector3=map.global_position
 # All 16 supported Alpha landscape faces have parallel +/-Z exterior axes.
 # Use a real oblique view rather than inventing an orthogonal source panel.
 print("R19_ALPHA_ORTHOGONAL_SIGN unavailable in supported 16-plate Alpha family; oblique source view follows")
 var sign_center=origin+Vector3(49.3792,43.62725,154.69998)
 await shot("sign-head-on",sign_center+Vector3(0,0,20),sign_center)
 await shot("sign-oblique",sign_center+Vector3(14,2,20),sign_center)
 await shot("score-context",origin+Vector3(57.63,31,64),origin+Vector3(42.78,27,27.88192))
 var target=origin+Vector3(-359.6906,35.65,261.0)
 var offsets=[Vector3(5,2.5,-7),Vector3(-5,2.5,-7),Vector3(0,4,-6),Vector3(4,4,-2),Vector3(0,2.5,-5),Vector3(0,5,-1),Vector3(-2,2.5,-4),Vector3(2,1.5,-4)]
 for i in offsets.size():
  var eye=target+offsets[i]
  var hit=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(eye,target,1))
  print("TRUCK_LOS ",i," ",hit)
  await shot("exposed-bed-"+str(i),eye,target)
 net.close(false);game._quitting=true;game.queue_free()
 for i in 8:await process_frame
 quit()
