extends SceneTree
class TrackedAWP extends "res://scripts/precision_rifle.gd":
	func _vr_active()->bool:return true
func _initialize()->void:call_deferred("run")
func run()->void:
	var actor=load("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);actor.xr_enabled=true;actor.set_process(false);actor.set_physics_process(false)
	var gun:=TrackedAWP.new();root.add_child(gun);gun.setup(actor,false);gun.set_process(false)
	gun.global_position=Vector3(4800,1.4,4800)
	actor.xr_camera.global_transform=Transform3D(Basis.IDENTITY,Vector3(4800,1.65,4800))
	gun.optic._commit_displayed_frame()
	var failures:=0
	var max_error:=0.0
	for frame in 3:
		actor.xr_camera.global_basis=Basis.from_euler(Vector3(-.06*(frame+1),.09*(frame+1),.2))
		var current:Vector3=-actor.xr_camera.global_basis.z
		var stale:Vector3=gun.optic._material.get_shader_parameter("view_forward")
		var ray_error:float=rad_to_deg(current.angle_to(stale))
		print("SCOPE_STALE frame=",frame," latest_forward=",current," cached_forward=",stale," angular_error_deg=",ray_error)
	# Godot CameraData folds view_offset.inverse into each per-eye projection.
	# Round-trip asymmetric frusta including eye rotation and translation.
	var samples:=0
	for eye in [-1.0,1.0]:
		var projection:=Projection.create_frustum(-.07+eye*.009,.08+eye*.009,-.06,.09,.05,650.0)
		var projection_before:=projection
		var offset:=Transform3D(Basis(Vector3.UP,eye*.03),Vector3(eye*.032,0,.01))
		var combined:=projection*Projection(offset.affine_inverse())
		var center_before:=Vector2.INF
		for frame in 90:
			# No optical commit in this loop. Latest render yaw/pitch/roll only.
			var latest:=Transform3D(Basis.from_euler(Vector3(.5*sin(frame*.1),frame*.07,.7*sin(frame*.2))),Vector3(4800,1.65,4800))
			actor.xr_camera.global_transform=latest
			var forward:Vector3=-latest.basis.z
			var image_basis:Basis=gun.optic.world_level_basis(forward)
			for local in [Vector3(0,0,-1),Vector3(1.0/3,0,-1),Vector3(0,1.0/3,-1)]:
				var world_direction:Vector3=latest.basis*local
				# Shader transforms directions with w=0: never subtract world4800 points.
				var camera_point:Vector3=offset.origin+latest.basis.inverse()*world_direction
				var clip:Vector4=combined*Vector4(camera_point.x,camera_point.y,camera_point.z,1)
				var ndc:=Vector2(clip.x,clip.y)/clip.w
				if local==Vector3(0,0,-1):
					if center_before==Vector2.INF:center_before=ndc
					# Direction-only projection must ignore large world translation.
					if ndc.distance_to(center_before)>.00001:failures+=1
				var reconstructed:Vector4=combined.inverse()*(clip/clip.w)
				var ray:Vector3=(Vector3(reconstructed.x,reconstructed.y,reconstructed.z)/reconstructed.w-offset.origin).normalized()
				var aperture:=Vector2(ray.x,ray.y)/(-ray.z)/(1.0/3)
				var expected:Vector2=Vector2(local.x,local.y)*3
				var error:float=aperture.distance_to(expected);max_error=maxf(max_error,error)
				if error>.00001:failures+=1
				var worldray:Vector3=latest.basis*ray
				var image:=Vector2(worldray.dot(image_basis.x),worldray.dot(image_basis.y))/worldray.dot(forward)*3
				if absf(image.length()-aperture.length())>.00001:failures+=1
				samples+=1
			if projection!=projection_before:failures+=1
		print("EYE ",eye," projected_center=",center_before," projection_unchanged=",projection==projection_before)
	print("NUMERICAL_ONLY samples=",samples," optical_refreshes_in_loop=0 world_origin=4800 center_ndc_tolerance=.00001 aperture_tolerance=.00001 direction_w=0")
	print("ASYMMETRIC_EYES center/radius roundtrip max_error=",max_error)
	gun.shutdown();gun.queue_free();actor.queue_free();await process_frame
	print("R19_SCOPE failures=",failures," gpu=false headset=false");quit(failures)

