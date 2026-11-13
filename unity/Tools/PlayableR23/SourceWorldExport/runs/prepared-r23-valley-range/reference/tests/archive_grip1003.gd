extends SceneTree
var failures:Array=[]
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);print("FAIL ",label)
func body(parent:Node,at:Vector3,climb:bool)->void:
	var b=StaticBody3D.new();b.collision_layer=1;b.collision_mask=0
	if climb:b.add_to_group("climbable")
	var shape=CollisionShape3D.new();shape.shape=BoxShape3D.new();shape.shape.size=Vector3(2,2,2);b.add_child(shape);parent.add_child(b);b.position=at
func run()->void:
	var game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	var converted=Node3D.new();converted.name="ArchiveGeometry";game.add_child(converted);body(converted,Vector3(0,3,9000),false);body(converted,Vector3(5,3,9000),true)
	var original=Node3D.new();original.name="OriginalMapGeometry";game.add_child(original);body(original,Vector3(10,3,9000),false)
	for i in 3:await physics_frame
	var p=Vector3(1.05,3,9000);var q=PhysicsShapeQueryParameters3D.new();q.shape=game.player._grab_shape;q.transform=Transform3D(Basis.IDENTITY,p);q.collision_mask=1
	check(not game.get_world_3d().direct_space_state.get_rest_info(q).is_empty(),"archive boundary remains a solid physics contact")
	check(game.player.find_grab_surface(p).is_empty(),"invisible archive boundary cannot be grabbed")
	check(not game.player.find_grab_surface(Vector3(6.05,3,9000)).is_empty(),"archive architecture remains grabbable")
	check(not game.player.find_grab_surface(Vector3(11.05,3,9000)).is_empty(),"original-map grip behavior preserved")
	var row={"passed":failures.is_empty(),"failures":failures,"scope":"actual player grip-query regression with synthetic own geometry; archive boundary solidity and architecture/original-map grips"};var f=FileAccess.open(OS.get_cmdline_user_args()[0],FileAccess.WRITE);f.store_string(JSON.stringify(row,"\t"));f.close();print("ARCHIVE_GRIP "+JSON.stringify(row));quit(0 if failures.is_empty() else 1)
