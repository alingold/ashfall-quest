extends SceneTree
func _initialize():call_deferred("run")
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate();root.add_child(scene)
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.material_override.resource_name not in ["s15","vc","s3"]:continue
  
  var groups=preload("res://scripts/archive_semantics_r8.gd")._components(view.mesh)
  for row in groups.rows.values():
   var box:AABB=view.global_transform*row.box
   if box.get_center().distance_to(Vector3(-59.2498,159.9434,-5.38115))<8:print("HATCH_ACTUAL_COMPONENT ",scene.get_path_to(view)," center=",row.box.get_center()," size=",row.box.size," tris=",row.triangles)
 scene.free();quit()
