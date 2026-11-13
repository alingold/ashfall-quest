extends RefCounted
## Explicitly opt-in, numeric orientation diagnostics only. No file/network I/O.
## Root owns integration. Never writes tracking, transforms, inputs or settings.
var _rack:Node
var _started_us:=0
var _last_us:=-1
var _duration_us:=20000000
var _interval_us:=200000
var _limit:=128
var _count:=0
var _last_supported:=-1
var _last_grip:=-1
func setup(selected_rack:Node,duration_s:float=20.0,max_samples:int=128,rate_hz:float=5.0)->void:
	_rack=selected_rack;_started_us=Time.get_ticks_usec();_last_us=-1;_count=0;_last_supported=-1;_last_grip=-1
	_duration_us=int(clampf(duration_s,1.0,30.0)*1000000)
	_limit=clampi(max_samples,1,256);_interval_us=int(1000000/clampf(rate_hz,1.0,15.0))
func is_complete()->bool:return _count>=_limit or Time.get_ticks_usec()-_started_us>_duration_us
static func angle_degrees(a:Basis,b:Basis)->float:
	return rad_to_deg(maxf(a.x.angle_to(b.x),maxf(a.y.angle_to(b.y),a.z.angle_to(b.z))))
func snapshot()->Dictionary:
	if not is_instance_valid(_rack) or is_complete():return {}
	var weapons:Array=_rack.get("weapons");var selected:int=int(_rack.get("selected"))
	if selected<0 or selected>=weapons.size():return {}
	var gun:Node3D=weapons[selected]
	if not is_instance_valid(gun) or not is_instance_valid(gun.right_hand) or not is_instance_valid(gun.left_hand):return {}
	var now:=Time.get_ticks_usec()
	var supported:=int(gun.supported)
	var grip:=int(gun.left_hand.get_float("grip")>.55 or gun.left_hand.is_button_pressed("grip_click"))
	var transition:=supported!=_last_supported or grip!=_last_grip
	var interval:int=mini(_interval_us,66667) if transition else _interval_us
	if _last_us>=0 and now-_last_us<interval:return {}
	var target:Basis=gun.corrected_grip_transform(gun.right_hand.global_transform).basis
	var stick:Vector2=gun.right_hand.get_vector2("primary");var support_stick:Vector2=gun.left_hand.get_vector2("primary")
	var result:={"elapsed_ms":(now-_started_us)/1000.0,"process_frame":Engine.get_process_frames(),"physics_frame":Engine.get_physics_frames(),"selected":selected,"dominance_left":int(gun.right_hand==gun.player.get("left_hand")),"support_transition":int(supported!=_last_supported),"grip_transition":int(grip!=_last_grip),"supported":supported,"support_grip":grip,"offhand_influence":gun.offhand_influence,"motion_damping":gun.motion_damping,"grip_toggle":int(gun.grip_toggle),"support_weight":gun._support_weight,"filter_ready":int(gun._aim_filter_ready),"tau_s":gun.aim_smoothing_time_constant(),"controller_gun_error_deg":angle_degrees(target,gun.global_basis),"controller_filter_error_deg":angle_degrees(target,gun._filtered_aim),"filter_gun_error_deg":angle_degrees(gun._filtered_aim,gun.global_basis),"model_gun_error_deg":angle_degrees(gun.model.global_basis,gun.global_basis),"dominant_stick_x":stick.x,"dominant_stick_y":stick.y,"support_stick_x":support_stick.x,"support_stick_y":support_stick.y}
	for side in ["dominant","support"]:
		var hand:XRController3D=gun.right_hand if side=="dominant" else gun.left_hand
		var tracker:=XRServer.get_tracker(hand.tracker) as XRPositionalTracker
		if tracker!=null:
			var pose:=tracker.get_pose(hand.pose)
			if pose!=null:result[side+"_tracker_node_error_deg"]=angle_degrees(pose.get_adjusted_transform().basis,hand.basis);result[side+"_confidence"]=int(pose.tracking_confidence)
		var proxy:=hand.get_node_or_null("ArticulatedGlove") as Node3D
		if proxy!=null:result[side+"_proxy_model_error_deg"]=angle_degrees(proxy.global_basis,gun.model.global_basis)
	var optic:Node3D=gun.get("optic") if "optic" in gun else null
	if is_instance_valid(optic):
		result["optic_active"]=int(optic.active);result["optic_image_age_s"]=optic._elapsed
		result["optic_displayed_gun_error_deg"]=angle_degrees(optic._displayed_pose.basis,gun.global_basis)
		if optic.get("_presentation_basis_r16")!=null:result["optic_presentation_head_error_deg"]=angle_degrees(optic._presentation_basis_r16,gun.camera.global_basis)
	_count+=1;_last_us=now;_last_supported=supported;_last_grip=grip
	return result
