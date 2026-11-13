extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func snapshot(geometry:Node)->Dictionary:
	var result:Dictionary={}
	for view in geometry.find_children("*","MeshInstance3D",true,false):
		if view.mesh==null:continue
		var hash:=HashingContext.new();hash.start(HashingContext.HASH_SHA256);var bytes:PackedByteArray=view.mesh.get_faces().to_byte_array()
		if not bytes.is_empty():hash.update(bytes)
		result[str(geometry.get_path_to(view))]={"faces":hash.finish().hex_encode(),"box":view.mesh.get_aabb(),"material":view.material_override}
	return result
func native_sample(camera:Camera3D,path:String)->Dictionary:
	if DisplayServer.get_name()=="headless":return {}
	for i in 5:await process_frame
	await RenderingServer.frame_post_draw
	var result={"triangles":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"vram_bytes":Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)}
	get_root().get_texture().get_image().save_png(path)
	return result
func run():
	var index:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_lod_index_r16.json"))
	check(index.maps.size()==105,"LOD index covers105 maps")
	for slug in index.maps:
		var row:Dictionary=index.maps[slug]
		check(FileAccess.get_sha256(row.path)==row.sha256,slug+" LOD artifact hash")
		check(row.coarsest_triangles<=row.base_triangles,slug+" far detail never exceeds base")
		var data:Resource=load(row.path)
		for surfaces in data.get_meta("meshes").values():
			for surface in surfaces:
				var total:int=int(surface.indices) if int(surface.indices)>0 else int(surface.vertices)
				for size in surface.lods:
					var ids:PackedInt32Array=surface.lods[size]
					var valid:bool=size>0 and ids.size()%3==0 and ids.size()<total
					for id in ids:
						if id<0 or id>=int(surface.vertices):valid=false;break
					check(valid,slug+" safe reduced indices")
	game=load("res://scripts/main.gd").new();root.add_child(game)
	for slug in ["battlefield-paintball-gods-pitdevil","alfa-aglae-souza","deep-space-5-stand-alone-pro-yt"]:
		game.front_menu.selected_map="archive:"+slug;game._start_from_title()
		var deadline=Time.get_ticks_msec()+90000
		while (not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready) and Time.get_ticks_msec()<deadline:await process_frame
		var map:Node=game.active_sandbox_map
		check(map.map_ready,slug+" map load")
		game.player.set_physics_process(false)
		var before:Dictionary=snapshot(map._geometry);var collisions=map._geometry.find_children("*","CollisionShape3D",true,false).size()
		var meta:Dictionary=map._meta;var camera:Camera3D=game.player.desktop_camera
		var focus:Vector3=map.global_position+Vector3(meta.center[0],meta.center[1],meta.center[2])
		camera.global_position=focus+Vector3(120,140,180);camera.look_at(focus,Vector3.UP)
		var native_before:Dictionary=await native_sample(camera,OS.get_cmdline_user_args()[0]+"/NATIVE-R16-"+slug+"-before.png")
		var lods:Dictionary=preload("res://scripts/map_lods_r16.gd").apply(map)
		check(int(lods.meshes)>40,slug+" actual LOD coverage")
		var after:Dictionary=snapshot(map._geometry)
		check(before==after,slug+" full base triangles/bounds/materials exactly preserved")
		check(collisions==map._geometry.find_children("*","CollisionShape3D",true,false).size(),slug+" collision nodes unchanged")
		var native_after:Dictionary=await native_sample(camera,OS.get_cmdline_user_args()[0]+"/NATIVE-R16-"+slug+"-after.png")
		facts.append({"map":slug,"lods":lods,"native_before":native_before,"native_after":native_after})
		game.player.set_physics_process(true);game._return_home0992();await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RENDER-R16.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"All105LOD index safety/hashes; full-detail geometry/collision preservation on3maps; native PC counters if available, not Quest frame-rate acceptance."},"\t"));file.close()
	print("RENDER_R16 checks=",checks," failures=",failures.size())
	game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
