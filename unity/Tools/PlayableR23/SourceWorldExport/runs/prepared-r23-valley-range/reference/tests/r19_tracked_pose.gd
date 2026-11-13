extends SceneTree
# Only hardware availability is replaced; production polling and poses run unchanged.
class TrackedGun extends "res://scripts/weapon.gd":
	func _vr_active()->bool:return true
var failures:=0
func check(ok:bool,label:String)->void:
	if not ok:failures+=1
	print("PASS " if ok else "FAIL ",label)
func _initialize()->void:call_deferred("run")
func run()->void:
	var actor=load("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);actor.xr_enabled=true;actor.input_enabled=true;actor.set_process(false);actor.set_physics_process(false)
	var trackers:Dictionary={}
	for hand in [actor.left_hand,actor.right_hand]:
		var t:=XRControllerTracker.new();t.type=XRServer.TRACKER_CONTROLLER;t.name="r19_"+str(hand.get_instance_id());XRServer.add_tracker(t);hand.tracker=t.name;hand.pose="grip";trackers[hand]=t
		for key in ["grip","trigger"]:t.set_input(key,0.0)
		t.set_input("primary",Vector2.ZERO)
	var head:=XRPositionalTracker.new();head.type=XRServer.TRACKER_HEAD;head.name="head";XRServer.add_tracker(head)
	for side in ["right","left"]:
		actor.dominant_hand=side
		var gun:=TrackedGun.new();root.add_child(gun);gun.setup(actor,false);gun.set_process(false);gun.motion_damping=0
		for influence in [0.0,0.5,1.0]:
			gun.offhand_influence=influence
			for frame in 180:
				var dt:float=[1.0/72,1.0/90,0.008,0.025][frame%4]
				actor.global_transform=Transform3D(Basis.from_euler(Vector3(.15*sin(frame*.1),frame*.017,.1)),Vector3(4800,0,4800))
				var raw:=Transform3D(Basis.from_euler(Vector3(PI/2+.1,frame*.009,.2*sin(frame*.1))),Vector3(.2,1.4,-.3))
				trackers[gun.right_hand].set_pose("grip",raw,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
				head.set_pose("default",Transform3D(Basis.from_euler(Vector3(.2,frame*.02,.3)),Vector3(0,1.65,0)),Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
				var expected:Transform3D=gun.corrected_grip_transform(gun.right_hand.global_transform)
				var support_world:Vector3=expected*gun.FOREGRIP_POINT+expected.basis.x*.08
				var support_local:Vector3=gun.left_hand.get_parent().to_local(support_world)
				trackers[gun.left_hand].set_pose("grip",Transform3D(Basis.IDENTITY,support_local),Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
				trackers[gun.left_hand].set_input("grip",1.0 if frame%30>5 else 0.0)
				actor.update_hand_input_rearm(dt)
				gun._process(dt)
				var angle:float=rad_to_deg(maxf(gun.global_basis.x.angle_to(expected.basis.x),maxf(gun.global_basis.y.angle_to(expected.basis.y),gun.global_basis.z.angle_to(expected.basis.z))))
				if frame%30==10:print("SNAP ",side," influence=",influence," frame=",frame," supported=",gun.supported," angle=",angle," hand=",gun.right_hand.global_transform," gun=",gun.global_transform," camera=",actor.xr_camera.global_transform)
				if influence==0.0 and angle>.05:failures+=1;print("FAIL zero influence ",frame," angle=",angle)
				# Late tracker delivery after weapon process, before next process.
				raw.origin.x+=.02;trackers[gun.right_hand].set_pose("grip",raw,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
				if frame==0:check(gun.right_hand.transform.origin.distance_to(raw.origin)<.001,"actual controller follows tracker "+side);check(actor.xr_camera.position.distance_to(Vector3(0,1.65,0))<.001,"actual camera follows head tracker")
				await process_frame
		gun.shutdown();gun.queue_free();await process_frame
	actor.queue_free();XRServer.remove_tracker(head)
	for t in trackers.values():XRServer.remove_tracker(t)
	await process_frame
	print("R19_TRACKED failures=",failures," headset_tested=false");quit(failures)

