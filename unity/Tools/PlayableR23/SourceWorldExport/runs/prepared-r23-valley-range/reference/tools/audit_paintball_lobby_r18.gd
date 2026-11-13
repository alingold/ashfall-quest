extends SceneTree
func _initialize():call_deferred("run")
func run():
 var game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:battlefield-paintball-gods-pitdevil";game._start_from_title()
 while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
 var map=game.active_sandbox_map
 print("PAINTBALL_LOBBY local=",map.warmup_spawn_r8-map.global_position," world=",map.warmup_spawn_r8," origin=",map.global_position)
 var probe_rows=load("res://scripts/paintball_score_frame_probe_r18.gd").rows(map._geometry,map.map_id,str(map._meta.get("cache_fingerprint","")))
 assert(probe_rows.size()==1)
 assert(map.find_child("AuthoredWorldScoreR9",true,false)!=null,"Verified frame must activate through production hook")
 print("R18_ACTUAL_PAINTBALL_FRAME_GUARDS_PASS; production board activates at verified source frame")
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/paintball-lobby-location-r18.json",FileAccess.WRITE)
 file.store_string(JSON.stringify({"local":[map.warmup_spawn_r8.x-map.global_position.x,map.warmup_spawn_r8.y-map.global_position.y,map.warmup_spawn_r8.z-map.global_position.z],"world":str(map.warmup_spawn_r8),"origin":str(map.global_position)},"\t"));file.close()
 game._quitting=true;quit()
