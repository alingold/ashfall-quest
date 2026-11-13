extends SceneTree
func _initialize():call_deferred("run")
func run():
 var catalog=load("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var scene=load("res://local_archive_r10/battlefield-paintball-gods-pitdevil.res").geometry.instantiate()
 var rows=[]
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.material_override==null or view.material_override.resource_name not in ["p8ad59c41","pd959969c"] or view.mesh==null:continue
  var box=view.mesh.get_aabb()
  if not box.has_point(Vector3(-3.96,78.7428,26.2366)):continue
  var hash=HashingContext.new();hash.start(HashingContext.HASH_SHA256);hash.update(view.mesh.get_faces().to_byte_array())
  rows.append({"path":str(scene.get_path_to(view)),"sha256":hash.finish().hex_encode(),"aabb":str(box),"triangles":view.mesh.get_faces().size()/3})
 var output={"map":"archive:battlefield-paintball-gods-pitdevil","cache_fingerprint":catalog.manifest("archive:battlefield-paintball-gods-pitdevil").cache_fingerprint,"source_frames":rows,"aperture":{"plane_x":-3.960613965988159,"y_min":74.84729766845703,"y_max":82.63829803466797,"z_min":22.694316864013672,"z_max":29.77889060974121},"status":"Source-authored hollow frame beside actual Welcome sign; generated display face is probe-only until native confirmation, not an original exported face."}
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/paintball-runtime-frame-r18.json",FileAccess.WRITE);file.store_string(JSON.stringify(output,"\t"));file.close()
 print("R18_RUNTIME_FRAME ",JSON.stringify(output));scene.free();quit()
