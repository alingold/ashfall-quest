extends SceneTree
func _initialize()->void:call_deferred("boot")
func boot()->void:
	var controller=preload("res://tests/hybrid1004.gd").new();controller.name="Hybrid1004";root.add_child(controller);controller.run()
