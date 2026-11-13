extends Node3D
## Shared opaque surface pass: marks every actual floor, wall and roof at the square boundary.
const WIDTH:=.3048
const Zone=preload("res://scripts/square_zone_r3.gd")
var game:Node
var net:Node
var material:ShaderMaterial
var _surfaces:Array[WeakRef]=[]
var _map:WeakRef
var _clock=0.0
var _bounds:Array[Rect2]=[]
var _cull_clock=1.0
func setup(g:Node,n:Node)->void:
	game=g;net=n;name="ZoneSurfaceMarkingR6"
	material=ShaderMaterial.new();material.shader=preload("res://assets/cyber_bird/zone_r6/surface.gdshader")
func _collect(node:Node)->void:
	if node is GeometryInstance3D and node.layers!=0 and not node is Label3D and not node is Sprite3D:
		node.material_overlay=material;_surfaces.append(weakref(node))
		var box:AABB=node.global_transform*node.get_aabb()
		_bounds.append(Rect2(Vector2(box.position.x,box.position.z),Vector2(box.size.x,box.size.z)))
	for child in node.get_children():_collect(child)
func _clear()->void:
	for ref in _surfaces:
		var node=ref.get_ref()
		if is_instance_valid(node) and node.material_overlay==material:node.material_overlay=null
	_surfaces.clear();_bounds.clear();_map=null;_cull_clock=1.0
	for node in get_tree().get_nodes_in_group("field_construction"):node.remove_meta("zone_surface_r6")
func _process(delta:float)->void:
	var active:bool=net.active and net.stage=="match" and not net.zone.is_empty() and not net.dedicated and not game.network_bridge._preparing
	var map:Node=game.active_sandbox_map if is_instance_valid(game.active_sandbox_map) else game.world
	if not active:
		if not _surfaces.is_empty():_clear()
		return
	if _map==null or _map.get_ref()!=map:
		_clear();_map=weakref(map);_collect(map)
	_clock+=delta
	if _clock>=1:
		_clock=0
		for node in get_tree().get_nodes_in_group("field_construction"):
			if not node.has_meta("zone_surface_r6"):_collect(node);node.set_meta("zone_surface_r6",true)
	var origin:Vector3=preload("res://scripts/map_catalog1000.gd").info(str(net.rules.map)).origin
	var current:Rect2=Zone.current(net.zone,net.now());var next:Rect2=net.zone.next
	material.set_shader_parameter("zone_origin",Vector2(origin.x,origin.z))
	material.set_shader_parameter("current_square",Vector4(current.position.x,current.position.y,current.end.x,current.end.y))
	material.set_shader_parameter("next_square",Vector4(next.position.x,next.position.y,next.end.x,next.end.y))
	_cull_clock+=delta
	if _cull_clock>=.1:
		_cull_clock=0
		current.position+=Vector2(origin.x,origin.z);next.position+=Vector2(origin.x,origin.z)
		for i in _surfaces.size():
			var node=_surfaces[i].get_ref()
			if not is_instance_valid(node):continue
			# A 3m broad phase includes the full band plus movement at maximum speed between updates.
			var moved:AABB=node.global_transform*node.get_aabb()
			var box:=Rect2(Vector2(moved.position.x,moved.position.z),Vector2(moved.size.x,moved.size.z))
			node.material_overlay=material if near_boundary(box,current) or near_boundary(box,next) else null
static func near_boundary(box:Rect2,zone:Rect2)->bool:
	var outer:Rect2=zone.grow(3.0);var inner:Rect2=zone.grow(-3.0)
	return box.intersects(outer,true) and (not inner.has_area() or not inner.encloses(box))
func _exit_tree()->void:_clear()
static func boundary_distance(rect:Rect2,point:Vector2)->float:
	var q:Vector2=(point-rect.get_center()).abs()-rect.size*.5
	return absf(minf(maxf(q.x,q.y),0.0)+q.max(Vector2.ZERO).length())
