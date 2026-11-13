extends SceneTree
## R18 measured foliage stability on the real Alpha map (native Vulkan Mobile, 4x MSAA
## project default). For real cutout foliage at several distances the camera makes a
## slow sub-pixel head drift; we measure frame-to-frame luminance change on foliage
## pixels (shimmer) and foliage pixel coverage (thinning), for material variants:
##   r17     authored cut .35, no A2C, bias 0          (R17 shipped state)
##   parent  cut .35 + A2C edge .30 (effective .65), normal<=.4, spec .15, bias .2
##   r18     discard .08 + A2C edge to the authored cut, normal<=.5, bias .2 (this branch)
##   r18_b0  as r18 with bias 0  /  r18_spec as r18 with spec .15
## PC GPU numbers are relative only; Quest acceptance still needs the headset.
var game:Node
var out:String
var checks=0
var failures:Array=[]
const W=480
const H=320
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func cutouts(map:Node)->Array:
	var mats:Dictionary={}
	for view in map.find_children("*","MeshInstance3D",true,false):
		var m=view.material_override
		if m is BaseMaterial3D and m.has_meta("r18_cutout_source"):mats[m.get_instance_id()]=m
	return mats.values()
func variant(materials:Array,name:String)->void:
	for m in materials:
		var src:Dictionary=m.get_meta("r18_cutout_source")
		m.normal_scale=src.normal_scale;m.metallic_specular=src.specular
		match name:
			"r17":
				m.alpha_scissor_threshold=src.threshold;m.alpha_antialiasing_mode=BaseMaterial3D.ALPHA_ANTIALIASING_OFF
			"parent":
				m.alpha_scissor_threshold=src.threshold;m.alpha_antialiasing_mode=BaseMaterial3D.ALPHA_ANTIALIASING_ALPHA_TO_COVERAGE;m.alpha_antialiasing_edge=.30
				m.normal_scale=minf(src.normal_scale,.4);m.metallic_specular=.15
			_:
				m.alpha_scissor_threshold=minf(preload("res://scripts/archive_materials1004.gd").CUTOUT_DISCARD_R18,src.threshold*.5)
				m.alpha_antialiasing_mode=BaseMaterial3D.ALPHA_ANTIALIASING_ALPHA_TO_COVERAGE;m.alpha_antialiasing_edge=src.threshold-m.alpha_scissor_threshold
				m.normal_scale=minf(src.normal_scale,.5)
				if name=="r18_spec":m.metallic_specular=.15
	root.texture_mipmap_bias=0.0 if name in ["r17","r18_b0","r17_nomsaa","r18_nomsaa"] else .2
	# Scope viewports render with MSAA disabled: coverage must match R17 there.
	root.msaa_3d=Viewport.MSAA_DISABLED if name.ends_with("_nomsaa") else Viewport.MSAA_4X
	if name=="r17_nomsaa":
		for m in materials:
			var src:Dictionary=m.get_meta("r18_cutout_source");m.alpha_scissor_threshold=src.threshold;m.alpha_antialiasing_mode=BaseMaterial3D.ALPHA_ANTIALIASING_OFF;m.normal_scale=src.normal_scale
func grab()->PackedFloat32Array:
	await process_frame;await RenderingServer.frame_post_draw
	var image:Image=root.get_texture().get_image()
	var cx:int=image.get_width()/2-W/2;var cy:int=image.get_height()/2-H/2
	var region:Image=image.get_region(Rect2i(cx,cy,W,H));region.convert(Image.FORMAT_L8)
	var bytes:PackedByteArray=region.get_data();var result:=PackedFloat32Array();result.resize(W*H)
	for i in W*H:result[i]=bytes[i]/255.0
	return result
func measure(camera:Camera3D,eye:Vector3,target:Vector3,foliage:Array,background:PackedFloat32Array)->Dictionary:
	var frames:Array=[];var side:Vector3=(target-eye).cross(Vector3.UP).normalized()
	for f in 16:
		# 1.5 mm/frame lateral drift + tiny yaw: a still-standing head's micro-motion.
		camera.global_position=eye+side*.0015*f;camera.look_at(target+side*.0005*f,Vector3.UP)
		frames.append(await grab())
	var mask:=PackedByteArray();mask.resize(W*H)
	var masked=0;var diff=0.0;var hot=0;var coverage=0.0
	for k in frames.size():
		var frame:PackedFloat32Array=frames[k];var count=0
		for i in W*H:
			if absf(frame[i]-background[i])>.03:count+=1;mask[i]=1
		coverage+=float(count)/(W*H)
	for i in W*H:masked+=mask[i]
	for k in range(1,frames.size()):
		var a:PackedFloat32Array=frames[k];var b:PackedFloat32Array=frames[k-1]
		for i in W*H:
			if mask[i]==1:
				var d:float=absf(a[i]-b[i]);diff+=d
				if d>.08:hot+=1
	var steps:int=(frames.size()-1)*maxi(1,masked)
	return {"foliage_pixels":masked,"coverage":snappedf(coverage/frames.size(),.0001),"shimmer_mean_abs_luma":snappedf(diff/steps,.00001),"shimmer_hot_fraction":snappedf(float(hot)/steps,.00001)}
