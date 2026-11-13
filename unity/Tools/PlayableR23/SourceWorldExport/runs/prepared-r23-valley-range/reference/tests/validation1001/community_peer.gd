extends SceneTree
func _initialize()->void:call_deferred("boot")
func boot()->void:
	var c=preload("res://tests/community_peer_controller1002.gd").new();c.name="CommunityNetworkProof";root.add_child(c);c.run()
