extends SceneTree
func _initialize():call_deferred("run")
func run():
	var archive=preload("res://scripts/archive_catalog1003.gd");archive.mount_local_pack_r7()
	var rows:Array=[];var seen:Dictionary={};var failures:Array=[]
	for entry in archive.rows():
		var slug:String=entry.slug
		var resource:Resource=load("res://local_archive_r10/"+slug+".res");var geometry:Node=resource.geometry.instantiate();var formats:Dictionary={};var textures=0
		for view in geometry.find_children("*","MeshInstance3D",true,false):
			var mat:Material=view.material_override
			if not mat is StandardMaterial3D:continue
			for key in ["albedo_texture","normal_texture"]:
				var texture:Texture2D=mat.get(key)
				if texture==null or seen.has(texture.get_instance_id()):continue
				seen[texture.get_instance_id()]=true;var img:Image=texture.get_image()
				if img==null:failures.append(slug+" unavailable texture image");continue
				textures+=1;var format:int=img.get_format();formats[str(format)]=int(formats.get(str(format),0))+1
				if img.is_compressed() and format in [Image.FORMAT_DXT1,Image.FORMAT_DXT3,Image.FORMAT_DXT5,Image.FORMAT_RGTC_R,Image.FORMAT_RGTC_RG,Image.FORMAT_BPTC_RGBA,Image.FORMAT_BPTC_RGBF,Image.FORMAT_BPTC_RGBFU]:failures.append(slug+" desktop-only compressed archive image "+str(format))
		rows.append({"map":slug,"unique_images":textures,"formats":formats});geometry.free();await process_frame
	var result={"passed":failures.is_empty() and rows.size()==105,"maps":rows,"failures":failures,"scope":"CPU image-format audit of all105 external archive resources; not Quest rendering or performance acceptance"}
	FileAccess.open(OS.get_cmdline_user_args()[0]+"/ARCHIVE-MOBILE-FORMATS.json",FileAccess.WRITE).store_string(JSON.stringify(result,"\t"));print("ARCHIVE_MOBILE_FORMATS ",JSON.stringify(result));quit(0 if failures.is_empty() else 1)
