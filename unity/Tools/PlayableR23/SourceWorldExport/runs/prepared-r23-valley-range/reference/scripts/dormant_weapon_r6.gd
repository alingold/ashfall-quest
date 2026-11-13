extends Node3D
## Ground instances share catalog art. Full input/reload/audio/model state is allocated on pickup.
var model:Node3D
var ammo=0
var magazine_size=0
var damage=0.0
var fire_interval=.2
var cooldown=0.0
var reload_state="ready"
var rocket_reserve=2
var motion_damping=1.0
var offhand_influence=0.0
var grip_toggle=false
var ammo_provider:Node
var ammo_slot=-1
func update_ammo_display()->void:pass
func reset_weapon()->void:ammo=magazine_size;reload_state="ready"
func refresh_hands()->bool:return false
func shutdown()->void:pass
func set_weapon_settings(settings:Dictionary)->void:
	motion_damping=float(settings.get("motion_damping",motion_damping));offhand_influence=float(settings.get("offhand_influence",offhand_influence));grip_toggle=bool(settings.get("grip_toggle",grip_toggle))
func get_loaded_rounds()->int:return ammo
