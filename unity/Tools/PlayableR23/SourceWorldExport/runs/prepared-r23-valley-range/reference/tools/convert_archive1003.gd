extends SceneTree
## Original converter. No archive scripts are evaluated.
const ROOT="res://local_archive/"
var materials:Dictionary={}
var output:Node3D
var slug:String
var source:String
var destination:String
func _initialize()->void:call_deferred("convert")
func convert()->void:
	slug=OS.get_cmdline_user_args()[0];source=ROOT+"maps/"+slug+"/source/";destination=ROOT+"maps/"+slug+"/converted/";DirAccess.make_dir_recursive_absolute(destination);DirAccess.make_dir_recursive_absolute(ROOT+"materials/");DirAccess.make_dir_recursive_absolute(ROOT+"textures/")
	var meta:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(source+"map.json"));var provenance:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(source+"provenance.json"))
	for key in provenance.materials:
		var row:Dictionary=provenance.materials[key];var signature=JSON.stringify(row).sha256_text();var path=ROOT+"materials/"+signature+".res"
		if FileAccess.file_exists(path):materials[key]=load(path);continue
		var mat=StandardMaterial3D.new();mat.resource_name=key;mat.vertex_color_use_as_albedo=true;mat.roughness=.72;mat.metallic=.35 if row.get("metal",false) else 0.0;mat.cull_mode=BaseMaterial3D.CULL_DISABLED
		if row.has("c"):mat.albedo_texture=_texture(str(row.c))
		if row.has("n"):mat.normal_enabled=true;mat.normal_texture=_texture(str(row.n));mat.normal_scale=.6
		if row.get("alpha",false) or "glass" in str(row.get("name","")).to_lower():mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
		if "glass" in str(row.get("name","")).to_lower():mat.albedo_color.a=.35
		ResourceSaver.save(mat,path,ResourceSaver.FLAG_COMPRESS);materials[key]=load(path)
	var state=GLTFState.new();var doc=GLTFDocument.new();var err=doc.append_from_file(source+"geometry.glb",state)
	if err!=OK:push_error("GLTF import "+str(err));quit(1);return
	var raw:Node3D=doc.generate_scene(state);root.add_child(raw);output=Node3D.new();output.name="ArchiveGeometry"
	var batches:Dictionary={};var collisions:Dictionary={};var layers:Dictionary={};var unknown:Array=[];var triangles=0;var chunks=0;var has_bounds=false;var solid_bounds=AABB();var boundary=AABB();var has_boundary=false
	var regex=RegEx.new();regex.compile("^(world|roof)_(-?[0-9]+)_(-?[0-9]+)_(.+)$")
	for node in raw.find_children("*","MeshInstance3D",true,false):
		var mesh:Mesh=node.mesh;var row=regex.search(str(node.name));var xf:Transform3D=node.global_transform
		var faces:PackedVector3Array=mesh.get_faces()
		for i in faces.size():faces[i]=xf*faces[i]
		if faces.is_empty():continue
		if row:
			var key=row.get_string(4);var cx=int(row.get_string(2));var cz=int(row.get_string(3));var batch="%s_%s_%d_%d"%[row.get_string(1),key,floori(float(cx)/4),floori(float(cz)/4)]
			if not batches.has(batch):batches[batch]={"tool":SurfaceTool.new(),"key":key};batches[batch].tool.begin(Mesh.PRIMITIVE_TRIANGLES)
			for surface in mesh.get_surface_count():batches[batch].tool.append_from(mesh,surface,xf)
			var tile="%d_%d"%[cx,cz]
			if not collisions.has(tile):collisions[tile]=PackedVector3Array()
			collisions[tile].append_array(faces);chunks+=1;triangles+=faces.size()/3
			var box=xf*mesh.get_aabb();solid_bounds=solid_bounds.merge(box) if has_bounds else box;has_bounds=true
		else:
			var kind=str(node.name).get_slice("_primitive",0);layers[kind]=int(layers.get(kind,0))+faces.size()/3
			match kind:
				"boundary":
					_body(faces,"Boundary",false);var box=xf*mesh.get_aabb();boundary=boundary.merge(box) if has_boundary else box;has_boundary=true
				"false":
					var view=MeshInstance3D.new();view.name="WalkThrough";view.mesh=mesh;view.transform=xf;var mat=StandardMaterial3D.new();mat.vertex_color_use_as_albedo=true;mat.cull_mode=BaseMaterial3D.CULL_DISABLED;view.material_override=mat;output.add_child(view);view.owner=output
				"killzone":_volumes(faces,"KillZones",32,"archive_killzone")
				"trigger":_volumes(faces,"UnscriptedTriggers",64,"archive_trigger")
				"playfield":pass # Metadata only, never a solid wall.
				_:unknown.append(str(node.name))
	if not unknown.is_empty() or not has_bounds:push_error("Unsupported mesh layers or empty world: "+str(unknown));quit(2);return
	for batch in batches:
		var view=MeshInstance3D.new();view.name="Draw_"+batch;view.mesh=batches[batch].tool.commit();view.material_override=materials[batches[batch].key];view.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;output.add_child(view);view.owner=output
	for tile in collisions:_body(collisions[tile],"World_"+tile,true)
	var packed=PackedScene.new();packed.pack(output);err=ResourceSaver.save(packed,destination+"map.scn",ResourceSaver.FLAG_COMPRESS)
	var limits=boundary if has_boundary else solid_bounds
	var result={"slug":slug,"map_id":"archive:"+slug,"title":str(meta.get("name",slug)),"creator":str(meta.get("creator","Unknown")),"source":str(provenance.source),"license":str(provenance.license),"recorded_spawns":meta.get("spawns",[]),"center":meta.get("center",[solid_bounds.get_center().x,solid_bounds.get_center().y,solid_bounds.get_center().z]),"template":str(meta.get("template","")),"bounds_min":_v(limits.position),"bounds_max":_v(limits.end),"solid_min":_v(solid_bounds.position),"solid_max":_v(solid_bounds.end),"raw_chunks":chunks,"draw_batches":batches.size(),"collision_chunks":collisions.size(),"solid_triangles":triangles,"layers":layers,"materials":materials.size(),"units":"metres, native right-handed glTF Y-up","scene_sha256":FileAccess.get_sha256(destination+"map.scn"),"source_sha256":FileAccess.get_sha256(source+"map.glb"),"rules_warning":"Archive original gameplay scripts/rules are not implemented. Trigger volumes are query-only. Scenery pickups are not functional ASHFALL items."}
	var paths:Dictionary={}
	for key in provenance.materials:
		var row:Dictionary=provenance.materials[key];paths[ROOT+"materials/"+JSON.stringify(row).sha256_text()+".res"]=true
		for field in ["c","n"]:
			if row.has(field):paths[ROOT+"textures/"+str(row[field]).sha256_text()+".res"]=true
	var keys:Array=paths.keys();keys.sort();var dependencies:Array=[];var fingerprint=str(result.scene_sha256)+"\n"
	for path in keys:
		var digest=FileAccess.get_sha256(path);dependencies.append({"path":path,"sha256":digest});fingerprint+=str(path)+":"+digest+"\n"
	result.dependencies=dependencies;result.local_cache_hash=fingerprint.sha256_text()
	var semantic="archive1003-converter-v2\n"+str(result.source_sha256)+"\n"+JSON.stringify(meta,"",true)+"\n"
	var texture_paths:Array=provenance.texture_uris;texture_paths.sort()
	for uri in texture_paths:semantic+=str(uri)+":"+FileAccess.get_sha256(ROOT+"shared/"+str(uri))+"\n"
	result.cache_fingerprint=semantic.sha256_text()
	var file=FileAccess.open(destination+"manifest.json",FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();raw.free();output.free();print("ARCHIVE_CONVERT "+slug+" triangles="+str(triangles)+" batches="+str(batches.size()));quit(0 if err==OK else 1)
func _texture(uri:String)->Texture2D:
	var path=ROOT+"textures/"+uri.sha256_text()+".res"
	if not FileAccess.file_exists(path):
		var image=Image.load_from_file(ROOT+"shared/"+uri);var texture=ImageTexture.create_from_image(image);ResourceSaver.save(texture,path,ResourceSaver.FLAG_COMPRESS)
	return load(path)
func _v(value:Vector3)->Array:return [value.x,value.y,value.z]
func _body(faces:PackedVector3Array,title:String,climb:bool)->void:
	var body=StaticBody3D.new();body.name=title;body.collision_layer=1;body.collision_mask=0
	if climb:body.add_to_group("climbable",true);body.add_to_group("construction_terrain",true)
	var shape=ConcavePolygonShape3D.new();shape.backface_collision=true;shape.set_faces(faces);var collider=CollisionShape3D.new();collider.shape=shape;body.add_child(collider);output.add_child(body);body.owner=output;collider.owner=output
func _volumes(faces:PackedVector3Array,title:String,layer:int,group:String)->void:
	# Disconnected archive volumes are decomposed separately; never hull the entire layer.
	var vertices:Dictionary={};var parent:Array[int]=[];var points:Array[Vector3]=[];var triangles:Array=[]
	for i in range(0,faces.size(),3):
		var ids:Array[int]=[]
		for k in 3:
			var point=faces[i+k];var key="%.4f,%.4f,%.4f"%[point.x,point.y,point.z]
			if not vertices.has(key):vertices[key]=points.size();parent.append(points.size());points.append(point)
			ids.append(vertices[key])
		for k in [1,2]:parent[_find(parent,ids[k])]=_find(parent,ids[0])
	var components:Dictionary={}
	for i in points.size():
		var at=_find(parent,i)
		if not components.has(at):components[at]=PackedVector3Array()
		components[at].append(points[i])
	var area=Area3D.new();area.name=title;area.collision_layer=layer;area.collision_mask=0;area.monitoring=false;area.add_to_group(group,true);output.add_child(area);area.owner=output
	for component in components.values():
		if component.size()<4:continue
		var shape=ConvexPolygonShape3D.new();shape.points=component;var collider=CollisionShape3D.new();collider.shape=shape;area.add_child(collider);collider.owner=output
func _find(parent:Array[int],index:int)->int:
	while parent[index]!=index:index=parent[index]
	return index
