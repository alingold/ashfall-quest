extends Node3D
## Shared15s station recharge. The host owns multiplayer health/shield and timers.
const COOLDOWN=15.0
var semantic:Node
var game:Node
var ready_at:Dictionary={}
var rings:MultiMesh
var labels:Array[Label3D]=[]
var clock=0.0
var last_heal=-INF
func setup(source:Node):
 semantic=source;game=source.map._game;name="HealingStationsR18"
 rings=MultiMesh.new();rings.transform_format=MultiMesh.TRANSFORM_3D;rings.use_custom_data=true
 var quad=QuadMesh.new();quad.size=Vector2(1.35,1.35);rings.mesh=quad;rings.instance_count=semantic.health_pads.size()
 var view=MultiMeshInstance3D.new();view.multimesh=rings;view.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;var material=ShaderMaterial.new();material.shader=preload("res://assets/materials/station_ring_r18.gdshader");view.material_override=material;add_child(view)
 for i in semantic.health_pads.size():
  var row:Dictionary=semantic.health_pads[i];var at:Vector3=row.center-global_position+Vector3.UP*.86
  rings.set_instance_transform(i,Transform3D(Basis.IDENTITY,at));rings.set_instance_custom_data(i,Color(0,0,0,0))
  var label=Label3D.new();label.position=at+Vector3.DOWN*.64;label.billboard=BaseMaterial3D.BILLBOARD_ENABLED;label.font_size=36;label.pixel_size=.003;label.outline_size=4;label.modulate=Color("ffd980");label.hide();add_child(label);labels.append(label)
func station_at(point:Vector3)->Dictionary:
 for row in semantic.health_pads:
  if semantic.contains_face(row,point,.75,-.20) and semantic._clear_contact(row,point):return {"key":row.station_key_r18,"cooldown":COOLDOWN}
 return {}
func _physics_process(delta:float):
 clock+=delta
 if clock<.1 or not semantic.map.map_ready:return
 clock=0
 var net:Node=game.network_bridge.net;var stamp:float=net.now() if net.active else Time.get_ticks_msec()/1000.0
 if not net.active and game.player.input_enabled and not game.dead and (game.health<100 or game.shield<100):
  var contact:Dictionary=station_at(game.player.get_feet_position_r16())
  if not contact.is_empty() and stamp>=float(ready_at.get(contact.key,-INF)):
   game.health=100.0;game.shield=100.0;ready_at[contact.key]=stamp+COOLDOWN;last_heal=stamp
   var cue=AudioStreamPlayer3D.new();add_child(cue);cue.stream=load("res://assets/audio/sfx/r18/station_restore.wav");cue.bus="SFX";cue.max_distance=25;cue.unit_size=3;cue.global_position=game.player.get_feet_position_r16();cue.finished.connect(cue.queue_free);cue.play()
 var times:Dictionary=net.station_ready_r18 if net.active else ready_at
 for i in semantic.health_pads.size():
  var row:Dictionary=semantic.health_pads[i];var remaining:float=clampf(float(times.get(row.station_key_r18,0))-stamp,0,COOLDOWN)
  rings.set_instance_custom_data(i,Color(clampf(remaining/COOLDOWN,0,1),0,0,0));labels[i].visible=remaining>0 and labels[i].global_position.distance_squared_to(game.player.get_view_camera().global_position)<3600
  labels[i].text=str(ceili(remaining))+"s" if remaining>0 else ""
