extends SceneTree
func _initialize():call_deferred("run")
func run():
 load("res://scripts/archive_catalog1003.gd").catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate()
 load("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:alfa-aglae-souza")
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null or view.material_override==null or view.material_override.resource_name!="vc":continue
  var groups=load("res://scripts/map_art_repair_r14.gd")._groups(view.mesh)
  for row in groups.rows.values():
   if row.box.get_center().distance_to(Vector3(-359.4039,36.3051,280.6427))<5:print("CAB_GROUP ",view.name," ",row)
 scene.free();quit()
