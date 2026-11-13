extends SceneTree
const Semantic=preload("res://scripts/map_semantics_r14.gd")
const Archive=preload("res://scripts/archive_catalog1003.gd")
var game:Node
var results:Dictionary={}
func _initialize()->void:call_deferred("run")
func reason(map:Node,at:Vector3)->Dictionary:
	var hit:Vector3=map._floor(at+Vector3.UP*.65,2.0)
	var longer:Vector3=map._floor(at+Vector3.UP*1.5,8)
	var row:Dictionary={"hint":str(at-map.global_position),"floor":str(hit-map.global_position) if hit.is_finite() else "none","long_floor":str(longer-map.global_position) if longer.is_finite() else "none"}
	if hit.is_finite():
		row.outside=map._outside(hit);row.delta=hit.y-at.y;row.room=map._room(hit);row.safe=map.safe_spawn_r8(hit+Vector3.UP*.12)
		var capsule:=CapsuleShape3D.new();capsule.radius=.3;capsule.height=1.8
		var query:=PhysicsShapeQueryParameters3D.new();query.shape=capsule;query.transform=Transform3D(Basis.IDENTITY,hit+Vector3.UP*.96);query.collision_mask=1
		var obstructions:Array=[]
		for block in map.get_world_3d().direct_space_state.intersect_shape(query,8):obstructions.append(str(block.collider.get_path()))
		row.obstructions=obstructions
	return row
func run()->void:
	game=load("res://scripts/main.gd").new();root.add_child(game)
	var limit_alpha:bool=OS.get_cmdline_user_args().has("--alpha-only")
	var only_map=""
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--semantic-map="):only_map=argument.trim_prefix("--semantic-map=")
	for entry in Archive.rows():
		if limit_alpha and str(entry.slug)!="alfa-aglae-souza":continue
		if not only_map.is_empty() and str(entry.slug)!=only_map:continue
		var row:Dictionary=Semantic.catalog().maps.get(str(entry.slug),{})
		var started=Time.get_ticks_msec()
		var map:Node=load("res://scripts/archive_map1003.gd").new();map.map_id=entry.id;map._meta=Archive.manifest(entry.id);map._game=game;map.position=Vector3(0,0,4800);game.add_child(map)
		var resource:Resource=load("res://local_archive_r10/"+str(entry.slug)+".res");map._geometry=resource.geometry.instantiate();map.add_child(map._geometry)
		for body in map._geometry.find_children("*","StaticBody3D",true,false):
			if not body.is_in_group("climbable"):map._floor_exclusions.append(body.get_rid())
		for floor_box in resource.details.get("spawn_floors_r9",[]):
			var body:=StaticBody3D.new();body.collision_layer=1;body.collision_mask=0;body.add_to_group("climbable");map.add_child(body)
			var shape:=CollisionShape3D.new();var box:=BoxShape3D.new();box.size=Vector3(floor_box.size.x,maxf(.12,floor_box.size.y),floor_box.size.z);shape.shape=box;body.add_child(shape);body.position=floor_box.get_center()
		var semantic:Node=Semantic.new();map.add_child(semantic);semantic.setup(map);semantic.set_physics_process(false)
		await physics_frame
		await physics_frame
		var details:Dictionary={"warmup":[],"deathmatch":[],"warmup_valid":semantic.validated_spawns("warmup").size(),"deathmatch_valid":semantic.validated_spawns("deathmatch").size(),"marker_triangles":semantic.removed_visual_triangles,"collision_triangles":semantic.removed_collision_triangles,"elapsed_ms":Time.get_ticks_msec()-started}
		for kind in ["warmup","deathmatch"]:
			for point in row.get(kind,[]):details[kind].append(reason(map,map.global_position+Semantic.vector(point.center)))
		if true:
			var heal_checks:Array=[]
			for pad in semantic.health_pads:
				var floor:Vector3=map._floor(pad.center+Vector3.UP*1.5,8)
				var check:Dictionary={"center":str(pad.center-map.global_position),"normal":str(pad.normal),"floor":str(floor-map.global_position) if floor.is_finite() else "none","face_rate":semantic.healing_rate_at(pad.center+pad.normal*.12),"floor_rate":semantic.healing_rate_at(floor+Vector3.UP*.12) if floor.is_finite() else 0}
				var query:=PhysicsRayQueryParameters3D.create(pad.center+pad.normal*.2,pad.center+pad.normal*.24,1);var hit:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(query)
				check.face_blocker=str(hit.collider.get_path()) if not hit.is_empty() else "none"
				if check.floor_rate==0:
					var down:=PhysicsRayQueryParameters3D.create(pad.center+Vector3.UP*1.5,pad.center-Vector3.UP*6.5,1);var block:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(down)
					check.floor_blocker=str(block.collider.get_path()) if not block.is_empty() else "none"
					check.floor_normal=str(block.normal) if not block.is_empty() else "none"
					if not only_map.is_empty() and not block.is_empty() and floor.is_finite() and floor.y-pad.center.y>.9:
						var components:Array=[];var cross_at:Vector3=pad.center-map.global_position+Vector3.UP*.8
						for view in map._geometry.find_children("*","MeshInstance3D",true,false):
							if view.material_override==null or view.material_override.resource_name!="vc" or view.mesh==null:continue
							if not view.mesh.get_aabb().grow(.2).has_point(cross_at):continue
							var groups:Dictionary=preload("res://scripts/archive_semantics_r8.gd")._components(view.mesh)
							for part in groups.rows.values():
								if part.box.get_center().distance_to(cross_at)<.25:
									var relative:Transform3D=map._geometry.global_transform.affine_inverse()*view.global_transform
									components.append({"center":str(part.box.get_center()),"size":str(part.box.size),"triangles":part.triangles,"relative":str(relative),"matching":semantic._matching("vc",relative*part.box.get_center(),part.triangles),"prefilter":semantic._has_selector("vc",relative*view.mesh.get_aabb()),"surface_count":view.mesh.get_surface_count()})
						check.cross_components=components
					if not block.is_empty():
						var below:Vector3=block.position-Vector3.UP*.02
						var farther:=PhysicsRayQueryParameters3D.create(below,pad.center-Vector3.UP*1.5,1);var under:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(farther)
						check.under_blocker=str(under.collider.get_path()) if not under.is_empty() else "none";check.under_point=str(under.position-map.global_position) if not under.is_empty() else "none"
				heal_checks.append(check)
			details.heal=heal_checks
		results[str(entry.slug)]=details;print("SEMANTIC_VALIDATE ",entry.slug," ",JSON.stringify(details))
		map.queue_free();resource=null;await process_frame;await physics_frame
	var output:String=OS.get_cmdline_user_args()[0];FileAccess.open(output+("/semantic-validation-probe-r14.json" if not only_map.is_empty() else ("/semantic-validation-alpha-r14.json" if limit_alpha else "/semantic-validation-r14.json")),FileAccess.WRITE).store_string(JSON.stringify(results,"\t"))
	game._quitting=true;game.queue_free();await process_frame;quit()
