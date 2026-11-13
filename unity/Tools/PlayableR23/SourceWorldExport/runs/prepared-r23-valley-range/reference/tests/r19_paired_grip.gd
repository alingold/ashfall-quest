extends SceneTree
class Actor extends "res://scripts/player.gd":
	func update_tracking()->bool:tracking_available=true;return true
class CX4 extends "res://scripts/catalog_firearm.gd":
	func _vr_active()->bool:return true
class AWP extends "res://scripts/precision_rifle.gd":
	func _vr_active()->bool:return true
class Rack extends Node:
	var weapons:Array=[]
	var selected:=0
	var is_holstered:=false
	var is_selecting:=false
var failures:=0
func _initialize()->void:call_deferred("run")
func replay(kind,side:String,damping:float,held:bool)->Array:
	var actor:=Actor.new();root.add_child(actor);actor.setup(false);actor.xr_enabled=true;actor.input_enabled=true;actor.set_process(false);actor.set_physics_process(false);actor.dominant_hand=side;actor.collision_layer=0;actor.collision_mask=0
	var rack:=Rack.new();root.add_child(rack);actor.sprint_conflict_provider=rack
	var trackers:Dictionary={}
	for hand in [actor.left_hand,actor.right_hand]:
		var t:=XRControllerTracker.new();t.type=XRServer.TRACKER_CONTROLLER;t.name="paired_"+str(hand.get_instance_id());XRServer.add_tracker(t);hand.tracker=t.name;hand.pose="grip";trackers[hand]=t;t.set_input("grip",0.0);t.set_input("trigger",0.0);t.set_input("primary",Vector2.ZERO)
	var head:=XRPositionalTracker.new();head.type=XRServer.TRACKER_HEAD;head.name="head";XRServer.add_tracker(head)
	var gun=kind.new();root.add_child(gun);gun.setup(actor,false);gun.set_process(false);preload("res://scripts/arsenal_data.gd").apply(gun,0 if kind==CX4 else 5);rack.weapons=[gun]
	# Production settings entrypoint, including clamp and hand-refresh behavior.
	gun.set_weapon_settings({"motion_damping":damping,"offhand_influence":0.0,"grip_toggle":false})
	var records:Array=[]
	for frame in 120:
		var dt:float=[1.0/72,1.0/90,.020,.025,.008][frame%5]
		actor.global_transform=Transform3D(Basis.from_euler(Vector3(.05*sin(frame*.03),frame*.01,.03)),Vector3(4800,0,4800))
		var raw:=Transform3D(Basis.from_euler(Vector3(PI/2+.15*sin(frame*.09),.5*sin(frame*.04),.3*sin(frame*.05))),Vector3(.2,1.4,-.3))
		trackers[gun.right_hand].set_pose("grip",raw,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
		head.set_pose("default",Transform3D(Basis.from_euler(Vector3(.1,frame*.01,.2)),Vector3(0,1.65,0)),Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
		var corrected:Transform3D=gun.corrected_grip_transform(gun.right_hand.global_transform)
		var support:Vector3=corrected*(gun.FOREGRIP_POINT*gun.visual_model_scale())+corrected.basis.x*.07
		trackers[gun.left_hand].set_pose("grip",Transform3D(Basis.IDENTITY,gun.left_hand.get_parent().to_local(support)),Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
		trackers[gun.left_hand].set_input("grip",1.0 if held and frame%40>=10 and frame%40<32 else 0.0)
		actor._physics_process(dt);actor._process(dt);gun._process(dt)
		var scoped:bool=gun.optic.active if kind==AWP else false
		records.append({"basis":gun.global_basis,"model":gun.model.global_basis,"supported":gun.supported,"scoped":scoped,"tau":gun.aim_smoothing_time_constant(),"frame":frame})
		# Publish late tracking after all production process callbacks.
		raw.origin.x+=.015;trackers[gun.right_hand].set_pose("grip",raw,Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH)
		await process_frame
	gun.shutdown();gun.queue_free();actor.queue_free();rack.queue_free();XRServer.remove_tracker(head)
	for t in trackers.values():XRServer.remove_tracker(t)
	await process_frame
	return records
func run()->void:
	for kind in [CX4,AWP]:
		for side in ["right","left"]:
			for damping in [0.0,1.0,2.0]:
				var control:Array=await replay(kind,side,damping,false)
				var treatment:Array=await replay(kind,side,damping,true)
				var maximum:=0.0;var engaged:=0;var scoped:=0
				for frame in control.size():
					var a:Basis=control[frame].basis;var b:Basis=treatment[frame].basis
					var delta:float=rad_to_deg(maxf(a.x.angle_to(b.x),maxf(a.y.angle_to(b.y),a.z.angle_to(b.z))))
					maximum=maxf(maximum,delta)
					if treatment[frame].supported:engaged+=1
					if treatment[frame].scoped:scoped+=1
					if delta>.05 and frame%20==15:print("PAIR_DELTA frame=",frame," angle=",delta," tau_control=",control[frame].tau," tau_held=",treatment[frame].tau," scoped=",treatment[frame].scoped)
				if engaged==0:failures+=1
				# Preserve any actual-engine difference as a failing receipt; no exemptions.
				if maximum>.05:failures+=1
				print("PAIRED kind=",("cx4" if kind==CX4 else "awp")," side=",side," damping=",damping," max_support_delta_deg=",maximum," engaged_frames=",engaged," scoped_frames=",scoped)
	print("R19_PAIRED failures=",failures," xr_runtime=false headset=false");quit(failures)
