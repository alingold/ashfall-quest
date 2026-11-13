extends SceneTree
func _initialize():call_deferred("run")
func run():
	var policy:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/render_policy_r16.json"))
	var failures:Array=[];var formats:Dictionary={};var bytes=0;var raw=0;var count=0
	for path in policy.image_imports:
		var config:=ConfigFile.new()
		if config.load(path)!=OK:failures.append(path+" import config missing");continue
		var texture:Texture2D=load(str(config.get_value("remap","path.etc2","")))
		if texture==null:failures.append(path+" mobile texture missing");continue
		var image:Image=texture.get_image()
		if image==null or not image.is_compressed():failures.append(path+" not GPU compressed");continue
		if maxi(image.get_width(),image.get_height())>1 and not image.has_mipmaps():failures.append(path+" no distance mipmaps")
		if image.get_format()<Image.FORMAT_ETC2_R11 or image.get_format()>Image.FORMAT_ETC2_RGBA8:failures.append(path+" not Android ETC2/EAC")
		formats[str(image.get_format())]=int(formats.get(str(image.get_format()),0))+1;bytes+=image.get_data_size();raw+=image.get_width()*image.get_height()*4;count+=1
	var result={"passed":failures.is_empty(),"textures":count,"formats":formats,"mobile_gpu_bytes_including_mips":bytes,"uncompressed_base_rgba_bytes":raw,"failures":failures,"scope":"Offline mobile texture artifacts, not a claim that all textures are simultaneously resident or that Quest frame rate is established."}
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/MOBILE-TEXTURES-R16.json",FileAccess.WRITE);file.store_string(JSON.stringify(result,"\t"));file.close();print("MOBILE_TEXTURES_R16 ",JSON.stringify(result));quit(0 if failures.is_empty() else 1)
