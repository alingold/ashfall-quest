extends Node3D
## Spatial danger audio. R6 renders the boundary on actual world surfaces.
## White next-zone terrain ribbon remains a separate 0.3048m marking.
const Zone=preload("res://scripts/square_zone_r3.gd")
const HEIGHT=300.0
var game:Node
var net:Node
var walls:Array[MeshInstance3D]=[]
var rails:Array[MeshInstance3D]=[]
var labels:Array[Label3D]=[]
var material:ShaderMaterial
var source:AudioStreamPlayer3D
var outside_audio:AudioStreamPlayer
var edge_distance=INF
var outside=false
func setup(g:Node,n:Node)->void:
	game=g;net=n;name="CurrentRedDangerZoneR5"
	material=ShaderMaterial.new();material.shader=preload("res://assets/cyber_bird/zone_r5/danger.gdshader")
	for i in 4:
		var wall:=MeshInstance3D.new();wall.mesh=QuadMesh.new();wall.material_override=material;wall.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;add_child(wall);walls.append(wall)
	var red:=StandardMaterial3D.new();red.albedo_color=Color("ff1736");red.emission_enabled=true;red.emission=Color("ff1736");red.emission_energy_multiplier=3;red.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED
	var stream:AudioStreamWAV=load("res://assets/cyber_bird/zone_r5/danger_zone_loop.wav");stream.loop_mode=AudioStreamWAV.LOOP_FORWARD;stream.loop_begin=0;stream.loop_end=stream.data.size()/2
	source=AudioStreamPlayer3D.new();source.stream=stream;source.bus="SFX";source.max_distance=100;source.unit_size=25;source.attenuation_model=AudioStreamPlayer3D.ATTENUATION_DISABLED;source.doppler_tracking=AudioStreamPlayer3D.DOPPLER_TRACKING_DISABLED;add_child(source)
	outside_audio=AudioStreamPlayer.new();outside_audio.stream=stream;outside_audio.bus="SFX";outside_audio.volume_db=-15;add_child(outside_audio)
func nearest_edge(rect:Rect2,point:Vector2)->Vector2:
	var clamped:Vector2=point.clamp(rect.position,rect.end)
	var candidates:Array[Vector2]=[Vector2(rect.position.x,clamped.y),Vector2(rect.end.x,clamped.y),Vector2(clamped.x,rect.position.y),Vector2(clamped.x,rect.end.y)]
	candidates.sort_custom(func(a,b):return a.distance_squared_to(point)<b.distance_squared_to(point));return candidates[0]
func _process(_delta:float)->void:
	visible=net.active and net.stage=="match" and not net.zone.is_empty() and not net.dedicated and not game.network_bridge._preparing
	if not visible:
		source.stop();outside_audio.stop();edge_distance=INF;outside=false;return
	var rect:Rect2=Zone.current(net.zone,net.now());var origin:Vector3=preload("res://scripts/map_catalog1000.gd").info(str(net.rules.map)).origin
	var eye:Vector3=game.player.get_view_camera().global_position;material.set_shader_parameter("eye_world",eye)
	var center:Vector2=rect.get_center();var points:Array[Vector2]=[Vector2(center.x,rect.position.y),Vector2(rect.end.x,center.y),Vector2(center.x,rect.end.y),Vector2(rect.position.x,center.y)]
	var bottom:float=origin.y-300;var ceiling:float=origin.y+1500
	if is_instance_valid(game.active_sandbox_map) and "_meta" in game.active_sandbox_map:
		var m:Dictionary=game.active_sandbox_map._meta;bottom=origin.y+float(m.solid_min[1])-30;ceiling=maxf(origin.y+600,origin.y+float(m.solid_max[1])+400)
	ceiling=maxf(ceiling,game.network_bridge.launch_r12.ceiling()+30)
	for i in 4:
		walls[i].mesh.size=Vector2(rect.size.x if i%2==0 else rect.size.y,ceiling-bottom)
		walls[i].global_position=Vector3(origin.x+points[i].x,(bottom+ceiling)*.5,origin.z+points[i].y);walls[i].rotation.y=PI*.5 if i%2==1 else 0.0
	var local:=Vector2(eye.x-origin.x,eye.z-origin.z);var edge:=nearest_edge(rect,local);edge_distance=edge.distance_to(local);outside=not Zone.contains(rect,eye,origin)
	source.global_position=Vector3(origin.x+edge.x,eye.y,origin.z+edge.y)
	source.volume_db=lerpf(-9,-38,clampf(edge_distance/90,0,1))
	if edge_distance<90:
		if not source.playing:source.play()
	else:source.stop()
	if outside:
		outside_audio.volume_db=-15 if edge_distance<90 else -11
		if not outside_audio.playing:outside_audio.play()
	else:outside_audio.stop()
