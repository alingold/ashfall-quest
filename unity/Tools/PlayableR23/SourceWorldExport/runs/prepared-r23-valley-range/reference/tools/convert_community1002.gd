extends SceneTree
## Original adapter code. Inputs are data only; never runs source-map scripts.
const SOURCE="res://local_community/source/"
const OUT="res://local_community/converted/"
var materials:Dictionary={}
func _initialize()->void:call_deferred("convert")
func convert()->void:
	DirAccess.make_dir_recursive_absolute(OUT)
	var meta:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(SOURCE+"map.json"))
	var provenance:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(SOURCE+"provenance.json"))
	for key in provenance.materials:
		var row:Dictionary=provenance.materials[key];var m=StandardMaterial3D.new()
		m.resource_name=key
		if row.has("c"):m.albedo_texture=ImageTexture.create_from_image(Image.load_from_file(SOURCE+str(row.c)))
		m.vertex_color_use_as_albedo=true
		m.roughness=.72;m.metallic=.35 if row.get("metal",false) else 0.0
		m.cull_mode=BaseMaterial3D.CULL_DISABLED
		if row.has("n"):
			m.normal_enabled=true;m.normal_texture=ImageTexture.create_from_image(Image.load_from_file(SOURCE+str(row.n)));m.normal_scale=.6
		if row.get("alpha",false):m.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
		materials[key]=m
	var state=GLTFState.new();var doc=GLTFDocument.new()
	var err=doc.append_from_file(SOURCE+"geometry.glb",state)
	if err!=OK:push_error("GLTF geometry parse failed "+str(err));quit(1);return
	var raw:Node3D=doc.generate_scene(state);var output=Node3D.new();output.name="LocalCommunityGeometry"
	var batches:Dictionary={};var collisions:Dictionary={};var layers:Dictionary={}
	var triangles=0;var raw_count=0;var boundary_bounds=AABB();var regex=RegEx.new();regex.compile("^(world|roof)_(-?[0-9]+)_(-?[0-9]+)_(.+)$")
	for node in raw.find_children("*","MeshInstance3D",true,false):
		var mesh:Mesh=node.mesh;var match_row=regex.search(str(node.name))
		if match_row:
			var kind=match_row.get_string(1);var cx=int(match_row.get_string(2));var cz=int(match_row.get_string(3));var key=match_row.get_string(4)
			var batch="%s_%s_%d_%d"%[kind,key,floori(float(cx)/4),floori(float(cz)/4)]
			if not batches.has(batch):batches[batch]={"tool":SurfaceTool.new(),"key":key};batches[batch].tool.begin(Mesh.PRIMITIVE_TRIANGLES)
			for surface in mesh.get_surface_count():batches[batch].tool.append_from(mesh,surface,node.transform)
			var tile="%d_%d"%[cx,cz]
			if not collisions.has(tile):collisions[tile]=PackedVector3Array()
			var faces:PackedVector3Array=mesh.get_faces()
			for i in faces.size():faces[i]=node.transform*faces[i]
			collisions[tile].append_array(faces);triangles+=faces.size()/3;raw_count+=1
		else:
			layers[str(node.name)]=mesh.get_faces().size()/3
			if str(node.name)=="boundary":_body(output,mesh.get_faces(),"InvisibleBoundary",false);boundary_bounds=mesh.get_aabb()
			elif str(node.name)=="false":
				var view=MeshInstance3D.new();view.name="WalkThroughDecoration";view.mesh=mesh
				var m=StandardMaterial3D.new();m.vertex_color_use_as_albedo=true;m.cull_mode=BaseMaterial3D.CULL_DISABLED;view.material_override=m;output.add_child(view);view.owner=output
	for batch in batches:
		var view=MeshInstance3D.new();view.name="Draw_"+batch;view.mesh=batches[batch].tool.commit();view.material_override=materials[batches[batch].key];view.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;output.add_child(view);view.owner=output
	for tile in collisions:_body(output,collisions[tile],"World_"+tile,true)
	var packed=PackedScene.new();packed.pack(output);err=ResourceSaver.save(packed,OUT+"map.scn")
	var result={"title":meta.name,"creator":meta.creator,"source":provenance.url,"license":provenance.license,"center":meta.center,"spawns":meta.spawns,"extent":meta.extent,"raw_chunks":raw_count,"draw_batches":batches.size(),"collision_chunks":collisions.size(),"solid_triangles":triangles,"nonworld_layers":layers,"units":"metres; native glTF right-handed Y-up","materials":materials.size(),"bounds_policy":"Imported boundary collides but is invisible and not climbable. Walk-through decoration is non-solid; playfield is metadata only. Roofs remain solid. Runtime recovery handles fallen/outside players.","sha256":FileAccess.get_sha256(SOURCE+"map.glb"),"scene_sha256":FileAccess.get_sha256(OUT+"map.scn")}
	result.bounds_min=[boundary_bounds.position.x,boundary_bounds.position.y,boundary_bounds.position.z];var end=boundary_bounds.end;result.bounds_max=[end.x,end.y,end.z]
	var f=FileAccess.open(OUT+"manifest.json",FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close()
	raw.free();output.free();print("COMMUNITY_CONVERT "+JSON.stringify(result));quit(0 if err==OK else 1)
func _body(parent:Node,faces:PackedVector3Array,title:String,climb:bool)->void:
	var body=StaticBody3D.new();body.name=title;body.collision_layer=1;body.collision_mask=0
	if climb:body.add_to_group("climbable",true);body.add_to_group("construction_terrain",true)
	var shape=ConcavePolygonShape3D.new();shape.backface_collision=true;shape.set_faces(faces)
	var collider=CollisionShape3D.new();collider.shape=shape;body.add_child(collider);parent.add_child(body);body.owner=parent;collider.owner=parent
