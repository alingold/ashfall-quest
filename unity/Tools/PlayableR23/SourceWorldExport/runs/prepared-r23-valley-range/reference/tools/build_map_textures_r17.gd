extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
var records:Dictionary={}
var saved:Dictionary={}
var output_root:String
var index_path:String
var source_bytes=0
func _initialize():call_deferred("run")
func texture(value:Texture2D,normal:bool)->void:
	var source:String=value.resource_path
	if not source.begins_with("res://local_archive/"):return
	var key=source+("|normal" if normal else "|color")
	if records.has(key):return
	var image:Image=value.get_image().duplicate()
	if image.is_compressed():image.decompress()
	var size:=Vector2i(image.get_width(),image.get_height());source_bytes+=image.get_data_size()
	var hash:=HashingContext.new();hash.start(HashingContext.HASH_SHA256);hash.update(image.get_data());var digest:String=hash.finish().hex_encode()
	var identity:String=(digest+("normal" if normal else "color")+":1024:r17").sha256_text()
	var destination:String=output_root.path_join(identity+".res")
	if not saved.has(identity):
		if maxi(size.x,size.y)>1024:
			var ratio:float=1024.0/maxi(size.x,size.y);image.resize(maxi(4,roundi(size.x*ratio)),maxi(4,roundi(size.y*ratio)),Image.INTERPOLATE_LANCZOS)
		if not image.has_mipmaps():image.generate_mipmaps(normal)
		var portable:=PortableCompressedTexture2D.new();portable.keep_compressed_buffer=true;portable.set_basisu_compressor_params(1,0.0)
		portable.create_from_image(image,PortableCompressedTexture2D.COMPRESSION_MODE_BASIS_UNIVERSAL,normal)
		if ResourceSaver.save(portable,destination,ResourceSaver.FLAG_COMPRESS)!=OK:push_error("Texture cache save failed");quit(1);return
		saved[identity]={"path":"res://assets/data/mobile_textures_r17/"+identity+".res","sha256":FileAccess.get_sha256(destination),"size":[portable.get_width(),portable.get_height()]}
		print("TEXTURE_R17 ",saved.size()," ",size," -> ",saved[identity].size," normal=",normal)
	records[key]={"source":source,"source_size":[size.x,size.y],"source_pixels_sha256":digest,"normal":normal,"path":saved[identity].path,"sha256":saved[identity].sha256,"size":saved[identity].size}
func material(value:Material)->void:
	if value is StandardMaterial3D:
		for property in value.get_property_list():
			var item=value.get(property.name)
			if item is Texture2D:texture(item,str(property.name).contains("normal"))
	elif value is ShaderMaterial and value.shader!=null:
		for property in value.shader.get_shader_uniform_list():
			var item=value.get_shader_parameter(property.name)
			if item is Texture2D:texture(item,str(property.name).contains("normal"))
func details(value:Variant)->void:
	if value is Material:material(value)
	elif value is Mesh:
		for surface in value.get_surface_count():material(value.surface_get_material(surface))
	elif value is Dictionary:
		for item in value.values():details(item)
	elif value is Array:
		for item in value:details(item)
func run():
	output_root=ProjectSettings.globalize_path("res://assets/data/mobile_textures_r17/");index_path=ProjectSettings.globalize_path("res://assets/data/mobile_texture_index_r17.json");DirAccess.make_dir_recursive_absolute(output_root)
	if FileAccess.file_exists(index_path):
		var previous=JSON.parse_string(FileAccess.get_file_as_string(index_path))
		if previous is Dictionary:
			records=previous.get("records",{})
			for record in records.values():
				var identity:String=str(record.path).get_file().trim_suffix(".res")
				saved[identity]={"path":record.path,"sha256":record.sha256,"size":record.size}
	var maps:Array=[]
	for row in Catalog.rows():
		if row.slug not in ["alfa-aglae-souza","battlefield-paintball-gods-pitdevil","deep-space-5-stand-alone-pro-yt"] and not OS.get_cmdline_user_args().has("--all-maps"):continue
		var resource:Resource=load("res://local_archive_r10/"+str(row.slug)+".res");var node:Node=resource.geometry.instantiate()
		for view in node.find_children("*","MeshInstance3D",true,false):
			material(view.material_override)
			if view.mesh!=null:
				for surface in view.mesh.get_surface_count():material(view.mesh.surface_get_material(surface))
		details(resource.details);node.free();maps.append(row.slug);print("TEXTURE_MAP_DONE ",row.slug)
	var file=FileAccess.open(index_path,FileAccess.WRITE);file.store_string(JSON.stringify({"schema":1,"library":Catalog.library_id(),"maps":maps,"records":records,"unique_textures":saved.size(),"source_bytes":source_bytes,"max_dimension":1024,"mipmapped":true,"compression":"Basis Universal, device-native GPU transcode"},"\t"));file.close();print("TEXTURE_R17_COMPLETE records=",records.size()," unique=",saved.size());quit()
