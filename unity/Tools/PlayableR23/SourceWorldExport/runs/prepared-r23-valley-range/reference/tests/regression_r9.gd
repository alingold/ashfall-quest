extends SceneTree
var checks=0
var failures:Array=[]
var game:Node
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run():
	var output:String=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var front:Node=game.front_menu;var player:Node=game.player;var gallery:Node=front.gallery
	check(not root.use_xr,"XR remains off")
	check(front.lobby.pedestals.size()==1 and front.lobby.avatars.size()==1,"one podium and one avatar")
	front.page="loadout";gallery.show_item(true,6);front._place_lobby_board();var board_pose:Transform3D=front.board.global_transform;var avatar:Node3D=front.lobby.avatars[0];var avatar_pose:Transform3D=avatar.global_transform
	check(avatar.scale.is_equal_approx(Vector3.ONE),"character is full size")
	check(gallery.prop==avatar,"selector uses the one world character")
	player.desktop_camera.position+=Vector3(2,1,3);player.desktop_camera.rotation=Vector3(.3,1.2,.2);front._place_lobby_board()
	check(front.board.global_transform.is_equal_approx(board_pose),"main board unaffected by moved/turned head")
	gallery._process(1);check(avatar.global_transform.is_equal_approx(avatar_pose),"head motion cannot move character dock")
	front.lobby._process(1);check(absf(avatar.rotation.y-avatar_pose.basis.get_euler().y)>.01,"world character rotates slowly without grabbing")
	check(avatar.global_position.is_equal_approx(avatar_pose.origin),"rotation retains world position")
	gallery.close();front.page="lobby";front.refresh_page();check(front._net_label1000.text.is_empty(),"obsolete main-menu instructions removed")
	var paddles:Node=game.revive_paddles
	check(paddles._label.no_depth_test,"revive label renders above world")
	for mesh in paddles._meter.find_children("*","MeshInstance3D",true,false):check(mesh.material_override.no_depth_test,"revive meter geometry renders above world")
	var reference=preload("res://scripts/reference_audio_099.gd")
	for kind in ["revive_facing","revive_ready_full0992"]:
		var stream:AudioStreamWAV=reference.generic(kind,0);check(stream!=null and stream.loop_mode==AudioStreamWAV.LOOP_DISABLED,kind+" is a real one-shot")
	paddles._facing=true;paddles.charge_ready=false;paddles._update_reference099(Vector3.ZERO);check(paddles._facing_cue_played_r9,"charging cue begins once")
	paddles._reference099.stop();paddles._update_reference099(Vector3.ZERO);check(not paddles._reference099.playing,"completed charge cue is not replayed every frame")
	paddles._facing=false;paddles._update_reference099(Vector3.ZERO);check(not paddles._facing_cue_played_r9,"new charge episode can re-arm cue")
	var rarity=preload("res://scripts/weapon_rarity_r6.gd")
	for catalog in [14,15,16,26,28,29]:
		rarity.apply(game.weapon.weapons[catalog],catalog,5);check(rarity.level(game.weapon.weapons[catalog])==1,"non-tiered level fixed "+str(catalog));check(not game.weapon.get_weapon_label(catalog).contains("★"),"non-tiered inventory has no stars "+str(catalog))
	game.queue_free();await process_frame;await physics_frame
	# All-map proxy works against a thin, hollow collision surface, without a Cyber Bird group.
	var actor:Node=load("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);freeze(actor);actor.desktop_camera.position=Vector3(0,1.65,0)
	var wall:=StaticBody3D.new();wall.add_to_group("climbable");root.add_child(wall);var shape:=CollisionShape3D.new();var hollow:=ConcavePolygonShape3D.new();hollow.backface_collision=true;hollow.set_faces(PackedVector3Array([Vector3(-2,0,-.5),Vector3(2,0,-.5),Vector3(2,3,-.5),Vector3(-2,0,-.5),Vector3(2,3,-.5),Vector3(-2,3,-.5)]));shape.shape=hollow;wall.add_child(shape)
	await physics_frame;await physics_frame
	for x in [-.8,0,.8]:
		actor.left_hand.global_position=Vector3(x,1.4,-1.1);var resolved:Vector3=actor.hand_walls_r2.resolve(actor,actor.left_hand)
		check(resolved.z>-.5,"thin wall blocks free hand "+str(x));check(not actor.find_grab_surface(resolved).is_empty(),"blocked hand remains grabbable "+str(x))
	actor.queue_free();wall.queue_free();await process_frame
	var receipt={"passed":failures.is_empty(),"checks":checks,"failures":failures,"scope":"real Main/menu, audio streams and cue lifecycle, rarity and real hollow concave physics. XR off; no headset acceptance."};var file=FileAccess.open(output+"/REGRESSION-R9.json",FileAccess.WRITE);file.store_string(JSON.stringify(receipt,"\t"));print("REGRESSION_R9 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
