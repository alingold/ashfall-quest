extends "res://tests/aim_sync_072.gd"
class VRActor extends Actor:
	func get_locomotion_anchor_position()->Vector3:return view.global_position
class Catalog extends "res://scripts/catalog_firearm.gd":
	func _vr_active()->bool:return true
class Precision extends "res://scripts/precision_rifle.gd":
	func _vr_active()->bool:return true
class Double extends "res://scripts/dt11.gd":
	func _vr_active()->bool:return true
class Pistol extends "res://scripts/pistol.gd":
	func _vr_active()->bool:return true
class Revolver extends "res://scripts/revolver.gd":
	func _vr_active()->bool:return true
class SPR extends "res://scripts/spr.gd":
	func _vr_active()->bool:return true
class LMG extends "res://scripts/belt_lmg.gd":
	func _vr_active()->bool:return true
func make_gun(actor:Node3D,id:String):
	var kind={"awp":Precision,"dt11":Double,"px4":Pistol,"magnum":Revolver,"spr":SPR,"m60":LMG}.get(id,Catalog);var gun=kind.new()
	if gun is Catalog:gun.profile_id=id;gun.mode="semi" if id in ["fal","1911"] else ("bolt" if id=="s85" else "auto")
	root.add_child(gun);gun.setup(actor,false);gun.set_process(false);gun.set_physics_process(false);gun._desktop_signals=true
	for profile in preload("res://scripts/arsenal_data.gd").manifest().profiles:
		if profile.id==id:preload("res://scripts/arsenal_data.gd").apply(gun,int(profile.index))
	return gun
func run()->void:
	var actor:=VRActor.new();root.add_child(actor);actor.add_child(actor.right_hand);actor.add_child(actor.left_hand);actor.add_child(actor.view);actor.view.position=Vector3(0,1.65,0)
	for id in ["fal","1911","px4","dt11","magnum","spr","awp","s85","mp5","mp9","cx4","tec9","m60"]:
		for hz in [30,72,90,120]:
			var gun=make_gun(actor,id);var shots:Array=[];capture(gun,shots);gun.motion_damping=1.0
			for frame in 5:
				# Publish an intentionally old frame, then move the physical gun before trigger dispatch.
				gun.publish_displayed_aim(Transform3D(Basis(Vector3.UP,-.8),Vector3(-4,1,3)))
				actor.right_hand.transform=Transform3D(Basis(Vector3.UP,frame*.17),Vector3(frame*.035,1.3,-.3));gun._process(1.0/hz)
				gun.cooldown=0;gun.reload_state="ready";gun.ammo=10
				if id=="dt11":gun.action.ammo=2;gun.action.state="ready";gun.chamber_states.assign(["live","live"])
				if gun.get("chambered")!=null:gun.chambered=true
				if gun.get("spent_chamber")!=null:gun.spent_chamber=false
				var pose:Transform3D=gun.get_shot_transform();verify_shot(gun,shots,pose,id+" current visible muzzle "+str(hz)+" "+str(frame))
			gun.shutdown();await create_timer(.03).timeout;gun.queue_free();await process_frame
	var tracker:=XRControllerTracker.new();tracker.type=XRServer.TRACKER_CONTROLLER;tracker.name="r9_scope_grip_test";XRServer.add_tracker(tracker);actor.left_hand.tracker=tracker.name;actor.left_hand.pose="grip";tracker.set_pose("grip",Transform3D(Basis.IDENTITY,Vector3(-.2,1.3,-.5)),Vector3.ZERO,Vector3.ZERO,XRPose.XR_TRACKING_CONFIDENCE_HIGH);tracker.set_input("grip",1.0)
	await process_frame;await physics_frame
	var awp=make_gun(actor,"awp");awp.global_position=Vector3(0,1.3,-.4);awp.supported=true;awp.cooldown=0;awp.chambered=true;awp.ammo=3;awp.reload_state="ready"
	check(awp.optic.alignment_allowed(),"AWP physical held grip acquires scope")
	awp.fire();check(awp.reload_state=="needs_bolt" and not awp.supported,"real AWP shot enters spent-bolt stage")
	check(awp.optic.alignment_allowed(),"AWP remains scoped after shot while grip stays held")
	tracker.set_input("grip",0.0);await process_frame;check(not awp.optic.alignment_allowed(),"AWP leaves scope on physical grip release")
	await create_timer(1.5).timeout
	XRServer.remove_tracker(tracker);awp.shutdown();await create_timer(.1).timeout;awp.queue_free();actor.queue_free();await process_frame
	var output:String=OS.get_cmdline_user_args()[0];var file=FileAccess.open(output+"/MOVING-MUZZLE-R9.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures==0,"checks":checks,"failures":failures,"scope":"actual weapon emissions at four frame rates after stale aim publication and controller movement; synthetic XR tracker grip, no OpenXR session or headset"},"\t"));file.close();print("MOVING_MUZZLE_R9 checks=",checks," failures=",failures);call_deferred("quit",0 if failures==0 else 1)
