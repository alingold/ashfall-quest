extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
func _initialize()->void:call_deferred("render_preview")
func render_preview()->void:
	var id="archive:"+OS.get_cmdline_user_args()[0];var meta=Catalog.manifest(id);var stage=Node3D.new();root.add_child(stage);stage.add_child(load(Catalog.scene(id)).instantiate())
	var world=WorldEnvironment.new();world.environment=Environment.new();world.environment.background_mode=Environment.BG_COLOR;world.environment.background_color=Color("9db6c6");world.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;world.environment.ambient_light_color=Color("c5d5e2");world.environment.ambient_light_energy=.8;world.environment.tonemap_mode=Environment.TONE_MAPPER_FILMIC;stage.add_child(world)
	var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-53,-28,0);sun.light_energy=1.0;stage.add_child(sun)
	var center:Array=meta.center;var low:Array=meta.solid_min;var high:Array=meta.solid_max;var target=Vector3(center[0],center[1],center[2]);var span=clampf(maxf(float(high[0])-float(low[0]),float(high[2])-float(low[2]))*.25,45,220)
	var camera=Camera3D.new();camera.far=10000;stage.add_child(camera);camera.current=true
	var best:Image;var best_score=-1.0
	var specs=[[.785,.8],[2.356,.8],[3.927,.8],[5.498,.8],[.785,1.6],[3.927,1.6]]
	# Optional lower angles keep a preview inside an authored transparent ceiling.
	if OS.get_cmdline_user_args().has("--low"):specs=[[.785,.12],[2.356,.12],[3.927,.12],[5.498,.12],[.785,.25],[3.927,.25]]
	for spec in specs:
		camera.position=target+Vector3(cos(spec[0])*span,span*spec[1],sin(spec[0])*span);camera.look_at(target,Vector3.UP)
		for i in 4:await process_frame
		var image=root.get_texture().get_image();var small=image.duplicate() as Image;small.resize(64,36);var score=0.0
		for y in range(1,36):
			for x in range(1,64):
				var c=small.get_pixel(x,y);var prior=small.get_pixel(x-1,y);var upper=small.get_pixel(x,y-1);score+=absf(c.get_luminance()-prior.get_luminance())+absf(c.get_luminance()-upper.get_luminance())+(maxf(c.r,maxf(c.g,c.b))-minf(c.r,minf(c.g,c.b)))*.04
		if score>best_score:best=image;best_score=score
	var path=Catalog.ROOT+"maps/"+Catalog.slug(id)+"/converted/preview.png";best.save_png(path);print("ARCHIVE_PREVIEW_OK "+id+" candidate score="+str(best_score));quit()
