extends RefCounted
## Hash-bound original replacement art. Rendering only, after R17 and before semantics.
const INDEX="res://assets/data/art_index_r19.json"
var materials={}
func material(kind:String)->Material:
 if materials.has(kind):return materials[kind]
 var m=ShaderMaterial.new();m.shader=load("res://assets/materials/assembly_r19.gdshader")
 m.resource_name="OriginalAssemblyR19_"+kind
 m.set_shader_parameter("style",{"sign":0,"banner":0,"wheel":1,"deck":2,"rail":3,"steel":3}.get(kind,3))
 if kind=="banner":m.set_shader_parameter("two_sided_art",true)
 if kind in ["sign","banner"]:m.set_shader_parameter("art",load("res://assets/art_r21/northline-portrait_texture.res" if kind=="banner" else "res://assets/art_r19/freight_texture.res"))
 materials[kind]=m;return m
func apply(geometry:Node,map_id:String,fingerprint:String)->Dictionary:
 if geometry.has_meta("art_r19"):return geometry.get_meta("art_r19")
 var index=JSON.parse_string(FileAccess.get_file_as_string(INDEX))
 var row:Dictionary=index.maps.get(map_id.trim_prefix("archive:"),{})
 if row.is_empty() or row.fingerprint!=fingerprint:return {}
 if FileAccess.get_sha256(row.path)!=row.sha256:return {}
 var plans:Dictionary=load(row.path).get_meta("plans")
 var stats={"guard_skips":0,"draw_chunks":0}
 for path in plans:
  var view=geometry.get_node_or_null(path);var plan=plans[path]
  if not view is MeshInstance3D or view.mesh==null:stats.guard_skips+=1;continue
  if digest(view.mesh)!=plan.sha256:stats.guard_skips+=1;continue
  var arrays=view.mesh.surface_get_arrays(0);var ix=arrays[Mesh.ARRAY_INDEX];var vertices=arrays[Mesh.ARRAY_VERTEX]
  var erase={};var batches={}
  for part in plan.parts:
   if not batches.has(part.kind):batches[part.kind]={"indices":PackedInt32Array(),"uv":PackedVector2Array(),"uv2":PackedVector2Array()}
   var batch=batches[part.kind];var offset=0
   for tri in part.triangles:
    erase[tri]=true
    for k in 3:
     batch.indices.append(ix[tri*3+k] if ix!=null and not ix.is_empty() else tri*3+k)
     var p:Vector3=part.uv[offset];offset+=1
     batch.uv.append(Vector2(p.x,1-p.y));batch.uv2.append(Vector2(p.z,0))
   stats[part.kind]=int(stats.get(part.kind,0))+1
  var keep=PackedInt32Array()
  for tri in view.mesh.get_faces().size()/3:
   if erase.has(tri):continue
   for k in 3:keep.append(ix[tri*3+k] if ix!=null and not ix.is_empty() else tri*3+k)
  # Retain encoded source normals/attributes and exact source positions in both subsets.
  var remainder=ArrayMesh.new()
  if not keep.is_empty():remainder=preload("res://scripts/source_surface_subset_r18.gd").build(view.mesh,keep)
  if remainder==null:stats.guard_skips+=1;continue
  for kind in batches:
   var batch=batches[kind];var a=arrays.duplicate();var v=PackedVector3Array();var n=PackedVector3Array();var colors=PackedColorArray()
   for at in batch.indices:
    v.append(vertices[at]);n.append(arrays[Mesh.ARRAY_NORMAL][at]);colors.append(arrays[Mesh.ARRAY_COLOR][at] if arrays[Mesh.ARRAY_COLOR]!=null else Color.WHITE)
   a.resize(Mesh.ARRAY_MAX);a.fill(null);a[Mesh.ARRAY_VERTEX]=v;a[Mesh.ARRAY_NORMAL]=n;a[Mesh.ARRAY_COLOR]=colors;a[Mesh.ARRAY_TEX_UV]=batch.uv;a[Mesh.ARRAY_TEX_UV2]=batch.uv2
   var mesh=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,a)
   var detail=MeshInstance3D.new();detail.name="OriginalAssemblyR19_"+kind;detail.mesh=mesh;detail.material_override=material(kind)
   detail.transform=view.transform;detail.layers=view.layers;detail.cast_shadow=view.cast_shadow
   for group in view.get_groups():detail.add_to_group(group)
   view.get_parent().add_child(detail,true);stats.draw_chunks+=1
  view.mesh=remainder
 geometry.set_meta("art_r19",stats);return stats
static func digest(mesh:Mesh)->String:
 var h=HashingContext.new();h.start(HashingContext.HASH_SHA256);h.update(mesh.get_faces().to_byte_array());return h.finish().hex_encode()
