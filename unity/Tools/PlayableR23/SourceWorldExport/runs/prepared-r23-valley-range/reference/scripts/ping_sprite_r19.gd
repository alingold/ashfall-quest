extends Sprite3D
## Rendering-only angular size at the real world mark; no camera-relative relocation.
## R23: ping is 4x larger (4 degrees, was 1). The target mark keeps its existing 5-degree size.
const NORMAL_ANGLE_DEGREES=4.0
const TARGET_ANGLE_DEGREES=5.0
var actor:Node
var anchor_r19=Vector3.ZERO
var multiplier_r19=1.0
func configure_r19(player:Node,index:int):
 actor=player;anchor_r19=global_position;multiplier_r19=1.0
 pixel_size=angular_width(index)/maxi(1,texture.get_width())
 billboard=BaseMaterial3D.BILLBOARD_ENABLED;no_depth_test=true;render_priority=120;cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
 process_priority=160;_process(0)
## World width of the full icon texture per metre of view depth.
static func angular_width(index:int)->float:
 return 2.0*tan(deg_to_rad(TARGET_ANGLE_DEGREES if index==4 else NORMAL_ANGLE_DEGREES)*.5)
func _process(_delta:float):
 if not is_instance_valid(actor):return
 if bool(actor.get_meta("tactical_map1000",false)):hide();return
 var camera:Camera3D=actor.get_view_camera()
 # Billboard geometry stays at its real mark. Only its size changes with view depth.
 # Using camera depth (rather than a near-camera shell) preserves parallax/columns.
 var depth:float=-camera.to_local(anchor_r19).z
 if depth<=.001:hide();return
 show();scale=Vector3.ONE*depth