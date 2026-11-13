extends SceneTree
var game:Node
var net:Node
var avatar:Node
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(name:String):
	avatar._process(.1)
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(output+"/"+name+".png");print("GHOST_CAPTURE_R13 ",name)
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
	net.host("Viewer",25602,"ghost");net.request("rules",{"map":"range","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0})
	while net.stage!="lobby" or not net.world_ready or game.network_bridge._preparing:await process_frame
	freeze(game);net.stage="match";net.players[1].team=0;net.players[2]=net.players[1].duplicate(true);net.players[2].name="ALLY · CLAUDE OPUS";net.players[2].style=6;net.players[2].inventory=[]
	var at:Vector3=game.player.global_position+Vector3(4,0,0)
	net.poses[2]={"root":Transform3D(Basis.IDENTITY,at),"head":Transform3D(Basis.IDENTITY,at+Vector3.UP*1.7),"left":Transform3D(Basis.IDENTITY,at+Vector3(-.3,1.0,-.08)),"right":Transform3D(Basis.IDENTITY,at+Vector3(.3,1.0,-.08)),"weapon":0,"holstered":false}
	avatar=load("res://scripts/net_avatar1000.gd").new();game.add_child(avatar);avatar.setup(2,net,game.network_bridge);game.network_bridge.avatars[2]=avatar
	# A split-color test board makes actual translucency visible in a native render.
	for i in 2:
		var board:=MeshInstance3D.new();var box:=BoxMesh.new();box.size=Vector3(1.6,2.5,.08);board.mesh=box;var mat:=StandardMaterial3D.new();mat.albedo_color=Color("709eae" if i==0 else "a36f51");mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;board.material_override=mat;game.add_child(board);board.global_position=at+Vector3((i-.5)*1.6,1.1,.8)
	game.weapon.hide();game.body_visual.hide();game.ui_layer.hide();game.combat_hud.hide()
	var camera:Camera3D=game.player.get_view_camera();camera.global_position=at+Vector3(0,1.55,-4.3);camera.look_at(at+Vector3.UP*1.3);camera.fov=62
	await shot("R13-01-ALIVE-REFERENCE")
	net.players[2].life="downed";net.players[2].health=0;net.players[2].shield=0
	await shot("R13-02-TEAMMATE-GHOST")
	var wall:=MeshInstance3D.new();var wall_mesh:=BoxMesh.new();wall_mesh.size=Vector3(2.2,2.2,.12);wall.mesh=wall_mesh;var wall_mat:=StandardMaterial3D.new();wall_mat.albedo_color=Color("29333d");wall_mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;wall.material_override=wall_mat;game.add_child(wall);wall.global_position=at+Vector3(0,1,-.6)
	net.players[2].life="alive";net.players[2].health=100;await shot("R13-08-ALIVE-BEHIND-WALL");net.players[2].life="downed";net.players[2].health=0;await shot("R13-07-GHOST-THROUGH-WALL")
	net.players[1].team=1;await shot("R13-09-OPPONENT-BEHIND-WALL");net.players[1].team=0;wall.hide()
	net.players[1].team=1;await shot("R13-03-OPPONENT-NO-GHOST");net.players[1].team=0
	net.players[2].life="eliminated";await shot("R13-04-ELIMINATED-TEAMMATE")
	for style in [3,4,5]:
		net.players[2].style=style;net.players[2].name="ALLY · "+["ASTRA","SOL","ARGON"][style-3];await shot("R13-STYLE-"+str(style)+"-GHOST")
	net.players[2].life="alive";net.players[2].health=50;net.players[2].shield=0;net.players[2].style=6;net.players[2].name="ALLY · CLAUDE OPUS"
	await shot("R13-05-REVIVED-RESTORED")
	net.players[1].life="downed";net.players[1].health=0;net.players[1].shield=0;game.network_bridge._sync_player();game.body_visual.hide();game.combat_hud.refresh(0);game.combat_hud.get_node("CompactMatchHUDR3")._process(0)
	await shot("R13-06-COMPACT-DEATH-NOTICE")
	game.network_bridge.leave_session();game._quitting=true;game.queue_free();await process_frame;quit()
