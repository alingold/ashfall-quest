extends RefCounted
## A free-hand interaction proxy; never writes tracked controllers or gun poses.
const RADIUS := .065
var previous:Dictionary={}
func active(actor:Node)->bool:
	return actor.is_inside_tree() and actor.get_world_3d()!=null
func resolve(actor:Node3D,hand:Node3D)->Vector3:
	var desired:Vector3=hand.global_position
	if not active(actor):previous.erase(hand);return desired
	var space:=actor.get_world_3d().direct_space_state
	var head:Vector3=actor.get_view_camera().global_position
	var ray:=PhysicsRayQueryParameters3D.create(head,desired,1,[actor.get_rid()])
	ray.hit_back_faces=true
	var hit:=space.intersect_ray(ray)
	if not hit.is_empty():
		var normal:Vector3=hit.normal
		if normal.dot(head-hit.position)<0:normal=-normal
		desired=hit.position+normal*(RADIUS+.004)
	var start:Vector3=previous.get(hand,head)
	if start.distance_to(head)>maxf(1.2,head.distance_to(hand.global_position)+.15) or start.distance_to(hand.global_position)>.75:start=head
	# A previous contact may be on the far side after walking around an edge.
	# Rebase the proxy on the current head side instead of trapping it across a wall.
	var previous_ray:=PhysicsRayQueryParameters3D.create(head,start,1,[actor.get_rid()]);previous_ray.hit_back_faces=true
	if not space.intersect_ray(previous_ray).is_empty():start=head
	var shape:=SphereShape3D.new();shape.radius=RADIUS
	var query:=PhysicsShapeQueryParameters3D.new();query.shape=shape;query.collision_mask=1;query.exclude=[actor.get_rid()]
	query.transform.origin=start;query.motion=desired-start
	var fractions:=space.cast_motion(query)
	if fractions[0]<1.0:desired=start+query.motion*maxf(0.0,fractions[0]-.002)
	previous[hand]=desired
	return desired
func reset(hand:Node)->void:previous.erase(hand)
