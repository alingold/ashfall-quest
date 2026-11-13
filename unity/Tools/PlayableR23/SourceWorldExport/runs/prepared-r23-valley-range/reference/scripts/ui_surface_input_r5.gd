extends RefCounted
## Forward tracked pointer drag/release to native controls on a textured viewport.
var down=false
var armed=false
var captured=false
func step(viewport:SubViewport,board:Node3D,pose:Transform3D,pressed:bool,eligible:bool,regions:Array)->void:
	if not eligible:
		_release(viewport);armed=false;return
	if not armed:
		if not pressed:armed=true
		return
	var start:=board.to_local(pose.origin);var direction:=board.global_basis.inverse()*(-pose.basis.z)
	if absf(direction.z)<.00001:
		if not pressed:_release(viewport)
		return
	var distance:float=-start.z/direction.z;var hit:=start+direction*distance
	var size:Vector2=board.mesh.size
	var point:=Vector2(hit.x/size.x+.5,.5-hit.y/size.y)*Vector2(viewport.size)
	var inside:bool=distance>0 and Rect2(Vector2.ZERO,Vector2(viewport.size)).has_point(point)
	var accepted=false
	for rect in regions:accepted=accepted or (inside and rect.has_point(point))
	if accepted or captured:
		var move:=InputEventMouseMotion.new();move.position=point;move.global_position=point;move.button_mask=MOUSE_BUTTON_MASK_LEFT if down else 0;viewport.push_input(move,true)
		if pressed and not down and accepted:
			var press:=InputEventMouseButton.new();press.position=point;press.global_position=point;press.button_index=MOUSE_BUTTON_LEFT;press.pressed=true;viewport.push_input(press,true);captured=true;down=true
	if not pressed:_release(viewport,point)
func _release(viewport:SubViewport,point:Vector2=Vector2(-100,-100))->void:
	if down:
		var release:=InputEventMouseButton.new();release.button_index=MOUSE_BUTTON_LEFT;release.pressed=false;release.position=point;release.global_position=point;viewport.push_input(release,true)
	down=false;captured=false
