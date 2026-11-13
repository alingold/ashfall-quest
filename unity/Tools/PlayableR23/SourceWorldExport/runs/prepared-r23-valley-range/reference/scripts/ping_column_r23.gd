extends MultiMeshInstance3D
## R23: dotted sky column whose dots match the ping icon's on-screen size at every distance.
## Render-only. Dots are spaced and sized from the anchor's camera depth each frame.
const MAX_DOTS:=512
var actor:Node
var icon_index:=0
var icon_center_y:=.5
var top_y:=80.0
func configure(player:Node,index:int,mark_y:float,sky_limit_y:float,color:Color)->void:
	actor=player;icon_index=index;top_y=maxf(0,sky_limit_y-mark_y)
	var dot:=SphereMesh.new();dot.radius=.5;dot.height=.49;dot.radial_segments=8;dot.rings=4
	var mm:=MultiMesh.new();mm.transform_format=MultiMesh.TRANSFORM_3D;mm.mesh=dot;mm.instance_count=MAX_DOTS;mm.visible_instance_count=0
	multimesh=mm
	var material:=StandardMaterial3D.new();material.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;material.albedo_color=color;material_override=material
	cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	process_priority=161;_process(0)
func _process(_delta:float)->void:
	if not is_instance_valid(actor) or multimesh==null:return
	if bool(actor.get_meta("tactical_map1000",false)):hide();return
	var camera:Camera3D=actor.get_view_camera()
	var anchor:Vector3=global_position+Vector3.UP*icon_center_y
	var depth:float=-camera.to_local(anchor).z
	if depth<=.001:hide();return
	show()
	# Same width as the ping icon texture at this depth (the original 1:1 icon/dot ratio).
	var width:float=preload("res://scripts/ping_sprite_r19.gd").angular_width(icon_index)*depth
	var spacing:float=width*1.37
	var start:float=icon_center_y+width*1.25
	var count:int=clampi(int(floor((top_y-start)/spacing))+1,0,MAX_DOTS)
	var dot_basis:=Basis.IDENTITY.scaled(Vector3.ONE*width)
	for i in count:multimesh.set_instance_transform(i,Transform3D(dot_basis,Vector3(0,start+i*spacing,0)))
	multimesh.visible_instance_count=count