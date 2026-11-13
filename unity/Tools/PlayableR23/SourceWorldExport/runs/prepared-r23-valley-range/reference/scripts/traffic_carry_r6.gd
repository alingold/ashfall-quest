extends Node
## Moving traffic attachment for ASHFALL's custom feet support and world grips.
## Does not change the player's yaw, tracked local poses, controls, or fling gain.
var actor:Node3D
var traffic:Node3D
var platform:Node3D
var grips:Dictionary={}
var _last_actor_position:=Vector3.INF
var _platform_velocity:=Vector3.ZERO
var _grip_velocity:=Vector3.ZERO
var _last_mode:=""

func setup(player:Node3D,life:Node3D)->void:
	actor=player;traffic=life;name="TrafficCarryR6";process_physics_priority=-30
	_last_actor_position=actor.global_position
func _physics_process(delta:float)->void:
	if not is_instance_valid(traffic) or not traffic.is_physics_processing():reset();return
	step(delta)
func reset()->void:
	if is_instance_valid(actor):
		for hand in grips:actor._grabs.erase(hand)
		if actor._grabs.is_empty():actor.climbing=false
	platform=null;grips.clear();_platform_velocity=Vector3.ZERO;_grip_velocity=Vector3.ZERO;_last_mode=""
	if is_instance_valid(actor):_last_actor_position=actor.global_position
func _eligible()->bool:
	return is_instance_valid(actor) and actor.is_inside_tree() and is_instance_valid(traffic) and traffic.is_inside_tree() and actor.input_enabled and not actor.mantling and not bool(traffic.game.get("front_end_active"))
func _valid_body(body:Node3D)->bool:
	return is_instance_valid(body) and body.is_inside_tree() and body.is_in_group("traffic_carry_r6") and body.get_parent()==traffic
func _pose_delta(body:Node3D)->Transform3D:
	return body.global_transform*traffic.previous_pose(body).affine_inverse()
func _clock_step_safe(body:Node3D)->bool:
	var before:Transform3D=traffic.previous_pose(body)
	return before.origin.distance_to(body.global_position)<1.5 and absf(before.basis.get_rotation_quaternion().dot(body.global_basis.get_rotation_quaternion()))>.985
func _grip_body(hand:Node3D,anchor:Vector3)->Node3D:
	var hit:Dictionary=actor.find_grab_surface(anchor)
	var body:Node3D=traffic.body_from_hit(hit)
	if _valid_body(body):return body
	# Preserve the accepted thin-wall proxy acquisition when raw tracking has
	# reached through a surface. Resolve only new grips, not held hand poses.
	var point:Vector3=actor.hand_walls_r2.resolve(actor,hand)
	body=traffic.body_from_hit(actor.find_grab_surface(point))
	return body if _valid_body(body) else null
func _support_body()->Node3D:
	var hit:Dictionary=actor._support_hit()
	if hit.is_empty() or hit.normal.y<.85 or absf(float(hit.position.y)-actor.global_position.y)>.055:return null
	return traffic.body_from_hit(hit)
func _translate(displacement:Vector3,body:Node3D)->Vector3:
	if displacement.length_squared()<.00000001:return Vector3.ZERO
	actor._sync_head_shape()
	var already:bool=actor.get_collision_exceptions().has(body)
	if not already:actor.add_collision_exception_with(body)
	var start:Vector3=actor.global_position
	actor.move_and_collide(displacement)
	if not already:actor.remove_collision_exception_with(body)
	var actual:Vector3=actor.global_position-start
	# This is artificial platform movement, not a physical head reach. Keep
	# the next tracked-head sweep anchored to the carried safe head position.
	if actor._last_head.is_finite():actor._last_head+=actual
	for hand in actor.hand_walls_r2.previous:
		actor.hand_walls_r2.previous[hand]+=actual
	return actual
func _inherit_motion(velocity:Vector3)->void:
	if not velocity.is_finite() or velocity.length_squared()<.0001:return
	actor.velocity+=velocity.limit_length(12.0)
	actor._climb_launch_pending=true
