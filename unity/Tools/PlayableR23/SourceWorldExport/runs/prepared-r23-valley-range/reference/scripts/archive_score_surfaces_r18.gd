extends RefCounted
## Source-frame display recovery. No static mesh/viewport references; no cached pack-detail mutation.
## The exported frame/aperture is original; the omitted display face is generated within that aperture.
const DATA_PATH="res://assets/data/world_score_surfaces_r18.json"
const DATA_SHA="d7043e09712a4a9e2190ebdb6047f9b7f31683dd93222c767215f9a8e66319bd"
static func faces(geometry:Node,map_id:String,fingerprint:String)->Array:
 var text=FileAccess.get_file_as_string(DATA_PATH).replace("\r\n","\n")
 if text.sha256_text()!=DATA_SHA:return []
 var root:Dictionary=JSON.parse_string(text);var data:Dictionary=root.maps.get(map_id,{})
 if data.is_empty() or not data.get("approved",false) or fingerprint!=data.cache_fingerprint or data.source_frames.size()!=2:return []
 var source_layers=1;var source_groups:Array=[]
 for source in data.source_frames:
  var view=geometry.get_node_or_null(source.path)
  if not view is MeshInstance3D or view.mesh==null or view.transform!=Transform3D.IDENTITY:return []
  source_layers=view.layers;source_groups=view.get_groups()
  var hash=HashingContext.new();hash.start(HashingContext.HASH_SHA256);hash.update(view.mesh.get_faces().to_byte_array())
  if hash.finish().hex_encode()!=source.sha256:return []
 var aperture:Dictionary=data.aperture
 var tl=Vector3(aperture.plane_x,aperture.y_max,aperture.z_max);var bl=Vector3(aperture.plane_x,aperture.y_min,aperture.z_max)
 var br=Vector3(aperture.plane_x,aperture.y_min,aperture.z_min);var tr=Vector3(aperture.plane_x,aperture.y_max,aperture.z_min)
 var vertices=PackedVector3Array([tl,bl,br,tl,br,tr]);var uv=PackedVector2Array([Vector2(0,0),Vector2(0,1),Vector2(1,1),Vector2(0,0),Vector2(1,1),Vector2(1,0)])
 var arrays=[];arrays.resize(Mesh.ARRAY_MAX);arrays[Mesh.ARRAY_VERTEX]=vertices;arrays[Mesh.ARRAY_NORMAL]=PackedVector3Array([Vector3.RIGHT,Vector3.RIGHT,Vector3.RIGHT,Vector3.RIGHT,Vector3.RIGHT,Vector3.RIGHT]);arrays[Mesh.ARRAY_TEX_UV]=uv
 var mesh=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays);mesh.resource_name="AuthoredWelcomeFrameDisplayR18";mesh.set_meta("source_layers_r18",source_layers);mesh.set_meta("source_groups_r18",source_groups)
 return [mesh]