func run():
	out=OS.get_cmdline_user_args()[0]
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
	var deadline=Time.get_ticks_msec()+180000
	while (not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready) and Time.get_ticks_msec()<deadline:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	for n in ["mission_label"]:if game.get(n) is CanvasItem:game.get(n).visible=false
	var materials:Array=cutouts(map)
	check(materials.size()>=1,"real cutout foliage materials present on Alpha")
	var foliage:Array=[]
	for view in map.find_children("*","MeshInstance3D",true,false):
		if view.material_override in materials and view.mesh!=null and view.layers!=0:foliage.append(view)
	# Deterministic foliage targets: centroids of real foliage triangles.
	var rng:=RandomNumberGenerator.new();rng.seed=1804;var targets:Array=[]
	for view in foliage:
		var faces:PackedVector3Array=view.mesh.get_faces()
		if faces.size()<3:continue
		for k in 6:
			var t:int=rng.randi_range(0,faces.size()/3-1)*3
			targets.append(view.global_transform*((faces[t]+faces[t+1]+faces[t+2])/3.0))
	var camera:Camera3D=game.player.desktop_camera;var space:PhysicsDirectSpaceState3D=map.get_world_3d().direct_space_state
	var views:Array=[]
	for distance in [12.0,40.0,110.0,260.0]:
		var found=0
		for target in targets:
			if found>=2:break
			for heading in 8:
				var dir:=Vector3(cos(heading*TAU/8),.25,sin(heading*TAU/8)).normalized();var eye:Vector3=target+dir*distance
				var ray:=PhysicsRayQueryParameters3D.create(eye,target);if not space.intersect_ray(ray).is_empty():continue
				if views.any(func(r):return r.eye.distance_to(eye)<1.0):continue
				views.append({"distance":distance,"eye":eye,"target":target});found+=1;break
	check(views.size()>=6,"found unobstructed foliage views at several distances")
	var names:=["r17","parent","r18","r18_b0","r18_spec","r17_nomsaa","r18_nomsaa"];var results:Array=[]
	for v in views:
		camera.global_position=v.eye;camera.look_at(v.target,Vector3.UP)
		# Foliage-hidden reference per MSAA mode, so masks only contain foliage pixels.
		for view in foliage:view.visible=false
		var backgrounds:Dictionary={}
		for mode in [Viewport.MSAA_4X,Viewport.MSAA_DISABLED]:
			root.msaa_3d=mode;await grab();await grab();backgrounds[mode]=await grab()
		for view in foliage:view.visible=true
		var row:Dictionary={"distance":v.distance,"eye":str(v.eye-map.global_position)}
		for name in names:
			variant(materials,name);await grab();await grab()
			row[name]=await measure(camera,v.eye,v.target,foliage,backgrounds[root.msaa_3d])
			if v==views[0] or v==views[views.size()-1]:root.get_texture().get_image().save_png(out+"/R18-FOLIAGE-d%d-%s.png"%[int(v.distance),name])
		var d:String=str(int(v.distance))+"m"
		check(row.r18_b0.coverage>=row.r17.coverage*.98,d+" shipped R18 foliage not thinner than R17 (4x MSAA)")
		check(row.r18_b0.shimmer_mean_abs_luma<=row.r17.shimmer_mean_abs_luma*1.02,d+" shipped R18 shimmer not worse than R17")
		# Scopes: never thinner than R17; the engine's mip-aware alpha scale may fill
		# distant crowns slightly (measured up to ~+5%), but must not blob them.
		check(row.r18_nomsaa.coverage>=row.r17_nomsaa.coverage*.98 and row.r18_nomsaa.coverage<=row.r17_nomsaa.coverage*1.10,d+" without MSAA (scopes) not thinner than R17 and <=10% fuller")
		check(row.r18_nomsaa.shimmer_mean_abs_luma<=row.r17_nomsaa.shimmer_mean_abs_luma*1.02,d+" without MSAA shimmer not worse than R17")
		results.append(row);print("R18_FOLIAGE ",JSON.stringify(row))
	variant(materials,"r18")
	var file=FileAccess.open(out+"/R18-FOLIAGE-STABILITY.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"msaa_3d_enum":root.msaa_3d,"materials":materials.size(),"foliage_instances":foliage.size(),"views":results,"scope":"Native PC Vulkan Mobile offscreen, sub-pixel camera drift; relative shimmer/coverage only, not headset acceptance."},"\t"));file.close()
	print("R18_FOLIAGE_DONE checks=",checks," failures=",failures.size())
	game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
