extends SceneTree
var failures:=0
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures+=1
	print("PASS " if ok else "FAIL ",label)
func run()->void:
	var actor=load("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);actor.input_enabled=true;actor.set_process(false);actor.set_physics_process(false)
	var rack=load("res://scripts/weapon_rack.gd").new();root.add_child(rack);rack.setup(actor);rack.set_process(false)
	for gun in rack.weapons:gun.set_process(false);gun.set_physics_process(false)
	var menu=load("res://scripts/menu.gd").new();root.add_child(menu);menu.setup(actor);menu.set_process(false)
	var fresh:Dictionary=menu.get_settings().weapon_settings
	check(fresh.motion_damping==1.0 and fresh.offhand_influence==0.0,"fresh production menu defaults damping1 influence0")
	for side in ["right","left"]:
		# Let actual rack refresh/apply-selection handle dominance changes.
		actor.dominant_hand=side
		for damping in [0.0,1.0,2.0]:
			menu.apply_settings({"dominant_hand":side,"weapon_settings":{"motion_damping":damping,"offhand_influence":0.0,"grip_toggle":false}})
			rack.set_weapon_settings(menu.get_settings().weapon_settings)
			for index in [0,5]:
				rack.selected=index;rack._apply_selection()
				var gun:Node3D=rack.weapons[rack.selected]
				check(gun.motion_damping==damping and gun.offhand_influence==0.0 and gun.right_hand==actor.get_weapon_hand() and gun.left_hand==actor.get_support_hand(),"actual selected rack "+str(gun.get_meta("reference_id"))+" side="+side+" damping="+str(damping))
			for gun in rack.weapons:gun.set_process(false);gun.set_physics_process(false)
	rack.shutdown();rack.queue_free();menu.queue_free();actor.queue_free();await process_frame;await process_frame
	print("R19_SETTINGS failures=",failures," xr_runtime=false headset=false");quit(failures)
