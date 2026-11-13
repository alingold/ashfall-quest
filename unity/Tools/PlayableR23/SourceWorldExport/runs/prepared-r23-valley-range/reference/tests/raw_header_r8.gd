extends SceneTree
func _initialize():call_deferred("run")
func run():
	var output:String=OS.get_cmdline_user_args()[0];var base:String="C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1004-audit/candidate/local_archive/maps/"
	preload("res://scripts/archive_catalog1003.gd").catalog();var facts:Array=[]
	for slug in ["bamboo-gods-pitdevil","banana-land-gods-pitdevil"]:
		var path:String="C:/Users/aling/Documents/Codex/2026-10-24/build-a-large-beautiful-playable-map/work/r8/raw-source/"+slug+".glb";var meta:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(base+slug+"/source/map.json"));var provenance:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(base+slug+"/source/provenance.json"))
		var state:=GLTFState.new();var document:=GLTFDocument.new();var error:int=document.append_from_file(path,state)
		if error!=OK:push_error("Raw source import failed "+str(error));quit(1);return
		var scene:Node3D=document.generate_scene(state);root.add_child(scene);var faces=0;var meshes=0
		for view in scene.find_children("*","MeshInstance3D",true,false):
			var regex:=RegEx.new();regex.compile("^(world|roof)_(-?[0-9]+)_(-?[0-9]+)_(.+?)(?:_primitive[0-9]+)?$")
			var match_row:RegExMatch=regex.search(str(view.name))
			if not match_row:view.hide();continue
			var key:String=match_row.get_string(4);var material_row:Dictionary=provenance.materials.get(key,{})
			var material_path:String="res://local_archive/materials/"+JSON.stringify(material_row).sha256_text()+".res"
			if ResourceLoader.exists(material_path):view.material_override=load(material_path)
			faces+=view.mesh.get_faces().size()/3;meshes+=1
		preload("res://scripts/archive_materials1004.gd").apply(scene)
		var environment:=WorldEnvironment.new();var settings:=Environment.new();settings.background_mode=Environment.BG_COLOR;settings.background_color=Color("9db6c6");settings.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;settings.ambient_light_color=Color("c5d5e2");settings.ambient_light_energy=.8;settings.tonemap_mode=Environment.TONE_MAPPER_FILMIC;environment.environment=settings;scene.add_child(environment)
		var sun:=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-53,-28,0);sun.light_energy=1;scene.add_child(sun)
		var camera:=Camera3D.new();scene.add_child(camera);var center:=Vector3(meta.center[0],meta.center[1],meta.center[2]);camera.position=center+Vector3(0,450,380);camera.look_at(center);camera.far=2500;camera.current=true
		for i in 8:await process_frame
		await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/RAW-SOURCE-"+slug+".png")
		facts.append({"slug":slug,"raw_source":path,"sha256":FileAccess.get_sha256(path),"solid_triangles":faces,"solid_meshes":meshes,"scope":"Diagnostic copy of original Delicious map.glb: extension declaration and render-material references removed; binary geometry, accessors and node transforms unchanged. ASHFALL Main, map converter and previous maps absent. Cached materials/camera/light only."})
		scene.free();await process_frame
	var file=FileAccess.open(output+"/RAW-SOURCE-R8.json",FileAccess.WRITE);file.store_string(JSON.stringify(facts,"\t"));print("RAW_SOURCE_R8 maps=",facts.size());quit()


