extends SceneTree
func _initialize():call_deferred("run")
func run():
	var catalog=load("res://scripts/archive_catalog1003.gd");catalog.mount_local_pack_r7()
	var rows:Array=[]
	for row in catalog.rows():
		if OS.get_cmdline_user_args().has("--alpha") and row.slug!="alfa-aglae-souza":continue
		var scene:Node=load("res://local_archive_r10/"+row.slug+".res").geometry.instantiate();var mats:Dictionary={};var counts:Dictionary={}
		for view in scene.find_children("*","MeshInstance3D",true,false):
			var mat:Material=view.material_override
			if mat==null:continue
			var key:String=mat.resource_name;counts[key]=int(counts.get(key,0))+1
			if mats.has(key):continue
			var data={"class":mat.get_class(),"resource":mat.resource_path}
			if mat is BaseMaterial3D:
				data.color=mat.albedo_color.to_html();data.vertex=mat.vertex_color_use_as_albedo;data.texture=mat.albedo_texture.resource_path if mat.albedo_texture!=null else "";data.filter=mat.texture_filter
				if mat.albedo_texture!=null:
					var image:Image=mat.albedo_texture.get_image()
					if image!=null:
						if image.is_compressed():image.decompress()
						data.image_size=[image.get_width(),image.get_height()];var pink=0;var white=0;var total=0
						for y in range(0,image.get_height(),maxi(1,image.get_height()/32)):
							for x in range(0,image.get_width(),maxi(1,image.get_width()/32)):
								var color:Color=image.get_pixel(x,y);total+=1
								if color.r>.8 and color.b>.6 and color.g<.5:pink+=1
								if minf(color.r,minf(color.g,color.b))>.95:white+=1
						data.pink_fraction=float(pink)/total;data.white_fraction=float(white)/total
			mats[key]=data
		rows.append({"id":row.id,"materials":mats,"counts":counts});scene.free();print("MATERIAL_RUNTIME_R14 ",row.id," materials=",mats.size())
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/MATERIAL-RUNTIME-R14.json",FileAccess.WRITE);file.store_string(JSON.stringify({"maps":rows},"\t"));quit()
