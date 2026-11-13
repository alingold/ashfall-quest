extends SceneTree
const C=preload("res://scripts/archive_catalog1003.gd")
const Parts=preload("res://scripts/archive_semantics_r8.gd")
func _initialize():call_deferred("run")
func arr(v:Vector3)->Array:return [v.x,v.y,v.z]
func run():
 C.catalog()
 var path="res://assets/data/map_fixtures_r18.json"
 var data=JSON.parse_string(FileAccess.get_file_as_string(path))
 for slug in data.maps:
  var record=data.maps[slug]
  if record.get("tower_gate_grates",[]).is_empty():continue
  var original=load("res://local_archive_r10/"+slug+".res");var scene=original.geometry.instantiate();root.add_child(scene)
  var parts=[];var hatches=[];var fragments=[];var liners=[]
  for view in scene.find_children("*","MeshInstance3D",true,false):
   if view.material_override==null or view.material_override.resource_name not in ["s2","s3","s14","s15","vc"]:continue
   var groups=Parts._components(view.mesh)
   for id in groups.rows:
    var r=groups.rows[id];var c=r.box.get_center()
    if absf(c.y-159.943)<.05 and r.triangles==142 and view.material_override.resource_name in ["s14","s15"] and Vector2(c.x+152.8,c.z-19.2).length()<110:hatches.append({"key":view.material_override.resource_name,"box":r.box,"triangles":r.triangles})
    if absf(c.y-159.943)<.05 and r.triangles in [105,37] and view.material_override.resource_name in ["s14","s15"] and Vector2(c.x+152.8,c.z-19.2).length()<110:fragments.append({"key":view.material_override.resource_name,"box":r.box,"triangles":r.triangles})
    if view.material_override.resource_name in ["s2","s3"] and absf(c.y-160.07)<.05 and r.triangles in [8,14,22] and r.box.size.y<8 and r.box.size.x<10 and r.box.size.z<10 and Vector2(c.x+152.8,c.z-19.2).length()<110:liners.append({"key":view.material_override.resource_name,"box":r.box,"triangles":r.triangles})
    if view.material_override.resource_name=="vc" and c.y>156 and c.y<164 and r.triangles in [1,2,4,6,8]:parts.append({"key":"vc","box":r.box,"triangles":r.triangles})
  for a in fragments:
   if a.triangles!=105:continue
   for b in fragments:
    if b.triangles!=37 or b.key!=a.key or a.box.get_center().distance_to(b.box.get_center())>4:continue
    hatches.append({"key":a.key,"box":a.box.merge(b.box),"triangles":142,"members":[a,b]})
  print("R21_HATCH_CENTERS ",slug," count=",hatches.size())
  assert(hatches.size()==10,"Exact ten octagonal tower hatches "+slug+" count="+str(hatches.size()))
  var assemblies=[]
  for old in record.tower_gate_grates:
   if not str(old.opening_id).begins_with("mid_"):assemblies.append(old)
  for i in hatches.size():
   var hatch=hatches[i];var members=hatch.get("members",[hatch]).duplicate();var box:AABB=hatch.box
   for p in parts:
    if box.grow(.5).encloses(p.box):members.append(p)
   assert(members.reduce(func(total,p):return total+int(p.triangles),0)==158,"One142-triangle frame and16-triangle backing, "+slug+" hatch"+str(i)+" members="+str(members))
   var lining=[]
   for inner in liners:
    if inner.box.get_center().distance_to(hatch.box.get_center())<4:lining.append(inner)
   assert(lining.reduce(func(total,p):return total+int(p.triangles),0)==22,"Complete22-triangle inner hatch backing "+slug)
   members.append_array(lining)
   for member in members:box=box.merge(member.box)
   var specs=[];var triangles=0
   for part in members:
    var b:AABB=part.box;triangles+=part.triangles
    specs.append({"role":"octagonal_frame_and_cross" if part.key in ["s14","s15"] else ("inner_backing" if part.key in ["s2","s3"] else "backing_sector"),"key":part.key,"triangles":part.triangles,"center":arr(b.get_center()),"size":arr(b.size),"min":arr(b.position),"max":arr(b.end)})
   assemblies.append({"opening_id":"mid_"+str(i),"cardinal":"octagonal_middle","fixture_kind":"tower_mid_hatch","center":arr(box.get_center()),"size":arr(box.size),"min":arr(box.position),"max":arr(box.end),"total_triangles":triangles,"parts":specs,"evidence":"R21 actual Alpha native octagonal tower hatch, all ten exact source assemblies at159.94m. Existing R18 eight lower perimeter gates retained."})
  record.tower_gate_grates=assemblies;record.expected_member_specs=0;record.expected_triangles=0
  for a in assemblies:
   for part in a.parts:
    for m in part.get("members",[part]):record.expected_member_specs+=1;record.expected_triangles+=int(m.triangles)
  print("R21_HATCH_BAKE ",slug," openings=",assemblies.size()," members=",record.expected_member_specs," triangles=",record.expected_triangles)
  scene.free()
 data["r21_middle_hatch_note"]="Adds actual ten octagonal mid-height tower hatches per matching tower template, frame/cross, all backing sectors and inner22-triangle lining. Reuses fail-closed R18 render/collision extraction and authoritative breakables; retained source archives immutable."
 var f=FileAccess.open(path,FileAccess.WRITE);f.store_string(JSON.stringify(data,"  "));f.close();quit()
