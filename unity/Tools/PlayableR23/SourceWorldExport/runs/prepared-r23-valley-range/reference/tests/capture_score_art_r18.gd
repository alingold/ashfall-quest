extends SceneTree
## Run only in a coordinator-granted native slot. --validate-only-r18 parses without captures.
var game:Node
var camera:Camera3D
var output:String
var facts:Array=[]
var probes:Array=[]
var failed=false
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 if not ok:failed=true;push_warning(label)
func ready(stage:String="")->bool:
 var deadline=Time.get_ticks_msec()+60000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not game.network_bridge._preparing and (stage.is_empty() or game.network_bridge.net.stage==stage):return true
 return false
func enter(id:String)->bool:
 game.network_bridge.net.close(false)
 if game.front_end_active:game.front_menu.selected_map=id;game._start_from_title()
 else:load("res://scripts/cyber_bird_registration.gd").select(game,id)
 return await ready()
func hide_overlays():
 game.player.set_physics_process(false)
 for node in [game.combat_hud,game.ui_layer,game.vr_label,game.vr_cue,game.weapon,game.body_visual]:node.hide()
 camera=game.player.desktop_camera;camera.current=true
func shot(label:String,at:Vector3,target:Vector3):
 hide_overlays();camera.global_position=at;camera.look_at(target,Vector3.UP)
 for i in 8:await process_frame
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png(output+"/R18-"+label+".png")
func bright_pixels(image:Image,region:Rect2i)->int:
 var count=0
 for y in range(region.position.y,region.end.y,2):
  for x in range(region.position.x,region.end.x,2):
   var color=image.get_pixel(x,y)
   if minf(color.r,minf(color.g,color.b))>.45:count+=1
 return count
func board_shot(label:String,hosted:bool):
 var map=game.active_sandbox_map;var board=map.find_child("AuthoredWorldScoreR9",true,false)
 check(board!=null,"Missing actual map scoreboard")
 if board==null:return
 var face:MeshInstance3D=board.faces[0];var arrays=face.mesh.surface_get_arrays(0)
 var normal:Vector3=face.global_basis*arrays[Mesh.ARRAY_NORMAL][0]
 var center:Vector3=face.to_global(face.mesh.get_aabb().get_center())
 board._process(1)
 await shot(label,center+normal.normalized()*39,center)
 var pixels:Image=board.viewport.get_texture().get_image()
 check(pixels!=null and not pixels.is_empty(),"Empty scoreboard viewport readback")
 if pixels==null or pixels.is_empty():return
 pixels.save_png(output+"/R18-"+label+"-canvas.png")
 var minimum_alpha=1.0
 for y in range(0,pixels.get_height(),80):
  for x in range(0,pixels.get_width(),80):minimum_alpha=minf(minimum_alpha,pixels.get_pixel(x,y).a)
 check(minimum_alpha>.999,"Board late-pass texture must remain alpha1")
 var header=bright_pixels(pixels,Rect2i(60,45,900,150))
 var body=bright_pixels(pixels,Rect2i(60,220,900,65) if hosted else Rect2i(60,280,900,150))
 check(header>100,"Scoreboard header lacks bright text pixels")
 check(body>40,"Scoreboard state region lacks bright text pixels")
 var state=load("res://scripts/world_score_state_r18.gd").snapshot(game.network_bridge.net)
 check(state.active==hosted,"Unexpected scoreboard authority phase")
 var hash=HashingContext.new();hash.start(HashingContext.HASH_SHA256);hash.update(pixels.get_data())
 facts.append({"label":label,"minimum_alpha":minimum_alpha,"header_bright_samples":header,"state_bright_samples":body,"pixel_sha256":hash.finish().hex_encode(),"state":state,"camera":str(camera.global_position),"source_normal":str(normal)})
func opaque_depth_probe():
 var map=game.active_sandbox_map;var board=map.find_child("AuthoredWorldScoreR9",true,false);var face:MeshInstance3D=board.faces[0]
 var box:AABB=face.mesh.get_aabb();var normal:Vector3=(face.global_basis*face.mesh.surface_get_arrays(0)[Mesh.ARRAY_NORMAL][0]).normalized()
 var right:Vector3=Vector3.UP.cross(normal).normalized();var up:Vector3=normal.cross(right).normalized();var center:Vector3=face.to_global(box.get_center())
 var width:float=maxf(box.size.x,box.size.z);var height:float=box.size.y
 var sample=camera.unproject_position(center+right*width*.25-up*height*.25);var point=Vector2i(sample.round())
 var before_image=root.get_texture().get_image();var before_color=before_image.get_pixelv(point);var before_luma=(before_color.r+before_color.g+before_color.b)/3.0
 var wall=MeshInstance3D.new();wall.name="TemporaryOpaqueScoreOccluderR18";var mesh=BoxMesh.new();mesh.size=Vector3(width*1.08,height*1.08,.5);wall.mesh=mesh
 var material=StandardMaterial3D.new();material.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;material.albedo_color=Color(.6,.6,.6,1);wall.material_override=material;wall.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
 map.add_child(wall);wall.global_transform=Transform3D(Basis(right,up,normal),center+normal*2)
 await shot("score-opaque-occluder",camera.global_position,center)
 var after_image=root.get_texture().get_image();var after_color=after_image.get_pixelv(point);var after_luma=(after_color.r+after_color.g+after_color.b)/3.0
 check(after_luma>before_luma+.12,"Opaque world probe must hide the late-pass board")
 probes.append({"label":"opaque-world-depth","sample_pixel":str(point),"before_luma":before_luma,"occluder_luma":after_luma,"passed":after_luma>before_luma+.12})
 wall.queue_free();for i in 2:await process_frame
