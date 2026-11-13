extends RefCounted
## Original optional material adapter. Derived archive textures stay local/ignored.
const CACHE="user://archive_mipmaps_r8/"
const ORIGINAL_CACHE="res://local_archive/mipmaps1004/"
## R18 cutout foliage/fence edges. Godot adds alpha_antialiasing_edge ON TOP of the
## scissor threshold and still hard-discards below the scissor value, so a plain
## "enable A2C" both thins leaves (edge .35+.30=.65) and keeps the aliased hard cut.
## Keep the authored cut as the effective coverage edge, and move the hard discard
## down to near-empty texels: MSAA alpha-to-coverage then resolves partial leaf edges,
## and the engine's mip-aware alpha scale keeps distant crowns filled instead of
## flickering holes. Without MSAA (scope viewports) the silhouette equals the old cut.
const CUTOUT_DISCARD_R18=.08
static func cutout_r18(copy:BaseMaterial3D)->void:
	if copy.has_meta("r18_cutout_source"):return
	var cut:float=copy.alpha_scissor_threshold
	copy.set_meta("r18_cutout_source",{"threshold":cut,"normal_scale":copy.normal_scale,"specular":copy.metallic_specular})
	copy.alpha_scissor_threshold=minf(CUTOUT_DISCARD_R18,cut*.5)
	copy.alpha_antialiasing_mode=BaseMaterial3D.ALPHA_ANTIALIASING_ALPHA_TO_COVERAGE
	copy.alpha_antialiasing_edge=cut-copy.alpha_scissor_threshold
	# Sub-pixel leaf normals alias into sparkle; halve, never amplify, authored strength.
	copy.normal_scale=minf(copy.normal_scale,.5)
static func apply(node:Node)->Dictionary:
	if node.has_meta("archive_materials1004_applied"):return node.get_meta("archive_materials1004_applied").duplicate(true)
	DirAccess.make_dir_recursive_absolute(CACHE)
	var materials:Dictionary={};var textures:Dictionary={};var hits=0;var generated=0;var modified=0;var failures:Array=[]
	for part in node.find_children("*","MeshInstance3D",true,false):
		var original:Material=part.material_override
		if not original is StandardMaterial3D:continue
		var key=original.get_instance_id()
		if not materials.has(key):
			var copy:StandardMaterial3D=original.duplicate(false)
			for property in ["albedo_texture","normal_texture"]:
				var texture:Texture2D=original.get(property)
				if texture==null:continue
				var source=texture.resource_path
				if source.is_empty() or not source.begins_with("res://local_archive/"):continue
				var tid=source+":"+property
				if not textures.has(tid):
					var source_hash=FileAccess.get_sha256(source);var cache_key=("mips1004-v1:"+tid+":"+source_hash).sha256_text();var path=CACHE+cache_key+".res";var meta_path=CACHE+cache_key+".json";var cached:Texture2D
					if not FileAccess.file_exists(path) and FileAccess.file_exists(ORIGINAL_CACHE+cache_key+".res"):
						path=ORIGINAL_CACHE+cache_key+".res";meta_path=ORIGINAL_CACHE+cache_key+".json"
					if FileAccess.file_exists(path) and FileAccess.file_exists(meta_path):
						var meta=JSON.parse_string(FileAccess.get_file_as_string(meta_path))
						if meta is Dictionary and meta.get("output_sha256","")==FileAccess.get_sha256(path) and meta.get("source_sha256","")==source_hash:cached=load(path) as Texture2D
					if cached!=null:hits+=1
					else:
						var image:Image=texture.get_image().duplicate()
						if image.is_compressed() and image.decompress()!=OK:failures.append(source+" decompression failed");continue
						if not image.has_mipmaps() and image.generate_mipmaps(property=="normal_texture")!=OK:failures.append(source+" mip generation failed");continue
						cached=ImageTexture.create_from_image(image)
						if ResourceSaver.save(cached,path,ResourceSaver.FLAG_COMPRESS)!=OK:failures.append(path+" cache save failed")
						else:
							var file=FileAccess.open(meta_path,FileAccess.WRITE)
							if file!=null:file.store_string(JSON.stringify({"source_path":source,"source_sha256":source_hash,"output_sha256":FileAccess.get_sha256(path),"normal_map":property=="normal_texture","local_only":true,"license":"UNKNOWN REUSE RIGHTS; derived local archive data, excluded from exports"}));file.close()
						generated+=1
					textures[tid]=cached
				copy.set(property,textures[tid])
			copy.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
			copy.roughness=maxf(.85,copy.roughness)
			if copy.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA and copy.albedo_color.a>.95 and original.resource_name!="pb8a19331":
				copy.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR;copy.alpha_scissor_threshold=.35
			if copy.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR:cutout_r18(copy)
			if original.resource_name=="pb8a19331":
				var field:=ShaderMaterial.new();field.shader=preload("res://assets/materials/force_field_r9.gdshader");field.set_shader_parameter("pattern",copy.albedo_texture);materials[key]=field
			else:materials[key]=copy
		part.material_override=materials[key];modified+=1
	var result={"materials":materials.size(),"textures":textures.size(),"cache_hits":hits,"generated":generated,"mesh_overrides":modified,"failures":failures,"scope":"local mipmapped material copies; original archive files, geometry/collision and provenance unchanged"}
	node.set_meta("archive_materials1004_applied",result);return result
