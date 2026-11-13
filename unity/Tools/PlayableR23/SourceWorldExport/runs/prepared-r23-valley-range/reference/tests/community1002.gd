extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var stats:Dictionary={}
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func run()->void:
	game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	game.front_menu.selected_map="community_local";game._start_from_title()
	for i in 8:await physics_frame
	var map:Node=game.active_sandbox_map
	check(is_instance_valid(map) and map.get("MAP_ID")=="community_local","community selected in real game")
	check(map.map_ready,"settled collision map_ready")
	check(map.spawn_points.size()>=8,"recorded spawns validated")
	check(map.loot_points.size()>30,"broad floor-validated loot positions")
	check(game.player.global_position.distance_to(map.spawn)<1,"player at safe recorded spawn")
	check(map._geometry.get_node("InvisibleBoundary").collision_layer==1,"invisible boundary solid")
	check(not map._geometry.get_node("InvisibleBoundary").is_in_group("climbable"),"invisible boundary not climbable")
	check(map._geometry.get_node("WalkThroughDecoration").get_children().is_empty(),"walk-through has no collision")
	check(map._geometry.find_children("World_*","StaticBody3D",true,false).size()>40,"chunk collision exists")
	var points:Array=map.spawn_points.duplicate()
	for point in points:
		var floor_point=map._floor(point+Vector3.UP,2)
		check(floor_point.is_finite() and absf(point.y-floor_point.y)<.3,"spawn has floor at metre scale")
	var count=game.ammo_pickups.boxes.size();var weapons=game.weapon_inventory.pickups.size();var heals=game.field_consumables.pickups.size()
	check(count>=72 and weapons>=20 and heals>=3,"ASHFALL loot providers populated")
	var spread=AABB();var first=true
	for prop in game.weapon_inventory.pickups.values():
		if first:spread=AABB(prop.global_position,Vector3.ZERO);first=false
		else:spread=spread.expand(prop.global_position)
	check(spread.size.x>150 and spread.size.z>150,"weapons dispersed across map")
	var wall_proof=_architecture_probes(map)
	check(wall_proof.has("wall"),"vertical wall contact found")
	check(wall_proof.has("opening"),"unfilled framed architectural opening found")
	for i in 3:
		game._begin_chapter()
		for frame in 5:await physics_frame
		check(game.ammo_pickups.boxes.size()==count,"reset supply count stable")
		check(game.weapon_inventory.pickups.size()==weapons,"reset weapon count stable")
		check(game.field_consumables.pickups.size()==heals,"reset heal count stable")
	game.player.global_position=map.global_position+Vector3(0,-30,0)
	for i in 3:await physics_frame
	check(game.player.global_position.distance_to(map.spawn)<2,"fall recovery")
	stats={"spawns":points,"loot_sites":map.loot_points.size(),"supply_instances":count,"weapons":weapons,"heals":heals,"map":map.statistics,"architecture":wall_proof}
	# Preserve old maps and prove community geometry is removed on switch.
	for id in ["range","community_local","cyber_bird","community_local","valley"]:
		preload("res://scripts/cyber_bird_registration.gd").select(game,id)
		for i in 8:await physics_frame
		check(game.get_tree().get_nodes_in_group("climbable").size()>0,"map switch retains world collision")
		if id=="community_local":check(game.active_sandbox_map.map_ready and game.active_sandbox_map.spawn_points.size()==points.size(),"repeat community load identical")
		else:check(game.find_children("LocalCommunityMap","Node3D",true,false).is_empty(),"community removed on map switch")
	var report={"checks":checks,"failures":failures,"passed":failures.is_empty(),"statistics":stats,"scope":"actual headless game and physics queries; no headset proof"}
	var args=OS.get_cmdline_user_args()
	if args.size()>0:
		var f=FileAccess.open(args[0],FileAccess.WRITE);f.store_string(JSON.stringify(report,"\t"))
	print("COMMUNITY_TEST "+JSON.stringify(report));quit(0 if failures.is_empty() else 1)
func _ray(a:Vector3,b:Vector3)->Dictionary:return game.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(a,b,1))
func _architecture_probes(map:Node)->Dictionary:
	var proof:Dictionary={}
	for site in map.loot_points:
		for direction in [Vector3.LEFT,Vector3.RIGHT,Vector3.FORWARD,Vector3.BACK]:
			var hit=_ray(site+Vector3.UP,site+Vector3.UP+direction*24)
			if hit.is_empty() or absf(hit.normal.y)>.05 or not hit.collider.is_in_group("climbable"):continue
			var normal:Vector3=hit.normal;var wall:Vector3=hit.position
			if not proof.has("wall"):
				var query=PhysicsShapeQueryParameters3D.new();var sphere=SphereShape3D.new();sphere.radius=.18;query.shape=sphere;query.collision_mask=1;query.transform=Transform3D(Basis.IDENTITY,wall+normal);query.motion=-normal*2
				var fractions=game.get_world_3d().direct_space_state.cast_motion(query);check(fractions[0]<.8,"wall blocks swept head-sized sphere")
				var hand:Node3D=game.player.left_hand;var pose=hand.global_transform;hand.global_position=wall+normal*.03
				check(not game.player.find_grab_surface(hand.global_position).is_empty(),"actual player grip query finds imported wall")
				game.player._update_grab(hand,true,false);check(game.player._grabs.has(hand),"actual climb anchor acquired on imported wall");game.player._update_grab(hand,false,false);hand.global_transform=pose
				proof.wall={"point":wall,"normal":normal,"sweep":fractions[0]}
			var side=normal.cross(Vector3.UP)
			for x in range(-4,5):
				for y in range(-2,7):
					var point=wall+side*float(x)*.65+Vector3.UP*float(y)*.65
					if not _ray(point+normal*.75,point-normal*.75).is_empty():continue
					var below=point-Vector3.UP*1.3;var above=point+Vector3.UP*1.3
					if _ray(below+normal*.75,below-normal*.75).is_empty() or _ray(above+normal*.75,above-normal*.75).is_empty():continue
					var query=PhysicsShapeQueryParameters3D.new();var sphere=SphereShape3D.new();sphere.radius=.15;query.shape=sphere;query.collision_mask=1;query.transform=Transform3D(Basis.IDENTITY,point+normal*.65);query.motion=-normal*1.3
					var fractions=game.get_world_3d().direct_space_state.cast_motion(query)
					if fractions[0]>.99:proof.opening={"point":point,"normal":normal,"sweep":fractions[0]};check(true,"opening admits swept head-sized sphere while sill/lintel remain solid");return proof
	return proof
