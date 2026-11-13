extends "res://scripts/authored_reflex_sight.gd"
## CX4 presentation correction only: the dot projects the same displayed ray fire() emits.
var reference:Node3D
static func install(weapon:Node3D)->void:
	if weapon.has_meta("cx4_ballistic_sight_r5") or not weapon.has_meta("ring_sight"):return
	var assembly:Node3D=weapon.get_meta("ring_sight")
	var bead:Node3D=assembly.find_child("SolidFrontDot",true,false)
	if bead!=null:bead.hide()
	var sight:Node3D=load("res://scripts/cx4_sight_r5.gd").new()
	weapon.add_child(sight);sight.configure(weapon,assembly.get_node("FrontReference"))
	weapon.set_meta("cx4_ballistic_sight_r5",sight);weapon.set_meta("reflex_sight",sight)
func configure(weapon:Node3D,marker:Node3D)->void:
	gun=weapon;reference=marker;style="rings";name="CX4BallisticDotR5";process_priority=101
	_build_collimated_dot();update_aim()
func update_aim()->void:
	if not is_instance_valid(gun) or not is_instance_valid(reference):return
	visible=gun.is_visible_in_tree()
	if not visible:return
	global_transform=reference.global_transform
	super.update_aim()
