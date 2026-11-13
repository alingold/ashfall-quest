extends RefCounted
## Offline hash-bound triangle selectors. Rendering only; every source collision remains untouched.
static var _index:Dictionary={}
var materials:Dictionary={}
func material(kind:String)->Material:
 if materials.has(kind):return materials[kind]
 var result=ShaderMaterial.new();result.resource_name="OriginalSurfaceR17_"+kind
 result.shader=load("res://assets/materials/roadblock_r18.gdshader" if kind=="roadblock" else ("res://assets/materials/vehicle_r17.gdshader" if kind in ["van","truck","tire"] else "res://assets/materials/surface_r17.gdshader"))
 if kind!="roadblock":result.set_shader_parameter("style",{"van":0,"truck":1,"tire":2,"wood":0,"junction":1}.get(kind,0))
 materials[kind]=result;return result
func apply(geometry:Node,map_id:String,fingerprint:String)->Dictionary:
 if geometry.has_meta("art_r17"):return geometry.get_meta("art_r17")
 if _index.is_empty() and FileAccess.file_exists("res://assets/data/art_index_r17.json"):_index=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r17.json"))
 var row:Dictionary=_index.get("maps",{}).get(preload("res://scripts/archive_catalog1003.gd").slug(map_id),{})
 if row.is_empty() or row.cache_fingerprint!=fingerprint:return {}
 if not FileAccess.file_exists(row.path) or FileAccess.get_sha256(row.path)!=row.sha256:return {}
 var resource:Resource=load(row.path);var plans:Dictionary=resource.get_meta("plans");var stats:Dictionary={"draw_chunks":0,"guard_skips":0}
 for path in plans:
  var view:MeshInstance3D=geometry.get_node_or_null(path)
  if view==null or view.mesh==null:stats.guard_skips+=1;continue
  var h=HashingContext.new();h.start(HashingContext.HASH_SHA256);h.update(view.mesh.get_faces().to_byte_array())
  if h.finish().hex_encode()!=plans[path].sha256:stats.guard_skips+=1;continue
  var arrays:Array=view.mesh.surface_get_arrays(0);var old=arrays[Mesh.ARRAY_INDEX];var vertices=arrays[Mesh.ARRAY_VERTEX];var colors=arrays[Mesh.ARRAY_COLOR];var normals=arrays[Mesh.ARRAY_NORMAL];var erase:Dictionary={};var tools:Dictionary={};var roadblock_indices=PackedInt32Array()
  for part in plans[path].parts:
   var kind:String=part.kind
   if not tools.has(kind):tools[kind]=SurfaceTool.new();tools[kind].begin(Mesh.PRIMITIVE_TRIANGLES)
   var offset=0
   for triangle in part.triangles:
    var tri:int=triangle;erase[tri]=true
    for k in 3:
     var at:int=old[tri*3+k] if old!=null and not old.is_empty() else tri*3+k;var p:Vector3=part.uv[offset];offset+=1
     if kind=="roadblock":roadblock_indices.append(at);continue
     tools[kind].set_uv(Vector2(p.x,p.y));tools[kind].set_uv2(Vector2(p.z,0))
     tools[kind].set_color(colors[at] if colors!=null else Color.WHITE);tools[kind].set_normal(normals[at] if normals!=null else Vector3.UP);tools[kind].add_vertex(vertices[at])
   stats[kind]=int(stats.get(kind,0))+1
  var roadblock_mesh:ArrayMesh=null
  if not roadblock_indices.is_empty():
   roadblock_mesh=preload("res://scripts/source_surface_subset_r18.gd").build(view.mesh,roadblock_indices)
   if roadblock_mesh==null:stats.guard_skips+=1;continue
  var keep=PackedInt32Array();var count:int=old.size() if old!=null and not old.is_empty() else vertices.size()
  for tri in count/3:
   if erase.has(tri):continue
   for k in 3:keep.append(old[tri*3+k] if old!=null and not old.is_empty() else tri*3+k)
  var mesh=ArrayMesh.new()
  if not keep.is_empty():arrays[Mesh.ARRAY_INDEX]=keep;mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
  view.mesh=mesh
  for kind in tools:
   var detail=MeshInstance3D.new();detail.name="OriginalMatchedR17_"+kind;detail.mesh=roadblock_mesh if kind=="roadblock" else tools[kind].commit();detail.material_override=material(kind);detail.transform=view.transform;view.get_parent().add_child(detail,true)
   if kind=="roadblock":
    detail.layers=view.layers;detail.cast_shadow=view.cast_shadow
    for group in view.get_groups():detail.add_to_group(group)
   stats.draw_chunks+=1
 geometry.set_meta("art_r17",stats);return stats
func wood_panels(details:Dictionary)->int:
 var count=0
 for panel in details.panels:
  if panel.glass:continue
  var a:Array=panel.mesh.surface_get_arrays(0);var colors=a[Mesh.ARRAY_COLOR]
  if colors==null or colors.is_empty():continue
  var c:Color=colors[0]
  if not (c.r>c.b+.06 and c.r>.3 and c.r<.8):continue
  var tool=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES);var projected:Dictionary=preload("res://scripts/archive_prop_materials_r10.gd")._axes(panel.faces)
  for i in panel.faces.size():
   var p:Vector3=projected.points[i];tool.set_uv(Vector2(p.x,p.y));tool.set_uv2(Vector2(p.z,0));tool.set_color(c);tool.add_vertex(panel.faces[i])
  tool.generate_normals();panel.mesh=tool.commit();panel.material=material("wood");count+=1
 return count
