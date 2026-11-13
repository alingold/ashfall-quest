extends SceneTree
func _initialize():call_deferred("run")
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate()
 preload("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:alfa-aglae-souza")
 preload("res://scripts/map_art_r17.gd").new().apply(scene,"archive:alfa-aglae-souza","4bdb9eb629b352982c4d161ea9937b33eb320df341f56146970c0a5fbf56ca92")
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.mesh==null or view.mesh.get_surface_count()==0 or view.material_override==null or view.material_override.resource_name!="vc":continue
  var groups=preload("res://scripts/map_art_repair_r14.gd")._groups(view.mesh)
  for component in groups.rows.values():
   if component.box.get_center().distance_to(Vector3(57.5,25.4,61))<5:print("R19_VAN_COMPONENT ",view.name," ",component)
 scene.free();quit()
