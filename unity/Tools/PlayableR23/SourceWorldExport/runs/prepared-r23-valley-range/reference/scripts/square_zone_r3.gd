extends RefCounted
## Server chooses nested squares. Clients evaluate the same timestamped motion.
const Maps=preload("res://scripts/map_catalog1000.gd")
const RATIO:=.65
const MIN_SIZE:=12.0
var rng:=RandomNumberGenerator.new()
func _init():rng.randomize()
func reset(rules:Dictionary,time:float)->Dictionary:
	if not rules.zone_enabled:return {}
	var info:Dictionary=Maps.info(str(rules.map))
	var span:Vector2=info.max-info.min
	var size:float=minf(span.x,span.y)
	var rect:=Rect2(info.min+(span-Vector2.ONE*size)*.5,Vector2.ONE*size)
	return plan(rect,rules,time,0)
func plan(rect:Rect2,rules:Dictionary,time:float,index:int)->Dictionary:
	var edge:=maxf(MIN_SIZE,rect.size.x*RATIO)
	var offset:=Vector2(rng.randf_range(0,maxf(0,rect.size.x-edge)),rng.randf_range(0,maxf(0,rect.size.y-edge)))
	var next:=Rect2(rect.position+offset,Vector2.ONE*edge)
	# Bound each moving edge's linear speed, independent of its direction.
	var travel:=maxf(maxf(absf(next.position.x-rect.position.x),absf(next.position.y-rect.position.y)),maxf(absf(next.end.x-rect.end.x),absf(next.end.y-rect.end.y)))
	var hold:=time+float(rules.zone_wait)
	return {"from":rect,"next":next,"hold_until":hold,"ends":hold+travel/maxf(.1,float(rules.zone_speed)),"index":index,"final":rect.size.x<=MIN_SIZE+.001}
func advance(state:Dictionary,rules:Dictionary,time:float)->Dictionary:
	if state.is_empty() or state.final or time<float(state.ends):return state
	var result:=state
	for i in 32:
		result=plan(result.next,rules,float(result.ends),int(result.index)+1)
		if result.final or time<float(result.ends):break
	return result
static func current(state:Dictionary,time:float)->Rect2:
	if state.is_empty():return Rect2()
	var a:Rect2=state.from;var b:Rect2=state.next
	var duration:=maxf(.001,float(state.ends)-float(state.hold_until))
	var fraction:=clampf((time-float(state.hold_until))/duration,0,1)
	return Rect2(a.position.lerp(b.position,fraction),a.size.lerp(b.size,fraction))
static func contains(rect:Rect2,point:Vector3,origin:Vector3)->bool:
	var local:=Vector2(point.x-origin.x,point.z-origin.z)
	return local.x>=rect.position.x and local.y>=rect.position.y and local.x<=rect.end.x and local.y<=rect.end.y
