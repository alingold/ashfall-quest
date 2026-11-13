extends "res://tests/traversal1004.gd"
var batch_token=""
var rows:Array=[]
func _initialize()->void:call_deferred("run_batch")
func run_batch()->void:
	# Keep an actual application process visible to the shared scheduler while
	# disabling 3D drawing. This is a physics/memory window, not a render proof.
	root.disable_3d=true
	var args=OS.get_cmdline_user_args();var job:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(args[0]));batch_token=job.token
	for item in job.items:
		failures.clear();facts.clear();capsule=CapsuleShape3D.new()
		await run([str(item.slug),str(item.facts_file)])
		if is_instance_valid(game):
			if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.has_method("leave"):game.active_sandbox_map.leave()
			game._quitting=true;game.queue_free()
		await process_frame;await process_frame
		rows.back().after_teardown={"root_children":root.get_child_count(),"nodes":Performance.get_monitor(Performance.OBJECT_NODE_COUNT),"resources":Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT),"static_memory":OS.get_static_memory_usage()}
	var passed=rows.size()==job.items.size() and rows.all(func(row:Dictionary):return row.passed)
	var result={"passed":passed,"run_token":batch_token,"rows":rows,"scope":"one graphics-mode XR-off Godot application with 3D viewport disabled; fresh actual Main/map per case, same bounded traversal methods, object teardown between cases. Shared exclusive compute window avoids lens RAM contention. Not rendered footage or VR/WAN acceptance."};var file=FileAccess.open(args[1],FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();print("TRAVERSAL_BATCH1004 "+JSON.stringify(result));quit(0 if passed else 1)
func finish(path:String,result:Dictionary)->void:
	result.run_token=batch_token;var file=FileAccess.open(path,FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();rows.append({"map":result.get("map",""),"passed":result.get("passed",false),"facts_file":path});print("BATCH_MAP "+str(result.get("map",""))+" "+str(result.get("passed",false)))