func all_face_views():
 var board=game.active_sandbox_map.find_child("AuthoredWorldScoreR9",true,false)
 for i in range(1,board.faces.size()):
  var face:MeshInstance3D=board.faces[i];var normal:Vector3=face.global_basis*face.mesh.surface_get_arrays(0)[Mesh.ARRAY_NORMAL][0];var center:Vector3=face.to_global(face.mesh.get_aabb().get_center())
  await shot("score-hosted-face-%d"%i,center+normal.normalized()*39,center)
func welcome_frame_probe():
 var map=game.active_sandbox_map;var center:Vector3=map.global_position+Vector3(-3.960614,78.7428,26.23660)
 var existing=map.find_child("AuthoredWorldScoreR9",true,false)
 if existing!=null:existing.hide()
 await shot("paintball-welcome-frame-original",center+Vector3(20,1,0),center)
 # This overview includes the actual Welcome sign and measured hollow frame together.
 await shot("paintball-welcome-frame-context",map.global_position+Vector3(20,80.5,18),map.global_position+Vector3(-3.96,78.7,16))
 var rows=load("res://scripts/paintball_score_frame_probe_r18.gd").rows(map._geometry,map.map_id,str(map._meta.get("cache_fingerprint","")))
 check(rows.size()==1,"Measured Paintball frame fingerprint/hash guard rejected source")
 if rows.is_empty():return
 var board=map.find_child("AuthoredWorldScoreR9",true,false)
 var temporary=false
 if board==null:
  board=load("res://scripts/archive_world_score_r9.gd").new();map.add_child(board);board.setup(game,rows);board.name="PaintballFrameScoreProbeR18";temporary=true
 board.show()
 await shot("paintball-welcome-frame-display-probe",center+Vector3(20,1,0),center)
 probes.append({"label":"paintball-source-frame","center":str(center-map.global_position),"status":"Native-verified exact source frame display; production hook enabled"})
 if temporary:board.queue_free()
 for i in 2:await process_frame
func run():
 if OS.get_cmdline_user_args().has("--validate-only-r18"):print("R18_CAPTURE_FIXTURE_PARSE_PASS; no capture executed");quit();return
 if DisplayServer.get_name()=="headless":push_error("Native fixture requires an explicit native slot; headless only supports --validate-only-r18");quit(1);return
 output=OS.get_cmdline_user_args()[0]
 game=load("res://scripts/main.gd").new();root.add_child(game)
 check(await enter("archive:melipop-gods-poplightning"),"Practice map load timed out")
 if failed:game._quitting=true;quit(1);return
 await board_shot("score-practice",false)
 var net=game.network_bridge.net
 check(net.host("Score capture",25919,"r18-capture")==OK,"Listen host failed")
 net.request("rules",{"map":"archive:melipop-gods-poplightning","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":120,"score_limit":25,"protection":0.0,"catalog":true})
 check(await ready("lobby"),"Hosted warmup load timed out")
 if failed:game._quitting=true;quit(1);return
 net.request("start");check(await ready("match"),"Match load timed out")
 if failed:game._quitting=true;quit(1);return
 # Synthetic victim exercises the real listen-server death/score path.
 net._add_player(2,"Capture victim","");net.players[1].team=0;net.players[2].team=1
 net.players[2].life="alive";net.players[2].loaded=true;net.players[2].protection_until=0
 net.hurt(2,10000,1,false,true,0,"fixture")
 await board_shot("score-hosted",true)
 await opaque_depth_probe()
 if OS.get_cmdline_user_args().has("--four-faces-r18"):await all_face_views()
 check(facts.size()==2 and facts[0].pixel_sha256!=facts[1].pixel_sha256,"Practice and hosted canvas must differ")
 check(int(net.match_scores.get(0,0))==1,"Real authority did not score the fixture death")
 check(await enter("archive:battlefield-paintball-gods-pitdevil"),"Paintball load timed out")
 if failed:game._quitting=true;quit(1);return
 var origin:Vector3=game.active_sandbox_map.global_position
 await welcome_frame_probe()
 # Lower title-area context remains separate from the actual high Welcome/frame area.
 await shot("paintball-title-context",origin+Vector3(-137,46,95),origin+Vector3(-137,45.7,125.42))
 await shot("paintball-lobby-context",game.active_sandbox_map.warmup_spawn_r8+Vector3(0,4,12),game.active_sandbox_map.warmup_spawn_r8+Vector3.UP*2)
 check(await enter("archive:alfa-aglae-souza"),"Alpha load timed out")
 if failed:game._quitting=true;quit(1);return
 var cab:Vector3=game.active_sandbox_map.global_position+Vector3(-359.4039,36.3051,280.6427)
 await shot("cab-after",cab+Vector3(7,4,8),cab)
 var roadblock:Vector3=game.active_sandbox_map.global_position+Vector3(-537.03,49.14236,78.53)
 await shot("roadblock-after",roadblock+Vector3(2,1.5,-6),roadblock)
 if OS.get_cmdline_user_args().has("--prop-context-r18"):
  var cylinder:Vector3=game.active_sandbox_map.global_position+Vector3(50.404,23.20443,237.60599)
  await shot("cylinder-context",cylinder+Vector3(3,1,-7),cylinder)
  var pole:Vector3=game.active_sandbox_map.global_position+Vector3(-14.78533,31.47199,214.93022)
  await shot("pole-context",pole+Vector3(8,4,-12),pole)
 var file=FileAccess.open(output+"/R18-score-art-capture.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":not failed,"facts":facts,"probes":probes,"scope":"Native PC raster/readback only; no physical Quest acceptance"},"\t"));file.close()
 print("R18_NATIVE_SCORE_ART_CAPTURE passed=",not failed," facts=",facts.size())
 net.close(false);game._quitting=true;quit(1 if failed else 0)
