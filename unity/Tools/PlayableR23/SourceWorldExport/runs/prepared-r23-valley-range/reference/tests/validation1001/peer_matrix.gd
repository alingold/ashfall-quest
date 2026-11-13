extends SceneTree
func _initialize()->void:call_deferred("boot")
func boot()->void:
	var controller:Node=preload("res://tests/validation1001/peer_controller.gd").new();controller.name="ValidationScenario1001";root.add_child(controller);controller.run()
