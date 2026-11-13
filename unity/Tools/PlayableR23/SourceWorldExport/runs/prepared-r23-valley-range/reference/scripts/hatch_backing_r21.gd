extends RefCounted
## Prebaked original hatch backing patches and public index LODs. No runtime clipping.
const F=preload("res://scripts/map_fixtures_r18.gd")
const INDEX="res://assets/data/hatch_backing_r21.json"
static func digest(faces:PackedVector3Array)->String:
 var h=HashingContext.new();h.start(HashingContext.HASH_SHA256);h.update(faces.to_byte_array());return h.finish().hex_encode()
static func apply(map:Node)->Dictionary:
 if map.has_meta("hatch_backing_r21"):return map.get_meta("hatch_backing_r21")
 var parsed=JSON.parse_string(FileAccess.get_file_as_string(INDEX))
 var row:Dictionary=parsed.get("maps",{}).get(F.resolve_slug(map.map_id),{}) if parsed is Dictionary else {}
 if row.is_empty():return {"status":"no_matching_hatches","apertures":0}
 if row.fingerprint!=str(map._meta.get("cache_fingerprint","")) or FileAccess.get_sha256(row.path)!=str(row.sha256):return {"status":"fingerprint_mismatch","apertures":0}
 var resource:Resource=ResourceLoader.load(row.path,"",ResourceLoader.CACHE_MODE_IGNORE)
 if resource==null:return {"status":"missing_patch","apertures":0}
 var plans:Dictionary=resource.get_meta("plans")
 var targets={}
 # Validate the entire original input before changing any geometry or collision.
 for type in ["meshes","shapes"]:
  for plan in plans[type]:
   var node=map._geometry.get_node_or_null(plan.path)
   if node==null or node.transform!=plan.transform:return {"status":"node_mismatch","apertures":0}
   var faces:PackedVector3Array=node.mesh.get_faces() if type=="meshes" else node.shape.get_faces()
   if digest(faces)!=str(plan.sha256):return {"status":"geometry_mismatch","apertures":0,"path":plan.path}
   targets[plan.path]=node
 for opening in plans.attachments:
  var found=false
  for panel in map._details_r8.panels:
   if str(panel.get("opening_id_r18",""))==opening:found=true
  if not found:return {"status":"missing_panel","apertures":0}
  for part in plans.attachments[opening]:
   if map._geometry.get_node_or_null(part.source_view)==null:return {"status":"missing_material","apertures":0}
 for plan in plans.meshes:targets[plan.path].mesh=plan.mesh
 for plan in plans.shapes:
  var shape=targets[plan.path].shape.duplicate();shape.set_faces(plan.faces);targets[plan.path].shape=shape
 for panel in map._details_r8.panels:
  var key=str(panel.get("opening_id_r18",""))
  if not plans.attachments.has(key):continue
  panel.attachments=panel.get("attachments",[]).duplicate()
  for part in plans.attachments[key]:
   var view=map._geometry.get_node(part.source_view)
   var mat:Material=view.material_override if view.material_override!=null else view.mesh.surface_get_material(0)
   panel.attachments.append({"mesh":part.mesh,"material":mat,"faces":part.mesh.get_faces()})
 var stats:Dictionary=resource.get_meta("statistics").duplicate(true)
 stats["prebaked"]=true;stats["runtime_clipping"]=false
 map.set_meta("hatch_backing_r21",stats);return stats
