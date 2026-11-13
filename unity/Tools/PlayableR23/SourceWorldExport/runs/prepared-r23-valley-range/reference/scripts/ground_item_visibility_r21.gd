extends RefCounted
## Render-only limit. Authority, pickup state, floor placement and held art stay intact.
const DISTANCE_M=40.0 # R23: was 60
const RETIRE_DETAIL_M=45.0
static func apply(node:Node):
 if node is GeometryInstance3D:
  if node.visibility_range_end<=0 or node.visibility_range_end>DISTANCE_M:
   node.visibility_range_end=DISTANCE_M
  node.visibility_range_end_margin=0
  node.visibility_range_fade_mode=GeometryInstance3D.VISIBILITY_RANGE_FADE_DISABLED
 for child in node.get_children():apply(child)

## R23: weapons lying on the map use reduced material copies. Held weapons keep the
## originals. Normal/detail/rim/clearcoat/height maps are dropped (fewer texture fetches
## per pixel), and anisotropic filtering is replaced with plain trilinear filtering.
static var _lite_r23:Dictionary={}
static func lite_r23(material:Material)->Material:
 if not material is BaseMaterial3D:return material
 var id:int=material.get_instance_id()
 if _lite_r23.has(id):return _lite_r23[id]
 var lite:BaseMaterial3D=material.duplicate()
 lite.normal_enabled=false;lite.detail_enabled=false;lite.rim_enabled=false;lite.clearcoat_enabled=false;lite.heightmap_enabled=false;lite.anisotropy_enabled=false;lite.subsurf_scatter_enabled=false
 lite.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
 lite.set_meta("ground_lite_r23",true)
 _lite_r23[id]=lite
 return lite
static func lite_materials_r23(node:Node)->void:
 if node is MeshInstance3D and node.mesh!=null:
  if node.material_override!=null:node.material_override=lite_r23(node.material_override)
  else:
   for s in node.mesh.get_surface_count():
    var active:Material=node.get_active_material(s)
    if active!=null:node.set_surface_override_material(s,lite_r23(active))
 for child in node.get_children():lite_materials_r23(child)