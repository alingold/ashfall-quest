extends SceneTree
var game:Node
var output:String
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func shot(title:String):
	for i in 4:await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(output+"/"+title+".png")
func run():
	output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var rows:Array=[]
	for slug in ["banana-land-gods-pitdevil","bamboo-gods-pitdevil","atrocious-gods-trashpandakiller","battlefield-paintball-gods-pitdevil","classic-bigbox-vr"]:
		game.front_menu.selected_map="archive:"+slug
		if game.front_end_active:game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,"archive:"+slug)
		for i in 8:await physics_frame
		freeze(game);game.combat_hud.hide();game.ui_layer.hide();game.vr_label.hide();game.vr_cue.hide();game.weapon.hide()
		var map:Node=game.active_sandbox_map;var camera:Camera3D=game.player.get_view_camera();var center:Vector3=map.global_position+Vector3(map._meta.center[0],map._meta.center[1],map._meta.center[2])
		camera.global_position=center+Vector3(0,450,380);camera.look_at(center);camera.far=2500;await shot(slug+"-overview")
		camera.global_position=map.spawn+Vector3.UP*1.65;camera.look_at(center+Vector3.UP*1.6);await shot(slug+"-spawn")
		var mats:Array=[]
		for mesh in map._geometry.find_children("*","MeshInstance3D",true,false):
			mats.append({"name":str(mesh.name),"surfaces":mesh.mesh.get_surface_count(),"aabb":str(mesh.mesh.get_aabb()),"material":str(mesh.material_override.resource_name) if mesh.material_override!=null else "none"})
		var spawns:Array=[];for at in map.spawn_points:spawns.append(str(at-map.global_position))
		rows.append({"slug":slug,"spawns":spawns,"spawn":str(map.spawn-map.global_position),"ready":map.map_ready,"materials":mats,"mip_stats":map.statistics.get("material_mips1004",{})})
		print("INSPECT_R8 ",slug," ready=",map.map_ready," spawns=",spawns.size())
	var f=FileAccess.open(output+"/MAP-INSPECTION-R8.json",FileAccess.WRITE);f.store_string(JSON.stringify(rows,"\t"));f.close();game.queue_free();await process_frame;quit()
