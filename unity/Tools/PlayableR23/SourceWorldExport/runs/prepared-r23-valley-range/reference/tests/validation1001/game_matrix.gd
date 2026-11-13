extends SceneTree
func _initialize()->void:call_deferred("boot")
func boot()->void:
	var args=OS.get_cmdline_user_args()
	if args.size()>0 and args[0]=="server" and args.has("--window-host"):root.disable_3d=true
	var controller:Node=preload("res://tests/validation1001/game_controller.gd").new();controller.name="GameValidation1001";root.add_child(controller);controller.run()
