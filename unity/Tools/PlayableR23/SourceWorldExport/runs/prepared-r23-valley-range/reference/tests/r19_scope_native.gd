extends SceneTree
var failures:=0
var capture_dir:=""
func _initialize()->void:call_deferred("run")
func run()->void:
	capture_dir=OS.get_cmdline_user_args()[0]
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):capture_dir=argument.trim_prefix("--capture-dir=")
	var actor=load("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);actor.set_process(false);actor.set_physics_process(false)
	var gun=load("res://scripts/precision_rifle.gd").new();root.add_child(gun);gun.setup(actor,false);gun.set_process(false)
	var vp:=SubViewport.new();vp.size=Vector2i(512,512);vp.own_world_3d=true;vp.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(vp)
	var camera:=Camera3D.new();vp.add_child(camera);camera.current=true;camera.projection=Camera3D.PROJECTION_FRUSTUM;camera.size=.12;camera.near=.05;camera.far=650
	var quad:=MeshInstance3D.new();quad.mesh=QuadMesh.new();quad.mesh.size=Vector2(4,4);quad.position.z=-.45;quad.custom_aabb=AABB(Vector3(-100,-100,-100),Vector3(200,200,200));camera.add_child(quad)
	var material:ShaderMaterial=gun.optic._material.duplicate();quad.material_override=material
	var white:=Image.create(256,256,false,Image.FORMAT_RGB8);white.fill(Color.WHITE)
	for y in range(145,152):
		for x in range(176,183):white.set_pixel(x,y,Color.RED)
	var texture:=ImageTexture.create_from_image(white)
	if OS.get_cmdline_user_args().has("--baseline"):material.shader=load("res://tests/r19_baseline_scope.gdshader")
	material.set_shader_parameter("optical_view",texture);material.set_shader_parameter("optical_right",texture);material.set_shader_parameter("angular_scale",1.0/3)
	var previous:=Basis.IDENTITY;var crossed:=false
	for eye in [-1.0,1.0]:
		camera.frustum_offset=Vector2(eye*.009,.015)
		var original_projection:=camera.get_camera_projection()
		for pitch in [0.0,.06,1.4,1.55,1.58,1.7,-1.4]:
			camera.global_transform=Transform3D(Basis.from_euler(Vector3(pitch,.7,.45)),Vector3(4800,1.65,4800))
			var forward:Vector3=-camera.global_basis.z
			var hint:Basis=gun.optic.continuous_level_basis(forward,previous,crossed)
			if absf(forward.y)>.98:crossed=true
			previous=hint;material.set_shader_parameter("view_right",hint.x)
			# No optical texture refresh: fixed white texture throughout batch.
			await process_frame;await RenderingServer.frame_post_draw
			var img:=vp.get_texture().get_image();var red_sum:=Vector2.ZERO;var red_count:=0;var low:=Vector2i(512,512);var high:=Vector2i(-1,-1)
			for y in 512:
				for x in 512:
					if img.get_pixel(x,y).r>.8 and img.get_pixel(x,y).g<.1:red_sum+=Vector2(x,y);red_count+=1
					if img.get_pixel(x,y).r>.15:low=low.min(Vector2i(x,y));high=high.max(Vector2i(x,y))
			var predicted_low:=Vector2(512,512);var predicted_high:=Vector2.ZERO
			for i in 256:
				var a:float=i*TAU/256;var direction:=Vector3(cos(a)/3,sin(a)/3,-1)
				# Projection in camera-local coordinates avoids world4800 subtraction.
				var clip:Vector4=original_projection*Vector4(direction.x,direction.y,direction.z,1)
				var pixel:=Vector2((clip.x/clip.w*.5+.5)*512,(-clip.y/clip.w*.5+.5)*512)
				predicted_low=predicted_low.min(pixel);predicted_high=predicted_high.max(pixel)
			var marker_world:Vector3=forward+hint.x*((179.5/256.0-.5)*2/3)+hint.y*(-(148.5/256.0-.5)*2/3)
			var marker_local:Vector3=camera.global_basis.inverse()*marker_world
			var marker_clip:Vector4=original_projection*Vector4(marker_local.x,marker_local.y,marker_local.z,1)
			var marker_expected:=Vector2((marker_clip.x/marker_clip.w*.5+.5)*512,(-marker_clip.y/marker_clip.w*.5+.5)*512)
			var marker_error:float=(red_sum/maxi(1,red_count)).distance_to(marker_expected)
			if marker_error>4:failures+=1
			print("ORIENTATION eye=",eye," pitch=",pitch," red_pixels=",red_count," marker_error_px=",marker_error)
			var error:float=maxf(Vector2(low).distance_to(predicted_low),Vector2(high).distance_to(predicted_high))
			if error>5 or camera.get_camera_projection()!=original_projection:failures+=1
			print("NATIVE eye=",eye," pitch=",pitch," bounds=",low,"..",high," predicted=",predicted_low,"..",predicted_high," error_px=",error," projection_unchanged=",camera.get_camera_projection()==original_projection)
			img.save_png(capture_dir+("/r19-baseline-eye-" if OS.get_cmdline_user_args().has("--baseline") else "/r19-candidate-eye-")+str(eye)+"-pitch-"+str(pitch)+".png")
	gun.shutdown();gun.queue_free();actor.queue_free();vp.queue_free();await process_frame
	print("R19_NATIVE failures=",failures," actual_xr_multiview=false headset=false");quit(failures)

