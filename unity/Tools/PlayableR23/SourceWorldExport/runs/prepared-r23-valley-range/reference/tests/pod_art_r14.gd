extends SceneTree
const Art=preload("res://scripts/launch_art_r12.gd")
var checks:int=0
var failures:Array=[]
var output:String
var scene:Node3D
var camera:Camera3D
var render_rows:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func cast(a:Vector3,b:Vector3)->Dictionary:
	return scene.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(a,b,1))
func shot(name:String,at:Vector3,target:Vector3):
	if DisplayServer.get_name()=="headless":return
	camera.global_position=at;camera.look_at(target)
	for i in 5:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+name+".png");print("POD_ART_CAPTURE ",name)
	render_rows.append({"view":name,"visible_draw_calls":RenderingServer.viewport_get_render_info(root.get_viewport_rid(),RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE,RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME),"visible_primitives":RenderingServer.viewport_get_render_info(root.get_viewport_rid(),RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE,RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME)})
func run():
	output=OS.get_cmdline_user_args()[0];scene=Node3D.new();root.add_child(scene)
	var environment:=WorldEnvironment.new();var env:=Environment.new();env.background_mode=Environment.BG_COLOR;env.background_color=Color("6f9fb8");env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;env.ambient_light_color=Color("a8c2d2");env.ambient_light_energy=.7;env.tonemap_mode=Environment.TONE_MAPPER_FILMIC;environment.environment=env;scene.add_child(environment)
	var light:=DirectionalLight3D.new();light.rotation_degrees=Vector3(-38,-30,0);light.light_energy=2.3;light.shadow_enabled=true;light.directional_shadow_max_distance=130;scene.add_child(light)
	camera=Camera3D.new();camera.current=true;camera.far=300;camera.fov=68;scene.add_child(camera)
	var deck:=MeshInstance3D.new();deck.mesh=Art.deck();scene.add_child(deck)
	var deck_body:=StaticBody3D.new();deck_body.collision_layer=1;scene.add_child(deck_body);var collider:=CollisionShape3D.new();var concave:=ConcavePolygonShape3D.new();concave.backface_collision=true;concave.set_faces(deck.mesh.get_faces());collider.shape=concave;deck_body.add_child(collider)
	var mm:=MultiMesh.new();mm.transform_format=MultiMesh.TRANSFORM_3D;mm.mesh=Art.cabin();mm.instance_count=12
	var pods:=MultiMeshInstance3D.new();pods.multimesh=mm;scene.add_child(pods)
	var bodies:Array=[]
	for i in 12:
		var transform:=Transform3D(Basis.IDENTITY,Vector3((i-5.5)*3.6,.14,-6));mm.set_instance_transform(i,transform)
		var body:Node3D=Art.pod_body(scene);body.transform=transform;bodies.append(body);Art.control(body,"LAUNCH")
	for i in 3:await physics_frame
	check(not root.use_xr,"XR remains disabled")
	check(Art.palette().size()==8,"eight shared opaque materials")
	check(Art.cabin()==Art.cabin() and Art.deck()==Art.deck(),"pod/deck geometry cached once")
	check(Art.cabin().get_surface_count()<=8 and Art.deck().get_surface_count()<=8,"maximum eight surfaces per shared mesh")
	check(mm.instance_count==12,"twelve instanced cabins")
	for i in 12:
		var x:float=(i-5.5)*3.6
		check(not cast(Vector3(x-1.5,1.80,-6),Vector3(x+1.5,1.80,-6)).size(),"both side window openings are empty slot "+str(i))
		check(not cast(Vector3(x,.9,-3.5),Vector3(x,.9,-6)).size(),"rear entrance is open slot "+str(i))
		check(not cast(Vector3(x,2.1,-3.5),Vector3(x,2.1,-6)).size(),"rear entrance has standing headroom slot "+str(i))
		check(cast(Vector3(x,1.80,-6),Vector3(x-1.5,.6,-6)).size()>0,"lower side wall collides slot "+str(i))
		var roof:Dictionary=cast(Vector3(x,1.80,-6),Vector3(x,4.5,-6))
		check(not roof.is_empty() and roof.position.y>2.55,"central cabin headroom slot "+str(i))
		check(cast(Vector3(x,3,3),Vector3(x,-1,3)).size()>0,"spawn aisle floor slot "+str(i))
		# Sample cabin bounding-volume corners through the entire initial rise/forward leg.
		var clear:bool=true
		for step in 10:
			var start:=Vector3(x,.14,-6)+Vector3(0,.5*step,-step)
			var end:=Vector3(x,.14,-6)+Vector3(0,.5*(step+1),-(step+1))
			for dx in [-1.45,0,1.45]:
				for dy in [1.7,3.55]:
					for dz in [-1.55,1.55]:
						var query:=PhysicsRayQueryParameters3D.create(start+Vector3(dx,dy,dz),end+Vector3(dx,dy,dz),1);query.exclude=bodies.map(func(b):return b.get_rid())
						var hit:Dictionary=scene.get_world_3d().direct_space_state.intersect_ray(query)
						if not hit.is_empty():
							if clear:print("ENVELOPE_HIT slot=",i," step=",step," at=",hit.position," delta=",Vector3(dx,dy,dz))
							clear=false
		check(clear,"first launch cabin envelope clears deck framework slot "+str(i))
	for side in [-1,1]:
		check(cast(Vector3(side*23.5,1.7,-4.7),Vector3(side*26,1.7,-4.7)).is_empty(),"open side glide exit "+str(side))
	await shot("R14-POD-01-OVERVIEW",Vector3(33,21,-27),Vector3(0,3,0))
	await shot("R14-POD-02-PLAYER-HEIGHT-DECK",Vector3(-4.5,1.8,6.1),Vector3(3,1.8,-6.3))
	await shot("R14-POD-03-RED-CABIN-WINDOWS",Vector3(3.7,2.4,-2.8),Vector3(1.8,1.65,-6))
	await shot("R14-POD-04-CABIN-INTERIOR",Vector3(1.8,1.8,-5.4),Vector3(1.8,1.6,-7.3))
	await shot("R14-POD-05-FRAMEWORK-AND-CANOPY",Vector3(0,1.8,-.7),Vector3(0,4.4,8.3))
	var result={"passed":failures.is_empty(),"checks":checks,"failures":failures,"pod_triangles":Art.cabin().get_faces().size()/3,"deck_triangles":Art.deck().get_faces().size()/3,"pod_surfaces":Art.cabin().get_surface_count(),"deck_surfaces":Art.deck().get_surface_count(),"pod_collision_shapes":bodies[0].get_child_count()-5,"shared_materials":Art.palette().size(),"cabins_per_deck":12,"render_views":render_rows,"scope":"Original isolated art scene, actual generated collision and shared meshes; XR off, no headset/flight acceptance"}
	var filename:String="POD-ART-R14-HEADLESS.json" if DisplayServer.get_name()=="headless" else "POD-ART-R14.json"
	var f:=FileAccess.open(output+"/"+filename,FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));print("POD_ART_R14 checks=",checks," failures=",failures.size());scene.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)

