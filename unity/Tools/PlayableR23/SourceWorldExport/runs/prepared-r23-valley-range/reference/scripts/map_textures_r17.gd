extends RefCounted
## Per-map lifetime: never retain map textures through a static resource dictionary.
static var _index:Dictionary={}
var materials:Dictionary={}
var textures:Dictionary={}
var meshes:Dictionary={}
var replaced=0
var misses=0
func texture(original:Texture2D,normal:bool)->Texture2D:
	var key=original.resource_path+("|normal" if normal else "|color")
	if textures.has(key):return textures[key]
	var record:Dictionary=_index.get("records",{}).get(key,{})
	if record.is_empty():misses+=1;return original
	if int(record.source_size[0])!=original.get_width() or int(record.source_size[1])!=original.get_height():return original
	var path:String=record.path
	if not FileAccess.file_exists(path) or FileAccess.get_sha256(path)!=str(record.sha256):return original
	var result:Texture2D=load(path);textures[key]=result;replaced+=1;return result
func material(original:Material)->Material:
	if original==null:return null
	var id:int=original.get_instance_id()
	if materials.has(id):return materials[id]
	var result:Material=original.duplicate(false);materials[id]=result
	if result is StandardMaterial3D:
		for property in original.get_property_list():
			var item=original.get(property.name)
			if item is Texture2D:result.set(property.name,texture(item,str(property.name).contains("normal")))
		result.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	elif result is ShaderMaterial and result.shader!=null:
		for property in result.shader.get_shader_uniform_list():
			var item=original.get_shader_parameter(property.name)
			if item is Texture2D:result.set_shader_parameter(property.name,texture(item,str(property.name).contains("normal")))
	return result
func mesh(original:Mesh)->Mesh:
	if original==null:return null
	var id:int=original.get_instance_id()
	if meshes.has(id):return meshes[id]
	var result:Mesh=original.duplicate(false);meshes[id]=result
	for surface in original.get_surface_count():
		if result is ArrayMesh:result.surface_set_material(surface,material(original.surface_get_material(surface)))
	return result
func details(value:Variant)->Variant:
	if value is Material:return material(value)
	if value is Mesh:return mesh(value)
	if value is Dictionary:
		var copy:Dictionary=value.duplicate(false)
		for key in copy:copy[key]=details(copy[key])
		return copy
	if value is Array:
		var copy:Array=value.duplicate(false)
		for i in copy.size():copy[i]=details(copy[i])
		return copy
	return value
func apply(map:Node)->Dictionary:
	if OS.get_name()!="Android" and not OS.get_cmdline_user_args().has("--quest-render-r17"):return {"enabled":false}
	if _index.is_empty() and FileAccess.file_exists("res://assets/data/mobile_texture_index_r17.json"):
		var value=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/mobile_texture_index_r17.json"))
		if value is Dictionary:_index=value
	if _index.get("library","")!=preload("res://scripts/archive_catalog1003.gd").library_id():return {"enabled":false,"reason":"library mismatch"}
	for view in map._geometry.find_children("*","MeshInstance3D",true,false):
		view.material_override=material(view.material_override);view.mesh=mesh(view.mesh)
	map._details_r8=details(map._details_r8)
	return {"enabled":true,"gpu_textures":replaced,"unmapped_texture_bindings":misses}
