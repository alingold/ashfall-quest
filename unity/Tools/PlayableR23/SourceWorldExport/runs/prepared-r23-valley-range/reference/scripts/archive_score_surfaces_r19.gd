extends RefCounted
## Generic measured-aperture extension. Uses existing score presentation/authority unchanged.
const DATA="res://assets/data/world_score_surfaces_r19.json"
const SHA="c20a17461e49c8da982be7af843e51bb25d4b841579b2094084584fee9c2025f"
static func vector(a:Array)->Vector3:return Vector3(a[0],a[1],a[2])
static func faces(geometry:Node,map_id:String,fingerprint:String)->Array:
 var text=FileAccess.get_file_as_string(DATA).replace("\r\n","\n")
 if text.sha256_text()!=SHA:return []
 var data:Dictionary=JSON.parse_string(text).maps.get(map_id,{})
 if data.is_empty() or data.cache_fingerprint!=fingerprint:return []
 var layers=1;var groups=[]
 for source in data.source_frames:
  var view=geometry.get_node_or_null(source.path)
  if not view is MeshInstance3D or view.mesh==null or view.transform!=Transform3D.IDENTITY:return []
  if preload("res://scripts/map_art_r19.gd").digest(view.mesh)!=source.sha256:return []
  layers=view.layers;groups=view.get_groups()
 var result=[]
 for aperture in data.apertures:
  var tl=vector(aperture.top_left);var bl=vector(aperture.bottom_left);var br=vector(aperture.bottom_right);var tr=vector(aperture.top_right);var normal=vector(aperture.normal)
  var a=[];a.resize(Mesh.ARRAY_MAX)
  a[Mesh.ARRAY_VERTEX]=PackedVector3Array([tl,bl,br,tl,br,tr])
  a[Mesh.ARRAY_NORMAL]=PackedVector3Array([normal,normal,normal,normal,normal,normal])
  a[Mesh.ARRAY_TEX_UV]=PackedVector2Array([Vector2(0,0),Vector2(0,1),Vector2(1,1),Vector2(0,0),Vector2(1,1),Vector2(1,0)])
  var mesh=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,a);mesh.resource_name="AuthoredTownFrameDisplayR19"
  mesh.set_meta("source_layers_r18",layers);mesh.set_meta("source_groups_r18",groups);result.append(mesh)
 return result
