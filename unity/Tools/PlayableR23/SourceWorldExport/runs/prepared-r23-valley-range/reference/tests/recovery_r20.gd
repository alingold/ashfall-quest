extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var maps:Array=[]
func _initialize():call_deferred("run")
func check(value:bool,label:String):
 checks+=1
 if not value:failures.append(label);print("FAIL ",label)
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game);game.player.set_physics_process(false)
 var cases=["archive:alfa-aglae-souza","archive:10000-days-gods-crazytrain","archive:showdown-gods-crazytrain","cyber_bird","archive:alfa-aglae-souza"]
 var loader=preload("res://scripts/map_loading_r18.gd").get_for(game)
 check(loader.get_child(0).material_override is StandardMaterial3D,"ordinary R18 material path, no clip-space loader shader")
 check(loader.progress==null,"simple loading text has no progress bar")
 for id in cases:
  game.front_menu.selected_map=id;game.front_menu._explore_from_r16("lobby")
  var deadline=Time.get_ticks_msec()+120000
  while Time.get_ticks_msec()<deadline:
   await process_frame
   if is_instance_valid(game.active_sandbox_map) and (not "map_ready" in game.active_sandbox_map or game.active_sandbox_map.map_ready) and not loader.active:break
  var map:Node=game.active_sandbox_map
  check(is_instance_valid(map) and (not "map_ready" in map or map.map_ready) and not loader.active,id+" loads and clears guard")
  if not is_instance_valid(map):break
  check(str(map.get("map_id"))==id if id.begins_with("archive:") else map.get_script().get_script_constant_map().get("MAP_ID","")==id,id+" is the selected map")
  check(not game.player.has_meta("map_loading_r18"),id+" leaves loading controls unlocked")
  var row={"id":id}
  if id.begins_with("archive:"):
   var lods:Dictionary=map.statistics.get("mesh_lods_r16",{})
   check(not lods.has("postcut_r19"),id+" bypasses experimental raw surface LODs")
   check(int(lods.get("meshes",0))>0,id+" retains public R18 distance LODs")
   row["lods"]=lods
  if id=="archive:alfa-aglae-souza":
   check(int(map.statistics.art_r19.get("sign",0))==16 and int(map.statistics.art_r19.get("guard_skips",-1))==0,"Alpha original sign/vehicle art remains guarded and active")
   var board=map.find_child("AuthoredWorldScoreR9",true,false)
   check(board!=null and board.faces.size()==3,"Alpha source-framed scoreboards retained")
  for i in 8:await process_frame
  if DisplayServer.get_name()!="headless":
   await RenderingServer.frame_post_draw
   root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/R20-"+str(maps.size())+"-"+id.replace(":","-")+".png")
  var retired=weakref(map);game._return_home0992()
  for i in 10:await process_frame
  check(retired.get_ref()==null,id+" map unloads on return")
  check(not loader.active and not game.player.has_meta("map_loading_r18"),id+" return clears loader")
  maps.append(row)
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RECOVERY-R20.json",FileAccess.WRITE)
 file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"maps":maps,"scope":"Actual exploration/return flows and native Vulkan when available; XR off. Does not establish Quest driver recovery."},"\t"));file.close()
 game._quitting=true;game.queue_free();for i in 8:await process_frame
 print("RECOVERY_R20 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
