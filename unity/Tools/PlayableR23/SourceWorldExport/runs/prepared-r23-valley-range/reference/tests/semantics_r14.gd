extends SceneTree
const Semantic=preload("res://scripts/map_semantics_r14.gd")
var failures:Array[String]=[]
var report:Dictionary={}
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);push_error("SEMANTICS_R14 "+label)
func _initialize()->void:call_deferred("run")
func run()->void:
	var catalog:Dictionary=Semantic.catalog();var maps:Dictionary=catalog.get("maps",{});check(maps.size()==105,"all105 source maps covered")
	var totals:Dictionary={"warmup":0,"deathmatch":0,"jump":0,"health":0,"tilted":0};var unverified_frames=0
	for slug in maps:
		var row:Dictionary=maps[slug];check(str(row.get("cache_fingerprint","")).length()==64,"hash-bound "+slug)
		for kind in ["warmup","deathmatch","jump","health"]:
			totals[kind]+=row.get(kind,[]).size()
			for site in row.get(kind,[]):
				var center:Vector3=Semantic.vector(site.center);var normal:Vector3=Semantic.vector(site.normal)
				check(center.is_finite() and normal.is_finite() and absf(normal.length()-1)<.001,"finite unit normal "+slug)
				if kind=="jump":
					check(normal.y>=-.001,"launch never downward "+slug)
					if normal.y<.985:totals.tilted+=1
					if "fallback" in str(site.evidence):unverified_frames+=1
					if site.has("front_point"):check(normal.dot(Semantic.vector(site.front_point)-center)>.001,"vertical frame faces authored chevron offset "+slug)
	check(maps["alfa-aglae-souza"].warmup.size()==6,"exact user white ring assemblies")
	check(maps["alfa-aglae-souza"].deathmatch.size()==17,"exact user black ring/person assemblies including split chunk")
	check(maps["alfa-aglae-souza"].health.size()==98,"Alpha cross +green/black healing assemblies including split chunks")
	var tilted:Array=maps["alfa-aglae-souza"].jump.filter(func(row):return Semantic.vector(row.center).distance_to(Vector3(-543.4,39.3,-135.6))<2)
	check(tilted.size()==1,"unique native tilted Alpha frame")
	if tilted.size()==1:check(Semantic.vector(tilted[0].normal).distance_to(Vector3(.25,.258819,.933013))<.002,"physical face normal of75-degree Alpha pad")
	report={"totals":totals,"chevron_plane_fallbacks":unverified_frames,"offline_only":true,"headset_tested":false}
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;var semantic:Node=map.find_child("MapSemanticsR14",true,false)
	if semantic==null:semantic=Semantic.new();map.add_child(semantic);semantic.setup(map);await physics_frame
	semantic.set_physics_process(false)
	var warmup:Array=semantic.validated_spawns("warmup");var deathmatch:Array=semantic.validated_spawns("deathmatch")
	report.alpha_runtime={"warmup_valid":warmup.size(),"deathmatch_valid":deathmatch.size(),"visual_triangles":semantic.removed_visual_triangles,"collision_triangles":semantic.removed_collision_triangles,"jump":semantic.jump_pads.size(),"health":semantic.health_pads.size()}
	check(warmup.size()>=1,"Alpha warmup floor/headroom validation")
	check(deathmatch.size()>=1,"Alpha deathmatch floor/headroom validation")
	check(semantic.removed_visual_triangles>10000,"full native marker assemblies extracted")
	check(semantic.removed_collision_triangles>10000,"full native marker assemblies have no statue collision")
	semantic.set_markers_visible(false);check(not semantic.marker_visuals.visible,"host marker symbols hidden")
	semantic.set_markers_visible(true);check(semantic.marker_visuals.visible,"solo READY retains marker inspection")
	var at:Vector3=map.global_position+Vector3(202.412,52.557,-16.859)
	var query:=PhysicsRayQueryParameters3D.create(at+Vector3(0,1.6,-.8),at+Vector3(0,1.6,.8),1)
	check(map.get_world_3d().direct_space_state.intersect_ray(query).is_empty(),"black marker humanoid has no torso collision")
	var heal:Vector3=map.global_position+Vector3(-526.7505,48.7341,125.6655)
	check(semantic.healing_rate_at(heal+Vector3.UP*.1)==10,"native green cross pad supplies health rate")
	check(semantic.healing_rate_at(heal+Vector3.UP*2)==0,"heal cannot reach higher floor")
	check(semantic.healing_rate_at(heal+Vector3.RIGHT*2)==0,"heal pad radius bounded")
	var floor:Vector3=map._floor(heal+Vector3.UP*1.5,3.0)
	check(floor.is_finite() and floor.y<heal.y+.3,"floating gold cross has no collision")
	check(floor.is_finite() and semantic.healing_rate_at(floor+Vector3.UP*.12)==10,"walking on native health pad supplies healing")
	game.player.input_enabled=true
	var pad:Dictionary=semantic.jump_pads.filter(func(row):return (row.center-map.global_position).distance_to(Vector3(-543.4,39.3,-135.6))<2)[0]
	game.player.reset_motion();game.player.global_position=pad.center+pad.normal*.35
	var launched:bool=semantic.try_launch(game.player)
	check(launched,"tilted source pad contact launches player")
	check(game.player.velocity.dot(pad.normal)>40,"directional impulse follows true frame face")
	check(not semantic.try_launch(game.player),"same-pad cooldown prevents relaunch loop")
	var other:Dictionary=semantic.jump_pads.filter(func(row):return row.center.distance_to(pad.center)>5 and row.normal.y>.99)[0]
	game.player.global_position=other.center+other.normal*.3
	check(semantic.try_launch(game.player),"second pad chains during previous pad cooldown/ascent")
	report.failures=failures;var output:String=OS.get_cmdline_user_args()[0];FileAccess.open(output+"/semantics-r14-receipt.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("SEMANTICS_R14 ",JSON.stringify(report));game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
