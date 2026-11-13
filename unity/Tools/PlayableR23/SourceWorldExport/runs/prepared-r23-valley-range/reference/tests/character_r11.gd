extends SceneTree
const Kit = preload("res://scripts/gemini_player_models_099.gd")
const Lobby = preload("res://scripts/lobby_map_091.gd")
const Body = preload("res://scripts/player_body.gd")
class Actor extends CharacterBody3D:
	var xr_enabled := true
	var tracking_available := true
	var input_enabled := true
	var left_hand := Node3D.new()
	var right_hand := Node3D.new()
	var view := Camera3D.new()
	func get_view_camera() -> Camera3D: return view
var failures: Array[String] = []
var checks := 0
var output: String
var stage: SubViewport
var camera: Camera3D
var receipts: Dictionary = {}
func _initialize(): call_deferred("run")
func check(ok: bool, label: String):
	checks += 1
	if not ok: failures.append(label); push_error(label)
func freeze(node: Node):
	node.set_process(false); node.set_physics_process(false)
	for child in node.get_children(): freeze(child)
func inside(mesh: Mesh, point: Vector3) -> bool:
	# Odd ray intersection count on closed garment volumes. Hardware islands add
	# pairs of crossings; the shared joint center must remain inside the sleeve.
	var direction := Vector3(.733, .417, .535).normalized()
	var faces := mesh.get_faces()
	var hits := 0
	for i in range(0, faces.size(), 3):
		var e1: Vector3 = faces[i+1]-faces[i]
		var e2: Vector3 = faces[i+2]-faces[i]
		var p: Vector3 = direction.cross(e2)
		var det: float = e1.dot(p)
		if absf(det)<.00000001: continue
		var t: Vector3 = point-faces[i]
		var u: float = t.dot(p)/det
		if u<0 or u>1: continue
		var q: Vector3 = t.cross(e1)
		var v: float = direction.dot(q)/det
		if v<0 or u+v>1: continue
		if e2.dot(q)/det>.000001: hits+=1
	return hits%2==1
func shot(name: String):
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	stage.get_texture().get_image().save_png(output.path_join(name+".png"))
	print("CHARACTER_SHOT ",name)
func view(at: Vector3, target: Vector3, fov: float = 40):
	camera.position=at; camera.look_at(target,Vector3.UP); camera.fov=fov
func environment():
	stage=SubViewport.new();stage.size=Vector2i(1600,1000);stage.own_world_3d=true
	stage.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(stage)
	var world_env:=WorldEnvironment.new();var env:=Environment.new();var sky:=Sky.new()
	var sm:=ProceduralSkyMaterial.new();sm.sky_top_color=Color("4f78b8");sm.sky_horizon_color=Color("b9c9da");sky.sky_material=sm
	env.sky=sky;env.background_mode=Environment.BG_SKY;env.ambient_light_source=Environment.AMBIENT_SOURCE_SKY;env.ambient_light_energy=.9
	env.tonemap_mode=Environment.TONE_MAPPER_FILMIC;world_env.environment=env;stage.add_child(world_env)
	var sun:=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-40,-30,0);sun.light_energy=1.4;sun.shadow_enabled=true;stage.add_child(sun)
	for at in [Vector3(0,3,-3),Vector3(4,3,-2)]:
		var fill:=OmniLight3D.new();fill.position=at;fill.light_energy=2;fill.omni_range=10;stage.add_child(fill)
	var floor_mesh:=MeshInstance3D.new();var box:=BoxMesh.new();box.size=Vector3(20,.1,20);floor_mesh.mesh=box;floor_mesh.position.y=-.05
	var mat:=StandardMaterial3D.new();mat.albedo_color=Color("3a4350");mat.roughness=.8;floor_mesh.material_override=mat;stage.add_child(floor_mesh)
	camera=Camera3D.new();stage.add_child(camera);camera.current=true
