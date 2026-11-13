extends SceneTree
var checks=0
var failures:Array=[]
var facts:Array=[]
var game:Node
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func settle():
	for i in 5:await physics_frame;await process_frame
func run():
	var output:String=OS.get_cmdline_user_args()[0];var all_maps:bool=OS.get_cmdline_user_args().has("--all-r9");game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var catalog=preload("res://scripts/archive_catalog1003.gd");var ids:Array=[]
	for row in catalog.rows():
		var only:String=""
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--only-map-r9="):only=arg.trim_prefix("--only-map-r9=")
		if not only.is_empty() and row.slug!=only:continue
		if not only.is_empty() or all_maps or row.slug in ["atrocious-gods-trashpandakiller","poppy-gods-poplightning","phoenix-bigbox-vr","classic-bigbox-vr","melipop-gods-poplightning","vortex-sd-stumpdawg","magnum-chaosvr","magnum-3-chaosvr"]:ids.append(str(row.id))
	check(not ids.is_empty(),"requested map set is not empty")
	for id in ids:
		game.front_menu.selected_map=id
		if game.front_end_active:game._start_from_title()
		else:preload("res://scripts/cyber_bird_registration.gd").select(game,id)
		await settle();freeze(game);var map:Node=game.active_sandbox_map
		check(map!=null and map.map_ready and map.load_error.is_empty(),id+" READY with R9 adapter")
		if map==null or not map.map_ready:continue
		check(map.safe_spawn_r8(map.warmup_spawn_r8),id+" supported warmup spawn")
		check(not map.spawn_points.is_empty(),id+" nonempty match sites")
		for at in map.spawn_points:check(map.safe_spawn_r8(at),id+" grounded match site")
		var net:Node=game.network_bridge.net;net.rules.map=id;net.zone.clear()
		var players={1:{"team":0},2:{"team":1}}
		for player in [1,2]:check(map.safe_spawn_r8(game.network_bridge._spawn_point(player,players,net.rules)),id+" supported unzoned match spawn")
		var info:Dictionary=preload("res://scripts/map_catalog1000.gd").info(id);net.zone={"next":Rect2(info.min,info.max-info.min)}
		for player in [1,2]:check(map.safe_spawn_r8(game.network_bridge._spawn_point(player,players,net.rules)),id+" supported zoned match spawn")
		net.zone.clear();check(game.player.get_view_camera().far>=2000,id+" whole-map far plane")
		var play:Node=map.gameplay_r8;check(play.boards.is_empty(),id+" no R8 floating scoreboard")
		var doors=0;var panes=0;var assemblies=0;var tested_door=false;var tested_glass=false
		for row in map._details_r8.panels:
			if row.glass:panes+=1
			else:doors+=1
			if not row.get("attachments",[]).is_empty():assemblies+=1
		for group in play._panels_r8.values():
			for panel in group:
				var row:Dictionary=map._details_r8.panels[int(panel.key)];var glass:bool=row.glass
				if (glass and tested_glass) or (not glass and tested_door):continue
				game.player.input_enabled=true;game.player.global_position=map.global_position+panel.box.get_center()-Vector3.UP*.9;panel._physics_process(.2)
				check(panel._broken and panel.collision_layer==0 and not panel.view.is_visible_in_tree(),id+" walk-through removes door/window assembly and collision")
				panel._apply(false)
				if glass:tested_glass=true
				else:tested_door=true
		# Check actual world collision through an assembled leaf, beyond merely hiding its node.
		var aperture_tests=0;var open_frames=0;var backed_candidates=0
		for group in play._panels_r8.values():
			for panel in group:
				var row:Dictionary=map._details_r8.panels[int(panel.key)]
				if row.glass or row.faces.size()!=30 or aperture_tests>=1:continue
				var normal:Vector3=preload("res://scripts/archive_semantics_r9.gd")._surface_normal(row.faces);var center:Vector3=map.global_position+panel.box.get_center();var right:Vector3=Vector3.UP.cross(normal).normalized()
				await physics_frame;await physics_frame
				var before:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(center+normal*.4,center-normal*.4,1))
				# Some imported frames have an already-open aperture and no leaf in their source.
				if before.is_empty():open_frames+=1;continue
				if before.get("collider")!=panel:backed_candidates+=1;continue
				check(before.get("collider")==panel,id+" intact door leaf blocks passage")
				panel.take_damage(1);await physics_frame;await physics_frame
				for offset in [Vector3.ZERO,right*.2,-right*.2,Vector3.UP*.4,Vector3.DOWN*.4]:
					var at:Vector3=center+offset;var after:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(at+normal*.4,at-normal*.4,1))
					if not after.is_empty():print("DOOR_RESIDUAL_R9 ",id," target=",panel.key," box=",panel.box," hit=",after.get("collider")," at=",after.get("position")," other_box=",after.collider.get("box"))
					check(after.is_empty(),id+" destroyed door leaves an actual aperture "+str(offset))
				aperture_tests+=1
		for i in mini(3,play.pads.size()):
			game.player.global_position=play.pads[i]+Vector3.UP*.1;game.player.velocity=Vector3.ZERO;game.player.input_enabled=true;play._launch_cooldown=0;play._physics_process(.01);check(game.player.velocity.y==30,id+" source launch pad "+str(i))
		if map.statistics.get("replacement_jump_pads_r9",0)>0:
			for at in play.pads:
				check(map.safe_spawn_r8(at+Vector3.UP*.12),id+" replacement pad has real floor and headroom")
				var clearance:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(at+Vector3.UP*.3,at+Vector3.UP*25,1))
				check(clearance.is_empty(),id+" replacement pad has overhead launch clearance")
				var full_support=true
				for edge in 8:
					var rim:Vector3=at+Vector3(cos(TAU*edge/8)*1.8,0,sin(TAU*edge/8)*1.8);var floor:Vector3=map._floor(rim+Vector3.UP*1.5,3)
					if not floor.is_finite() or absf(floor.y-at.y)>.06:full_support=false
				check(full_support,id+" full octagon footprint is supported on flat geometry")
		if id=="archive:phoenix-bigbox-vr":check(play.pads.size()==12 and map.statistics.get("replacement_jump_pads_r9",0)==12,"Phoenix twelve explicitly new orange octagonal launch pads, no crane triggers")
		if id=="archive:vortex-sd-stumpdawg":check(map._details_r8.panels.is_empty(),"Vortex decorative glass texture is not a breakable window")
		if id=="archive:poppy-gods-poplightning":
			check(map.warmup_spawn_r8.y>100 and map.warmup_spawn_r8.y<104,"Poppy actual separate source lobby platform")
			for at in map.spawn_points:check(at.y<90,"Poppy match excludes lobby platform")
		if id=="archive:melipop-gods-poplightning":
			check(map.get_node_or_null("AuthoredWorldScoreR9")!=null and map.get_node("AuthoredWorldScoreR9").faces.size()==4,"Melly Pop source megatron has four bound faces")
			check(play._weapon_boards_r9.size()==2,"Melly Pop two source weapon boards found");check(play.display_ids_r9.size()==game.weapon.ACTIVE_WEAPON_INDICES.size()*2,"Melly Pop one of every weapon per board")
		if map.magnum_only_r9():
			check(preload("res://scripts/net_rules1000.gd").starting_catalog(net.rules)==[7],id+" authoritative Magnum-only loadout")
			for index in game.weapon_inventory.pickups:check(game.weapon.catalog_index(index)==7,id+" only Magnum ground drops")
			game.player.global_position=map.center_respawn_r9-Vector3.UP*40;game.player.velocity=Vector3.DOWN*20;map._hazard_clock=.2;map._physics_process(.2);check(game.player.global_position.is_equal_approx(map.center_respawn_r9) and game.player.velocity.is_zero_approx(),id+" solo fall resets above center platform")
		facts.append({"map":id,"warmup":str(map.warmup_spawn_r8-map.global_position),"match_sites":map.spawn_points.size(),"doors":doors,"glass":panes,"panel_assemblies":assemblies,"door_aperture_tests":aperture_tests,"already_open_frame_candidates":open_frames,"wall_occluded_candidates":backed_candidates,"pads":play.pads.size(),"weapon_cache_sites":play.sites.size(),"weapon_board_items":play.display_ids_r9.size(),"score_faces":map._details_r8.get("score_faces_r9",[]).size()})
		var weak:WeakRef=weakref(map);game._return_home0992();await settle();freeze(game);check(weak.get_ref()==null,id+" unload releases map");print("LIBRARY_R9 ",id," checks=",checks," failures=",failures.size())
	var receipt={"passed":failures.is_empty(),"checks":checks,"failures":failures,"maps":facts,"all_maps":all_maps,"xr":"off","headset_tested":false,"scope":"actual Main READY, shared map adapters and grounded spawn providers; walk-trigger panel collision, pad launch, exact known sockets, fall reset and weak-reference unload. Geometry recognition does not establish original missing gameplay intent."};var file=FileAccess.open(output+("/LIBRARY-ALL-R9.json" if all_maps else "/LIBRARY-FOCUSED-R9.json"),FileAccess.WRITE);file.store_string(JSON.stringify(receipt,"\t"));print("LIBRARY_GAMEPLAY_R9 checks=",checks," failures=",failures.size());game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
