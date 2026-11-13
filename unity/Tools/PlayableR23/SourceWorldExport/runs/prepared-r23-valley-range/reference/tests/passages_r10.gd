extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run():
	game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	for id in ["archive:alfa-aglae-souza","archive:afterlife-gods-alamo-hank","archive:classic-bigbox-vr"]:
		game.front_menu.selected_map=id
		if game.front_end_active:game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
		for i in 5:await physics_frame;await process_frame
		freeze(game);var map:Node=game.active_sandbox_map;check(map.map_ready,id+" READY")
		var candidates:Array=[];var glass_count=0;var door_count=0;var pane_tests=0;var door_tests=0;var sample_nodes:Array=[]
		for group in map.gameplay_r8._panels_r8.values():
			for panel in group:
				if panel.glass_r10:glass_count+=1
				else:door_count+=1
				var row:Dictionary=map._details_r8.panels[int(panel.key)];var normal:Vector3=preload("res://scripts/archive_semantics_r9.gd")._surface_normal(row.faces)
				if absf(normal.y)>.1 or panel.box.size.y<1.2 or maxf(panel.box.size.x,panel.box.size.z)<1.1:continue
				var center:Vector3=map.global_position+panel.box.get_center();var query:=PhysicsRayQueryParameters3D.create(center+normal*.35,center-normal*.35,1);var before:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(query)
				if before.get("collider")!=panel:continue
				if (panel.glass_r10 and pane_tests>=24) or (not panel.glass_r10 and door_tests>=12):continue
				candidates.append({"panel":panel,"center":center,"normal":normal,"glass":panel.glass_r10});sample_nodes.append(panel)
				if panel.glass_r10:
					pane_tests+=1;check(row.material is ShaderMaterial and row.material.shader.resource_path.ends_with("clear_glass_r10.gdshader"),id+" pane uses transparent material")
				else:door_tests+=1
		if id!="archive:afterlife-gods-alamo-hank":check(pane_tests>0,id+" sampled real blocking panes")
		else:check(glass_count==0,"Afterlife decorative glass-named letters/posts are not mistaken for pane openings")
		for candidate in candidates:
			candidate.panel.take_damage(1)
			var voices:Array=map.find_children("*","AudioStreamPlayer3D",false,false);var expected:String="glass_break_r8.wav" if candidate.glass else "wood_break_r10.wav"
			check(not voices.is_empty() and voices[-1].stream.resource_path.ends_with(expected),id+" distinct sound routing "+expected)
			check(not candidate.panel.view.visible and candidate.panel.collision_layer==0,id+" pane/door assembly disappears")
		for i in 3:await physics_frame
		for candidate in candidates:
			var normal:Vector3=candidate.normal;var right:Vector3=Vector3.UP.cross(normal).normalized()
			for offset in [Vector3.ZERO,right*.2,-right*.2,Vector3.UP*.3,Vector3.DOWN*.3]:
				var at:Vector3=candidate.center+offset;var after:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(at+normal*.35,at-normal*.35,1))
				if not after.is_empty():print("RESIDUAL_R10 ",id," key=",candidate.panel.key," glass=",candidate.glass," box=",candidate.panel.box," hit=",after.collider," at=",after.position)
				check(after.is_empty(),id+" actual aperture clear "+candidate.panel.key+" "+str(offset))
		facts.append({"map":id,"glass":glass_count,"doors":door_count,"glass_samples":pane_tests,"door_samples":door_tests,"prop_materials":map._details_r8.get("prop_materials_r10",{})});print("PASSAGES_R10 ",id," checks=",checks," failures=",failures.size())
		game._return_home0992();for i in 5:await process_frame
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/PASSAGES-R10.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"actual Main, real world collision ray fan through recognized pane/door apertures, transparent material and distinct audio routing; XR off"},"\t"));game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