func run():
	var args:=OS.get_cmdline_user_args();output=args[0];DirAccess.make_dir_recursive_absolute(output)
	check(Kit.STYLE_KEYS.size()==7 and Kit.STYLE_KEYS[6]=="claude","existing seven styles and Opus slot preserved")
	# Real main-menu selector in the shipped game, before isolated close-ups.
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	game.front_menu.page="loadout";game.front_menu.gallery.show_item(true,6);game.front_menu.refresh_page()
	for i in 6: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(output.path_join("R11-00-ACTUAL-SKIN-SELECTOR.png"))
	game._quitting=true;game.queue_free();await process_frame
	environment()
	var expected: Array[String]=["Jacket","ChestPlate","Belt","AvatarHead"]
	for side in ["L","R"]:
		for part in ["UpperArm","Forearm","Thigh","Shin","Boot"]:expected.append(side+part)
	for light in [false,true]:
		ProjectSettings.set_setting("ashfall/lightweight_models",light)
		var label:String="light" if light else "pcvr"
		var meshes:Dictionary=Kit._load_kit_meshes(6)
		check(meshes.size()==14,label+" fourteen named pieces")
		for key in expected: check(meshes.has(key),label+" mesh "+key)
		if meshes.size()!=14:continue
		var triangles:=0
		for mesh:Mesh in meshes.values():
			triangles+=mesh.get_faces().size()/3
			check(mesh.get_aabb().size.is_finite(),label+" finite bounds")
		check(triangles==(22444 if light else 62042),label+" polygon budget retained")
		var head_box:AABB=meshes.AvatarHead.get_aabb()
		check(absf(head_box.position.z+.338)<.001 and absf(head_box.end.z-.327)<.001,label+" exported head includes 3 cm forward shift")
		var hero:Node3D=Lobby.make_avatar(6);stage.add_child(hero)
		var gloves:=0
		for child in hero.get_children():
			if "skeleton" in child and "side" in child:
				gloves+=1
				for mesh in child.find_children("*","MeshInstance3D",true,false):check(mesh.material_override==Kit.glove_material(6),label+" matched glove material")
		check(gloves==2,label+" both gloves attached")
		for spec in [["front",Vector3(0,1.25,-3.1)],["side",Vector3(3.1,1.25,0)],["back",Vector3(0,1.25,3.1)]]:
			view(spec[1],Vector3(0,1,0));await shot("R11-"+label+"-"+spec[0])
		view(Vector3(1,1.65,-1.35),Vector3(0,1.63,0),36);await shot("R11-"+label+"-head-neck")
		var actor:=Actor.new();stage.add_child(actor);actor.position=Vector3(3,0,0)
		actor.add_child(actor.view);actor.view.position=Vector3(0,1.65,0);actor.view.current=false
		actor.add_child(actor.left_hand);actor.add_child(actor.right_hand)
		for i in 2:
			var hand_node:Node3D=actor.left_hand if i==0 else actor.right_hand
			var wrapper:=Node3D.new();wrapper.name="ArticulatedGlove";hand_node.add_child(wrapper)
			wrapper.basis=preload("res://scripts/weapon.gd").corrected_grip_transform(Transform3D.IDENTITY).basis
			var glove:=preload("res://scripts/grip_hand_086.gd").new();wrapper.add_child(glove);glove.setup(i==0);Kit.style_gloves(glove,6)
		var body:Node3D=Body.new();stage.add_child(body);body.setup(actor);Kit.apply_style(body,6);body.set_process(false)
		Kit.remote_head(actor.view,6)
		check(actor.view.position==Vector3(0,1.65,0),label+" tracked eye position unchanged")
		var poses={"relaxed":Vector3(.26,1.0,-.04),"aim":Vector3(.29,1.19,-.4),"deep-bend":Vector3(.18,1.34,-.1),"extended":Vector3(.6,1.3,-.1)}
		for pose:String in poses:
			var hand:Vector3=poses[pose]
			actor.left_hand.global_position=actor.position+Vector3(-hand.x,hand.y,hand.z)
			actor.right_hand.global_position=actor.position+hand
			body.update_pose(0,true)
			for i in 2:
				var side:String="L" if i==0 else "R"
				for part in ["UpperArm","Forearm"]:
					var sleeve:MeshInstance3D=body.pieces[side+part]
					check(sleeve.global_transform.is_finite(),label+" "+pose+" finite "+side+part)
					check(inside(sleeve.mesh,sleeve.to_local(body.elbows[i])),label+" "+pose+" elbow center inside "+side+part)
			view(Vector3(4.4,1.5,-1.8),Vector3(3,1.28,0),42);await shot("R11-"+label+"-pose-"+pose)
			if pose=="aim" or pose=="deep-bend":
				view(body.elbows[0]+Vector3(-.45,.08,-.45),body.elbows[0],42);await shot("R11-"+label+"-left-elbow-"+pose)
		receipts[label]={"triangles":triangles,"meshes":meshes.size(),"gloves":gloves,"pose_count":poses.size()}
		hero.free();body.free();actor.free()
	var content:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/network_content1000.json"))
	var receipt={"passed":failures.is_empty(),"checks":checks,"failures":failures,"tiers":receipts,"head_forward_metres":.03,"content_id":content.content_id,"xr":false,"scope":"actual game selector plus real kit loader, body IK, remote head and elbow volume checks"}
	var file:=FileAccess.open(output.path_join("CHARACTER-R11.json"),FileAccess.WRITE);file.store_string(JSON.stringify(receipt,"  "))
	print("CHARACTER_R11 ",JSON.stringify(receipt));quit(0 if failures.is_empty() else 1)