func step(delta:float)->void:
	if delta<=0 or not _eligible():reset();return
	if _last_actor_position.is_finite() and _last_actor_position.distance_to(actor.global_position)>2.0:
		reset();return # Spawn, calibration/recenter, map change, or a clock jump.
	var released:bool=not grips.is_empty()
	for hand in grips.keys():
		if not is_instance_valid(hand) or not actor._grabs.has(hand) or not _valid_body(grips[hand].body):
			if actor._grabs.has(hand):actor._grabs.erase(hand)
			grips.erase(hand)
	released=released and grips.is_empty() and actor._grabs.is_empty()
	for hand in actor._grabs.keys():
		if grips.has(hand):continue
		var body:Node3D=_grip_body(hand,actor._grabs[hand])
		if body==null:continue
		if not _clock_step_safe(body):actor._grabs.erase(hand);continue
		var anchor:Vector3=actor._grabs[hand]
		grips[hand]={"body":body,"local":traffic.previous_pose(body).affine_inverse()*anchor,"written":anchor}
	var carrier:Node3D=null
	var displacement:=Vector3.ZERO
	var targets:Dictionary={}
	var same_body:bool=not grips.is_empty() and grips.size()==actor._grabs.size()
	for hand in grips.keys():
		var record:Dictionary=grips[hand];var body:Node3D=record.body
		if not _clock_step_safe(body):actor._grabs.erase(hand);grips.erase(hand);same_body=false;continue
		# Desktop climbing can intentionally advance its anchor up the wall.
		var changed:Vector3=actor._grabs[hand]-Vector3(record.written)
		record.local+=traffic.previous_pose(body).basis.inverse()*changed
		var target:Vector3=body.global_transform*Vector3(record.local)
		targets[hand]=target;displacement+=target-Vector3(actor._grabs[hand])
		if carrier==null:carrier=body
		elif carrier!=body:same_body=false
	if same_body and _valid_body(carrier) and not targets.is_empty():
		displacement/=float(targets.size())
		var actual:Vector3=_translate(displacement,carrier)
		_grip_velocity=actual/maxf(delta,.001)
		if actual.distance_to(displacement)>.06:
			for hand in grips:actor._grabs.erase(hand)
			grips.clear();targets.clear();carrier=null
		_last_mode="grip";platform=null
	for hand in targets:
		actor._grabs[hand]=targets[hand];grips[hand].written=targets[hand]
		if hand.global_position.distance_to(targets[hand])>1.05:
			actor._grabs.erase(hand);grips.erase(hand)
	if actor._grabs.is_empty():
		var can_ride:bool=not actor.gliding and actor.velocity.y<=.2 and not actor._jump_requested
		var previous_platform:Node3D=platform
		if can_ride and actor.is_supported() and _valid_body(platform) and _clock_step_safe(platform):
			var at:Vector3=_pose_delta(platform)*actor.global_position
			var desired:Vector3=at-actor.global_position
			var actual:Vector3=_translate(desired,platform)
			_platform_velocity=actual/maxf(delta,.001)
			if actual.distance_to(desired)>.06:platform=null
		var supported:Node3D=_support_body() if can_ride else null
		platform=supported if _valid_body(supported) else null
		if platform!=null:
			if previous_platform==null:
				var delta_pose:Transform3D=_pose_delta(platform)
				_platform_velocity=(delta_pose*actor.global_position-actor.global_position)/maxf(delta,.001)
			_last_mode="roof"
		elif previous_platform!=null and _last_mode=="roof":
			_inherit_motion(_platform_velocity);_last_mode=""
		elif released and _last_mode=="grip":
			_inherit_motion(_grip_velocity);_last_mode=""
	_last_actor_position=actor.global_position
func _exit_tree()->void:
	if is_instance_valid(actor):
		for hand in grips:actor._grabs.erase(hand)
		if actor._grabs.is_empty():actor.climbing=false
	grips.clear();platform=null
