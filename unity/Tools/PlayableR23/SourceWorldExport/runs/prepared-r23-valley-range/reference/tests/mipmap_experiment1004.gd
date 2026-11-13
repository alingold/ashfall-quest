extends RefCounted
static func apply(node:Node)->Dictionary:
	var materials:Dictionary={};var textures:Dictionary={};var modified=0
	for part in node.find_children("*","MeshInstance3D",true,false):
		var original:Material=part.material_override
		if not original is StandardMaterial3D:continue
		var key=original.get_instance_id()
		if not materials.has(key):
			var copy:StandardMaterial3D=original.duplicate(false)
			for property in ["albedo_texture","normal_texture"]:
				var texture:Texture2D=original.get(property)
				if texture==null:continue
				var tid=str(texture.get_instance_id())+property
				if not textures.has(tid):
					var image:Image=texture.get_image().duplicate()
					if image.is_compressed():image.decompress()
					if not image.has_mipmaps():image.generate_mipmaps(property=="normal_texture")
					textures[tid]=ImageTexture.create_from_image(image)
				copy.set(property,textures[tid])
			copy.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
			materials[key]=copy
		part.material_override=materials[key];modified+=1
	return {"materials":materials.size(),"textures":textures.size(),"mesh_overrides":modified,"scope":"in-memory render-only experimental mip chains; geometry, collision, source textures and local archive files untouched"}
