extends SceneTree
var game:Node
func _initialize():call_deferred("run")
func run():
	var slug:String="alfa-aglae-souza"
	for value in OS.get_cmdline_user_args():
		if value.begins_with("--r18-map="):slug=value.trim_prefix("--r18-map=")
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:"+slug;game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	var rows:Array=[];var total:int=0;var inflated:int=0;var inside_changed:int=0
	var eye:Vector3=game.player.global_position+Vector3.UP*1.7
	for view in map._geometry.find_children("*","MeshInstance3D",true,false):
		if not view.mesh is ArrayMesh:continue
		var found:bool=false;var used:AABB;var triangles:int=0;var references:int=0
		for s in view.mesh.get_surface_count():
			if view.mesh.surface_get_primitive_type(s)!=Mesh.PRIMITIVE_TRIANGLES:continue
			var arrays:Array=view.mesh.surface_get_arrays(s);var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
			var ids:PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array()
			if ids.is_empty():ids=PackedInt32Array(range(vertices.size()))
			triangles+=ids.size()/3
			for id in ids:
				assert(id>=0 and id<vertices.size())
				if not found:used=AABB(vertices[id],Vector3.ZERO);found=true
				else:used=used.expand(vertices[id])
				references+=1
		if not found:continue
		var old:AABB=view.mesh.get_aabb();var ratio:float=old.get_longest_axis_size()/maxf(.001,used.get_longest_axis_size())
		var old_world:AABB=view.global_transform*old;var used_world:AABB=view.global_transform*used.grow(.05)
		var old_d:float=eye.distance_to(eye.clamp(old_world.position,old_world.end));var used_d:float=eye.distance_to(eye.clamp(used_world.position,used_world.end))
		total+=triangles
		if ratio>1.05:inflated+=triangles
		if old_d<.01 and used_d>1:inside_changed+=triangles
		var material_rows:Array=[]
		for surface in view.mesh.get_surface_count():
			var material:Material=view.get_active_material(surface)
			var info:Dictionary={"class":material.get_class() if material!=null else "none"}
			if material is BaseMaterial3D:info.merge({"transparency":material.transparency,"billboard_mode":material.billboard_mode,"grow":material.grow,"grow_amount":material.grow_amount})
			material_rows.append(info)
		rows.append({"materials":material_rows,"path":str(map._geometry.get_path_to(view)),"triangles":triangles,"extent_ratio":ratio,"old_aabb":str(old),"referenced_aabb":str(used),"old_nearest_m":old_d,"used_nearest_padded_m":used_d,"references_checked":references})
	rows.sort_custom(func(a,b):return a.extent_ratio>b.extent_ratio)
	var report:Dictionary={"map":slug,"triangles":total,"triangles_extent_inflated_over_5pct":inflated,"triangles_inside_to_over_1m":inside_changed,"rows":rows,"limits":"Read-only referenced base vertices, no safe material eligibility or shader-displacement analysis yet. No production bounds change."}
	var file:=FileAccess.open(OS.get_cmdline_user_args()[0]+"/R19-USED-BOUNDS-"+slug+".json",FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("USED_BOUNDS ",slug," triangles=",total," inflated_5pct=",inflated," inside_to_over_1m=",inside_changed)
	game._quitting=true;game.queue_free();await process_frame;quit()
