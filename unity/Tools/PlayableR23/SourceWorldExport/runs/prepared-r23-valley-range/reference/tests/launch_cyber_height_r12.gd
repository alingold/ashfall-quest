extends SceneTree
func _initialize():call_deferred("run")
func run():
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);var net:Node=game.network_bridge.net;net.host("Height",25491,"r12");net.request("rules",{"map":"cyber_bird","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0})
	while net.stage!="lobby" or not net.world_ready or game.network_bridge._preparing:await process_frame
	var launch:Node=game.network_bridge.launch_r12;launch.ensure_layout();for i in 5:await physics_frame
	var drop:Vector3=launch.stations[0].frame*Vector3(0,-1,-20);var hit:Dictionary=game.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(drop,drop-Vector3.UP*1000,1))
	var passed:bool=launch.top>120 and launch.top<300 and not hit.is_empty() and hit.position.y<launch.top+1
	var result={"passed":passed,"playable_geometry_top":launch.top,"deck_y":launch.stations[0].frame.origin.y,"first_hot_drop_solid_y":hit.get("position",Vector3.INF).y,"canopy_bottom_y":420,"scope":"actual Cyberbird geometry/physics; no XR"};print("CYBER_HEIGHT_R12 ",JSON.stringify(result))
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/CYBER-HEIGHT-R12.json",FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));game.network_bridge.leave_session();game._quitting=true;game.queue_free();await process_frame;quit(0 if passed else 1)
