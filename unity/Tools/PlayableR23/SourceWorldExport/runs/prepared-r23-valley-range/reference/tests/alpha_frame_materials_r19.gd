extends SceneTree
## Read-only source material evidence; exports copies to this workspace evidence only.
func _initialize():call_deferred("run")
func run():
 preload("res://scripts/archive_catalog1003.gd").catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate();var seen={}
 for view in scene.find_children("*","MeshInstance3D",true,false):
  var material=view.material_override
  if not material is StandardMaterial3D or material.resource_name not in ["s3","s14","s15","s47"] or seen.has(material.resource_name):continue
  seen[material.resource_name]=true
  print("R19_FRAME_SOURCE_MATERIAL ",material.resource_name," color=",material.albedo_color," transparency=",material.transparency," texture=",material.albedo_texture)
  if material.albedo_texture!=null:
   var image=material.albedo_texture.get_image()
   if image.is_compressed():assert(image.decompress()==OK)
   assert(image.save_png(OS.get_cmdline_user_args()[0]+"/r19-source-material-"+material.resource_name+".png")==OK)
 scene.free();quit()
