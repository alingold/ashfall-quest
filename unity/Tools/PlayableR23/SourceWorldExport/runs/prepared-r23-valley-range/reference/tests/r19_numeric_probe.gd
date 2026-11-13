extends SceneTree
class Rack extends Node:
	var weapons:Array=[]
	var selected:=0
func _initialize()->void:call_deferred("run")
func run()->void:
	var actor=load("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);actor.set_process(false);actor.set_physics_process(false)
	var rack:=Rack.new();root.add_child(rack)
	var gun=load("res://scripts/precision_rifle.gd").new();root.add_child(gun);gun.setup(actor,false);gun.set_process(false);rack.weapons=[gun]
	var trackers:Array=[]
	for hand in [actor.left_hand,actor.right_hand]:
		var tracker:=XRControllerTracker.new();tracker.type=XRServer.TRACKER_CONTROLLER;tracker.name="numeric_"+str(hand.get_instance_id());XRServer.add_tracker(tracker);hand.tracker=tracker.name;hand.pose="grip";tracker.set_pose("grip",Transform3D.IDENTITY,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH);tracker.set_input("grip",0.0);tracker.set_input("primary",Vector2.ZERO);trackers.append(tracker)
	var probe=load("res://scripts/xr_pose_diagnostics_r19.gd").new();probe.setup(rack,20,4,1)
	var first:Dictionary=probe.snapshot();var failures:=0
	if first.is_empty() or not probe.snapshot().is_empty():failures+=1
	for value in first.values():
		if not (value is float or value is int):failures+=1
	for key in first:
		if "position" in key or "origin" in key or "basis" in key:failures+=1
	gun.supported=true
	probe._last_us-=1000000
	var transition:Dictionary=probe.snapshot()
	if transition.get("support_transition",0)!=1:failures+=1
	probe._last_us-=1000000;probe.snapshot();probe._last_us-=1000000;probe.snapshot()
	if not probe.is_complete() or not probe.snapshot().is_empty():failures+=1
	if not first.has("dominant_tracker_node_error_deg"):failures+=1
	probe.setup(rack,1,100,5);probe._started_us-=2000000
	if not probe.is_complete() or not probe.snapshot().is_empty():failures+=1
	var file:=FileAccess.open(OS.get_cmdline_user_args()[0]+"/r19-numeric-probe-sample.json",FileAccess.WRITE);file.store_string(JSON.stringify(transition));file.close()
	print("R19_NUMERIC_PROBE failures=",failures," max_samples=4 numeric_only=true positions=false bases=false io_in_helper=false")
	gun.shutdown();gun.queue_free();rack.queue_free();actor.queue_free()
	for tracker in trackers:XRServer.remove_tracker(tracker)
	await process_frame;quit(failures)
