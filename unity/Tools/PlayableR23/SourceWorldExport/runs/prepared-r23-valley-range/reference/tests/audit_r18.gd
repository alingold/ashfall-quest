extends SceneTree
const C=preload("res://scripts/archive_catalog1003.gd")
func _initialize():call_deferred("run")
func run():
 C.catalog();var result:Array=[]
 for slug in ["alfa-aglae-souza","classic-bigbox-vr","phoenix-bigbox-vr","afterlife-gods-alamo-hank","aftermath-light-vs-dark-gods-badbar"]:
  var resource:Resource=load("res://local_archive_r10/"+slug+".res");var scene:Node=resource.geometry.instantiate();var row:Dictionary={"map":slug,"details":{},"areas":[],"s13":[],"materials":[]}
  for key in resource.details:
   var v=resource.details[key]
   if v is Array:row.details[key]=v.size()
  row.boards=resource.details.get("weapon_boards_r9",[])
  for area in scene.find_children("*","Area3D",true,false):
   var shapes:Array=[]
   for child in area.get_children():
    if child is CollisionShape3D:shapes.append({"position":str(child.position),"box":str(child.shape.get_debug_mesh().get_aabb())})
   row.areas.append({"name":str(area.name),"groups":area.get_groups(),"layer":area.collision_layer,"transform":str(area.transform),"shapes":shapes})
  var seen:Dictionary={}
  for view in scene.find_children("*","MeshInstance3D",true,false):
   var mat:Material=view.material_override
   if mat==null:continue
   if not seen.has(mat.resource_name):
    seen[mat.resource_name]=true;row.materials.append({"key":mat.resource_name,"texture":mat.albedo_texture.resource_path if mat is StandardMaterial3D and mat.albedo_texture!=null else ""})
   if mat.resource_name!="s13":continue
   var groups:Dictionary=preload("res://scripts/map_art_repair_r14.gd")._groups(view.mesh)
   for part in groups.rows.values():
    if part.box.size.y<1.4 or part.box.size.y>12:continue
    row.s13.append({"triangles":part.triangles,"center":str(part.box.get_center()),"size":str(part.box.size)})
  scene.free();result.append(row);print("AUDIT_R18 ",slug," details=",row.details," areas=",row.areas.size()," s13=",row.s13.size());await process_frame
 FileAccess.open(OS.get_cmdline_user_args()[0]+"/AUDIT-R18.json",FileAccess.WRITE).store_string(JSON.stringify(result,"\t"));quit()
