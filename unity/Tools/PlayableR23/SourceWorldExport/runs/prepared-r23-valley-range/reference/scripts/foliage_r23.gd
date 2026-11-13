extends RefCounted
## R23 foliage optimisation for archive maps.
## 1. Leaf triangles are removed from the baked world collision (you no longer stand on,
##    climb, or get blocked by tree leaves; trunks/branches and everything else keep collision).
## 2. Leaf meshes are split into spatial cells. Each cell draws every leaf card up close and
##    only one card in three beyond FAR_SWAP_M (45 m) (kept cards are enlarged slightly so distant
##    crowns stay filled). Render-only: no gameplay or map-semantics change.
const FAR_SWAP_M:=45.0
const SWAP_MARGIN_M:=3.0
const CELL_M:=24.0
const KEEP_ONE_IN:=3
const FAR_CARD_SCALE:=1.25
## Leaf albedo textures, from archive_semantics_r9.json material records. Batched demo maps
## have opaque names; these three were identified by inspecting the textures.
const LEAF_WORDS:=["leaves","leaf","foliage"]
const LEAF_TEXTURES:=["tex/s/demo-gamemap-2-batched/s4_c.png","tex/s/demo-gamemap-batched/s7_c.png","tex/s/demo-gamemap-batched/s46_c.png"]
static var _semantics:Dictionary={}

static func leaf_keys(map_id:String)->Array:
	if _semantics.is_empty():
		var parsed=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/archive_semantics_r9.json"))
		_semantics=parsed if parsed is Dictionary else {"maps":{}}
	var keys:=[]
	var materials:Dictionary=_semantics.get("maps",{}).get(map_id,{}).get("materials",{})
	for key in materials:
		var row:Dictionary=materials[key];var texture:String=str(row.get("c","")).to_lower()
		if not bool(row.get("alpha",false)) or bool(row.get("fx",false)):continue
		var leafy:bool=texture in LEAF_TEXTURES
		for word in LEAF_WORDS:leafy=leafy or texture.get_file().contains(word)
		if leafy:keys.append(str(key))
	return keys

static func apply(geometry:Node3D,map_id:String)->Dictionary:
	var keys:=leaf_keys(map_id)
	var result:={"leaf_materials":keys,"leaf_meshes":0,"leaf_triangles":0,"far_triangles":0,"cells":0,"collision_faces_removed":0,"shapes_rebuilt":0,"ms":0}
	if keys.is_empty() or OS.get_cmdline_user_args().has("--original-foliage-r23"):return result
	var started:=Time.get_ticks_msec()
	var views:Array[MeshInstance3D]=[]
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		if view.mesh==null or not view.visible:continue
		for key in keys:
			if str(view.name).contains("_"+key+"_"):views.append(view);break
	var leaf_keys_set:={}
	for view in views:
		var xf:=_relative(geometry,view)
		for surface in view.mesh.get_surface_count():
			var arrays:Array=view.mesh.surface_get_arrays(surface)
			var points:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX];var index=arrays[Mesh.ARRAY_INDEX]
			var count:int=index.size() if index!=null else points.size()
			for i in range(0,count-2,3):
				var a:=xf*points[index[i] if index!=null else i];var b:=xf*points[index[i+1] if index!=null else i+1];var c:=xf*points[index[i+2] if index!=null else i+2]
				leaf_keys_set[_key(a,b,c)]=true
	_strip_collision(geometry,leaf_keys_set,result)
	for view in views:_split(view,result)
	result.ms=Time.get_ticks_msec()-started
	print("FOLIAGE_R23 ",map_id," ",result)
	return result

static func _key(a:Vector3,b:Vector3,c:Vector3)->Vector3i:return Vector3i((((a+b+c)/3.0)*1000.0).round())

static func _relative(root:Node3D,node:Node3D)->Transform3D:
	var xf:=Transform3D.IDENTITY;var current:Node=node
	while current!=null and current!=root:
		if current is Node3D:xf=current.transform*xf
		current=current.get_parent()
	return xf

static func _strip_collision(geometry:Node3D,leaves:Dictionary,result:Dictionary)->void:
	if leaves.is_empty():return
	for body in geometry.find_children("*","StaticBody3D",true,false):
		for child in body.get_children():
			if not (child is CollisionShape3D and child.shape is ConcavePolygonShape3D):continue
			var xf:=_relative(geometry,child);var faces:PackedVector3Array=child.shape.get_faces()
			var kept:=PackedVector3Array();var removed:=0
			for i in range(0,faces.size()-2,3):
				if leaves.has(_key(xf*faces[i],xf*faces[i+1],xf*faces[i+2])):removed+=1;continue
				kept.append(faces[i]);kept.append(faces[i+1]);kept.append(faces[i+2])
			if removed==0:continue
			# Shapes may be shared; give this body its own rebuilt copy.
			var shape:=ConcavePolygonShape3D.new();shape.backface_collision=child.shape.backface_collision;shape.set_faces(kept);child.shape=shape
			if kept.is_empty():child.disabled=true
			result.collision_faces_removed+=removed;result.shapes_rebuilt+=1

