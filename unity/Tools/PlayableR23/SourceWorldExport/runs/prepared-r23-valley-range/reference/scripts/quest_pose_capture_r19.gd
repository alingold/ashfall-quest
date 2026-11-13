extends Node
## Private playtest flag only; bounded numeric capture around first support grip.
var game:Node
var probe:RefCounted
var rows:Array=[]
var captured:Dictionary={}
var previous_body=Basis.IDENTITY
var previous_head=Basis.IDENTITY
var clock=0.0
var enabled=false
func setup(g:Node):
 game=g;process_priority=240
 enabled=g.xr_enabled and OS.get_cmdline_user_args().has("--quest-diagnostics-r19")
 set_process(enabled)
func _process(delta:float):
 if not enabled or not is_instance_valid(game) or game.front_end_active or not game.player.tracking_available:return
 var gun:Node=game.weapon.weapons[game.weapon.selected]
 if not gun.has_method("corrected_grip_transform") or not "left_hand" in gun or not is_instance_valid(gun.left_hand):return
 var catalog:int=game.weapon.catalog_index(game.weapon.selected)
 if probe==null:
  if captured.has(catalog) or captured.size()>=3:return
  if not game.player.is_hand_input_ready(gun.left_hand) or (gun.left_hand.get_float("grip")<=.55 and not gun.left_hand.is_button_pressed("grip_click")):return
  probe=preload("res://scripts/xr_pose_diagnostics_r19.gd").new();probe.setup(game.weapon,20,128,5)
  captured[catalog]=true;previous_body=game.player.global_basis;previous_head=game.player.get_view_camera().global_basis
 var row:Dictionary=probe.snapshot()
 if not row.is_empty():
  row["catalog"]=catalog
  row["body_step_deg"]=probe.angle_degrees(previous_body,game.player.global_basis)
  row["head_step_deg"]=probe.angle_degrees(previous_head,game.player.get_view_camera().global_basis)
  previous_body=game.player.global_basis;previous_head=game.player.get_view_camera().global_basis
  if rows.size()<384:rows.append(row)
 clock+=maxf(delta,0)
 if clock>=5:clock=0;write_r19()
 if probe.is_complete():probe=null;write_r19()
func write_r19():
 if not enabled or rows.is_empty():return
 var file=FileAccess.open("user://logs/quest-pose-r19.json",FileAccess.WRITE)
 if file!=null:file.store_string(JSON.stringify({"schema":1,"build":ProjectSettings.get_setting("application/config/version"),"samples":rows,"scope":"Opt-in private candidate; numeric pose errors/settings/sticks only, no positions, names or voice. Up to3x20s/384samples."}));file.close()
func _exit_tree():write_r19()
