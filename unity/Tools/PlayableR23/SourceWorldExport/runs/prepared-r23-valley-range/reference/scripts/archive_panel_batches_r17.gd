extends Node3D
## Exact source triangles, materials and collision. Rebuild only the affected tile
## when a door/window breaks; no shader discard, LOD swap or visibility cutoff.
var buckets:Dictionary={}
var membership:Dictionary={}
var dirty:Dictionary={}
var scheduled=false
var source_views=0
var triangles=0
static func tile_size()->float:
 for value in OS.get_cmdline_user_args():
  if value.begins_with("--panel-tile-r18="):return clampf(value.trim_prefix("--panel-tile-r18=").to_float(),16,256)
 # R18: 96x48x96 m tiles. Measured on Alpha: 300 -> 130 batch draws, identical
 # source triangles; worst single-tile rebuild stays well under a millisecond on PC.
 return 96.0
func setup(map:Node,panels:Array)->void:
 name="PanelBatchesR17"
 var span:float=tile_size()
 for panel in panels:
  var views:Array=[panel.view]
  views.append_array(panel.view.find_children("*","MeshInstance3D",true,false))
  var accepted:Array=[]
  for view in views:
   if view.mesh==null or view.mesh.get_surface_count()!=1:continue
   var material:Material=view.material_override
   if material==null:material=view.mesh.surface_get_material(0)
   if material==null:continue
   var center:Vector3=panel.box.get_center()
   var tile=Vector3i(floori(center.x/span),floori(center.y/(span*.5)),floori(center.z/span))
   var key=str(material.get_instance_id())+":"+str(tile)
   if not buckets.has(key):buckets[key]={"material":material,"parts":[],"view":null,"arrays":[],"ranges":[],"alive":[]}
   buckets[key].parts.append({"view":view,"panel":panel})
   accepted.append(key)
  if not accepted.is_empty():
   membership[panel.get_instance_id()]=accepted
   panel.batch_r17=weakref(self)
 for key in buckets:
  var bucket:Dictionary=buckets[key]
  # One original draw gains nothing from batching and needs no duplicate data.
  if bucket.parts.size()<2:continue
  var vertices=PackedVector3Array();var normals=PackedVector3Array();var uv=PackedVector2Array();var uv2=PackedVector2Array();var colors=PackedColorArray();var tangents=PackedFloat32Array()
  var tool=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES)
  var cursor=0
  for part in bucket.parts:
   var view:MeshInstance3D=part.view;var source:Array=view.mesh.surface_get_arrays(0)
   var transform:Transform3D=global_transform.affine_inverse()*view.global_transform
   var idx:PackedInt32Array=source[Mesh.ARRAY_INDEX] if source[Mesh.ARRAY_INDEX]!=null else PackedInt32Array()
   var count:int=idx.size() if not idx.is_empty() else source[Mesh.ARRAY_VERTEX].size()
   bucket.ranges.append(Vector2i(cursor,count));bucket.alive.append(not part.panel._broken)
   for i in count:
    var j:int=idx[i] if not idx.is_empty() else i
    vertices.append(transform*source[Mesh.ARRAY_VERTEX][j])
    normals.append((transform.basis*source[Mesh.ARRAY_NORMAL][j]).normalized() if source[Mesh.ARRAY_NORMAL]!=null else Vector3.UP)
    uv.append(source[Mesh.ARRAY_TEX_UV][j] if source[Mesh.ARRAY_TEX_UV]!=null else Vector2.ZERO)
    uv2.append(source[Mesh.ARRAY_TEX_UV2][j] if source[Mesh.ARRAY_TEX_UV2]!=null else Vector2.ZERO)
    colors.append(source[Mesh.ARRAY_COLOR][j] if source[Mesh.ARRAY_COLOR]!=null else Color.WHITE)
    if source[Mesh.ARRAY_TANGENT]!=null and source[Mesh.ARRAY_TANGENT].size()>j*4+3:
     var t:Vector3=transform.basis*Vector3(source[Mesh.ARRAY_TANGENT][j*4],source[Mesh.ARRAY_TANGENT][j*4+1],source[Mesh.ARRAY_TANGENT][j*4+2]);t=t.normalized()
     tangents.append_array(PackedFloat32Array([t.x,t.y,t.z,source[Mesh.ARRAY_TANGENT][j*4+3]]))
    else:tangents.append_array(PackedFloat32Array([1,0,0,1]))
   cursor+=count;source_views+=1
   view.layers=0
  var arrays:Array=[];arrays.resize(Mesh.ARRAY_MAX);arrays[Mesh.ARRAY_VERTEX]=vertices;arrays[Mesh.ARRAY_NORMAL]=normals;arrays[Mesh.ARRAY_TEX_UV]=uv;arrays[Mesh.ARRAY_TEX_UV2]=uv2;arrays[Mesh.ARRAY_COLOR]=colors;arrays[Mesh.ARRAY_TANGENT]=tangents
  bucket.arrays=arrays;triangles+=cursor/3
  var view=MeshInstance3D.new();view.name="ExactPanelTile";view.material_override=bucket.material;add_child(view);bucket.view=view
  _rebuild(key)
 map.statistics["panel_batches_r17"]={"source_draws":source_views,"batch_draws":get_child_count(),"triangles":triangles}
func changed(panel:Node)->void:
 for key in membership.get(panel.get_instance_id(),[]):
  if buckets[key].view==null:continue
  dirty[key]=true
 if not scheduled:scheduled=true;_flush.call_deferred()
func _flush()->void:
 scheduled=false
 for key in dirty:_rebuild(key)
 dirty.clear()
func _rebuild(key:String)->void:
 var bucket:Dictionary=buckets[key];var indices=PackedInt32Array()
 # R18: each part's index run is built once; a break/reset only concatenates runs.
 if not bucket.has("runs"):
  bucket["runs"]=[]
  for span in bucket.ranges:bucket.runs.append(PackedInt32Array(range(span.x,span.x+span.y)))
 for i in bucket.parts.size():
  if bucket.parts[i].panel._broken:continue
  indices.append_array(bucket.runs[i])
 var mesh=ArrayMesh.new()
 if not indices.is_empty():
  var arrays:Array=bucket.arrays.duplicate(false);arrays[Mesh.ARRAY_INDEX]=indices;mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
 bucket.view.mesh=mesh