static func _split(view:MeshInstance3D,result:Dictionary)->void:
	var parent:Node=view.get_parent();var cells:={}
	for surface in view.mesh.get_surface_count():
		var arrays:Array=view.mesh.surface_get_arrays(surface)
		var points:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
		var index:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array(range(points.size()))
		var material:Material=view.material_override if view.material_override!=null else view.mesh.surface_get_material(surface)
		# Leaf cards are connected triangle groups; union-find over shared vertex indices.
		var root:=PackedInt32Array(range(points.size()))
		for i in range(0,index.size()-2,3):
			var a:=_find(root,index[i])
			for j in [1,2]:
				var b:=_find(root,index[i+j])
				if a!=b:root[b]=a
		var cards:={}
		for i in range(0,index.size()-2,3):
			var r:=_find(root,index[i])
			if not cards.has(r):cards[r]=PackedInt32Array()
			cards[r].append(i)
		var serial:=0
		for card in cards.values():
			var center:=Vector3.ZERO;var corners:={}
			for t in card:
				for j in 3:corners[index[t+j]]=true
			for v in corners:center+=points[v]
			center/=maxi(1,corners.size())
			var cell:=Vector2i(floori(center.x/CELL_M),floori(center.z/CELL_M)) # ground-plane cells; whole crowns stay together
			var key:=str(surface)+":"+str(cell)
			if not cells.has(key):cells[key]={"arrays":arrays,"material":material,"near":PackedInt32Array(),"far":PackedInt32Array(),"centers":{}}
			var bucket:Dictionary=cells[key]
			bucket.near.append_array(card)
			if posmod(hash(serial*7919+card[0]),KEEP_ONE_IN)==0:
				bucket.far.append_array(card)
				for v in corners:bucket.centers[v]=center
			serial+=1
		result.leaf_triangles+=index.size()/3
	for key in cells:
		var bucket:Dictionary=cells[key]
		var near:=_cell_mesh(bucket.arrays,bucket.near,{},bucket.material)
		var far:=_cell_mesh(bucket.arrays,bucket.far,bucket.centers,bucket.material)
		if near==null:continue
		var near_view:=_cell_view(view,near,"FoliageNearR23");near_view.visibility_range_end=FAR_SWAP_M;near_view.visibility_range_end_margin=SWAP_MARGIN_M;parent.add_child(near_view)
		if far!=null:
			var far_view:=_cell_view(view,far,"FoliageFarR23");far_view.visibility_range_begin=FAR_SWAP_M;far_view.visibility_range_begin_margin=SWAP_MARGIN_M;parent.add_child(far_view)
			result.far_triangles+=bucket.far.size() # entries are triangle start offsets
		result.cells+=1
	view.visible=false;view.set_meta("foliage_split_r23",true);result.leaf_meshes+=1

static func _find(root:PackedInt32Array,i:int)->int:
	while root[i]!=i:
		root[i]=root[root[i]];i=root[i]
	return i

static func _cell_view(source:MeshInstance3D,mesh:ArrayMesh,label:String)->MeshInstance3D:
	var view:=MeshInstance3D.new();view.name=label;view.mesh=mesh;view.transform=source.transform;view.layers=source.layers
	view.cast_shadow=source.cast_shadow;view.visibility_range_fade_mode=GeometryInstance3D.VISIBILITY_RANGE_FADE_DISABLED
	return view

static func _cell_mesh(arrays:Array,triangles:PackedInt32Array,enlarge:Dictionary,material:Material)->ArrayMesh:
	if triangles.is_empty():return null
	var source_index:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array(range(arrays[Mesh.ARRAY_VERTEX].size()))
	var remap:={};var order:=PackedInt32Array();var index:=PackedInt32Array()
	for t in triangles:
		for j in 3:
			var v:int=source_index[t+j]
			if not remap.has(v):remap[v]=order.size();order.append(v)
			index.append(remap[v])
	var out:=[];out.resize(Mesh.ARRAY_MAX)
	for slot in Mesh.ARRAY_MAX:
		var data=arrays[slot]
		if data==null or slot in [Mesh.ARRAY_INDEX,Mesh.ARRAY_BONES,Mesh.ARRAY_WEIGHTS] or slot>=Mesh.ARRAY_CUSTOM0:continue
		out[slot]=_gather(data,order,4 if slot==Mesh.ARRAY_TANGENT else 1)
	if not enlarge.is_empty():
		var points:PackedVector3Array=out[Mesh.ARRAY_VERTEX]
		for i in order.size():
			var center:Vector3=enlarge.get(order[i],points[i])
			points[i]=center+(points[i]-center)*FAR_CARD_SCALE
		out[Mesh.ARRAY_VERTEX]=points
	out[Mesh.ARRAY_INDEX]=index
	var mesh:=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,out)
	mesh.surface_set_material(0,material)
	return mesh

static func _gather(data,order:PackedInt32Array,stride:int):
	if data is PackedVector3Array:
		var o:=PackedVector3Array();o.resize(order.size())
		for i in order.size():o[i]=data[order[i]]
		return o
	if data is PackedVector2Array:
		var o:=PackedVector2Array();o.resize(order.size())
		for i in order.size():o[i]=data[order[i]]
		return o
	if data is PackedColorArray:
		var o:=PackedColorArray();o.resize(order.size())
		for i in order.size():o[i]=data[order[i]]
		return o
	if data is PackedFloat32Array:
		var o:=PackedFloat32Array();o.resize(order.size()*stride)
		for i in order.size():
			for k in stride:o[i*stride+k]=data[order[i]*stride+k]
		return o
	return null
