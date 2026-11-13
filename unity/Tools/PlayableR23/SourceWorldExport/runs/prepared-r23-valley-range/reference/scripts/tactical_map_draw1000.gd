extends Control
var map:Node
const RECT:=Rect2(310,100,650,650)
func _draw()->void:
	draw_style_box(_panel(),Rect2(0,0,1400,850));var font =ThemeDB.fallback_font
	draw_rect(RECT,Color("0c2530"));draw_rect(RECT,Color("337286"),false,2)
	if map.top_image!=null:draw_texture_rect(map.top_image,RECT,false,Color(.7,.8,.85))
	var info:Dictionary=map.Maps.info(map.map_id())
	for i in range(1,10):
		var f =float(i)/10;draw_line(RECT.position+Vector2(RECT.size.x*f,0),RECT.position+Vector2(RECT.size.x*f,RECT.size.y),Color("23434d"));draw_line(RECT.position+Vector2(0,RECT.size.y*f),RECT.position+Vector2(RECT.size.x,RECT.size.y*f),Color("23434d"))
	# Actual world footprints projected north-up; screenshot remains beside this live map.
	var active:Node=map.game.active_sandbox_map
	if is_instance_valid(active) and "buildings" in active:
		for b in active.buildings:
			var center:Vector3=active.global_position+b.center
			var uv:Vector2=map.Maps.uv(map.map_id(),center)
			var scale:Vector2=RECT.size/(info.max-info.min)
			var size_value =Vector2(float(b.width),float(b.depth))*scale
			draw_rect(Rect2(RECT.position+uv*RECT.size-size_value*.5,size_value),Color("3e6373"))
	var session:Node=map.session
	if session.active and not session.zone.is_empty():
		var region:Rect2=preload("res://scripts/square_zone_r3.gd").current(session.zone,session.now())
		var upcoming:Rect2=session.zone.next
		var scale:Vector2=RECT.size/(info.max-info.min)
		draw_rect(Rect2(RECT.position+(region.position-info.min)*scale,region.size*scale),Color("ff273e"),false,4,true)
		draw_rect(Rect2(RECT.position+(upcoming.position-info.min)*scale,upcoming.size*scale),Color.WHITE,false,3,true)
	if session.active:
		for id in session.players:
			var p:Dictionary=session.players[id]
			if p.spectator or (id!=session.me() and not session.poses.has(id)):continue
			if p.life!="alive" and not session.allied(session.me(),id):continue
			_marker(id,p.name,map.game.player.global_position if id==session.me() else session.poses[id].root.origin,p.life)
	else:_marker(0,"YOU",map.game.player.global_position,"alive")
	for row in map.ping_rows_r19():
		var point:Vector2=RECT.position+map.Maps.uv(map.map_id(),row.point).clamp(Vector2.ZERO,Vector2.ONE)*RECT.size
		var owner_point:Vector2=RECT.position+map.Maps.uv(map.map_id(),row.owner_point).clamp(Vector2.ZERO,Vector2.ONE)*RECT.size
		var index:int=clampi(int(row.index),0,8);var color:Color=preload("res://scripts/ping_style_r19.gd").COLORS[index]
		draw_line(owner_point,point,Color(.005,.01,.015,.8),5,true)
		draw_line(owner_point,point,Color(color,.95),2.5,true)
		var texture:Texture2D=preload("res://scripts/weapon_icons_094.gd").icon(int(row.catalog)) if int(row.catalog)>=0 else load("res://assets/ui/ping0993_5.svg" if index==5 else "res://assets/ui/ping072_%d.svg"%mini(index,7))
		var size:float=60.0 if index==4 else 12.0
		draw_texture_rect(texture,Rect2(point-Vector2.ONE*size*.5,Vector2.ONE*size),false)
func _marker(id:int,title:String,position:Vector3,life:String)->void:
	if id==map.session.me() or not map.session.active:position=map.game.player.global_position
	var uv:Vector2=map.Maps.uv(map.map_id(),position);var p =RECT.position+uv.clamp(Vector2.ZERO,Vector2.ONE)*RECT.size
	var color =Color("ffce3c") if id==map.session.me() or not map.session.active else (Color("41deed") if map.session.allied(map.session.me(),id) else Color("ff756e"))
	if life=="alive":draw_circle(p,8,color)
	else:draw_circle(p,8,color,false,2,true)
	if id==map.session.me() or not map.session.active:
		var forward:Vector3=-map.game.player.get_view_camera().global_basis.z;var direction:=Vector2(forward.x,forward.z).normalized();var side:=Vector2(-direction.y,direction.x)
		if direction.length_squared()>.001:draw_colored_polygon(PackedVector2Array([p+direction*17,p-direction*3+side*6,p-direction*3-side*6]),color)
	draw_string(ThemeDB.fallback_font,p+Vector2(12,-8),title,HORIZONTAL_ALIGNMENT_LEFT,160,18,color)
func _panel()->StyleBoxFlat:
	var s =StyleBoxFlat.new();s.bg_color=Color(.02,.055,.075,.96);s.border_color=Color("32647a");s.set_border_width_all(2);s.set_corner_radius_all(14);return s
