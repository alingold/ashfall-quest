extends SceneTree
func _initialize()->void:call_deferred("boot")
func boot()->void:
	var c=preload("res://tests/archive_peer_controller1003.gd").new();c.name="ArchiveNetworkProof";root.add_child(c);c.run()
