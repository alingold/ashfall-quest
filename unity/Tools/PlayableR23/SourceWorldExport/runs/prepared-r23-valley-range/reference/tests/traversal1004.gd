extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
var game:Node
var map:Node
var actor:CharacterBody3D
var failures:Array=[]
var facts:Dictionary={}
var capsule=CapsuleShape3D.new()
func _initialize()->void:call_deferred("run")
func freeze(node:Node)->void:
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func sweep(from:Vector3,motion:Vector3)->float:
	var q=PhysicsShapeQueryParameters3D.new();q.shape=capsule;q.transform=Transform3D(Basis.IDENTITY,from);q.motion=motion;q.collision_mask=1;q.exclude=[actor.get_rid()]
	return map.get_world_3d().direct_space_state.cast_motion(q)[0]
func walk_edge(a:Vector3,b:Vector3)->bool:
	var n=maxi(1,ceili(Vector2(a.x-b.x,a.z-b.z).length()/1.5));var previous=a
	for i in range(1,n+1):
		var p=a.lerp(b,float(i)/n);var floor_point:Vector3=map._floor(p+Vector3.UP*.8,2.0)
		if not floor_point.is_finite() or absf(floor_point.y-(previous.y-.6))>.45:return false
		p=floor_point+Vector3.UP*.6
		if sweep(previous+Vector3.UP*.36,p-previous)<.99:return false
		previous=p
	return absf(previous.y-b.y)<.45
func run(args_override:Array=[])->void:
	var args=args_override if not args_override.is_empty() else OS.get_cmdline_user_args();var id="archive:"+args[0];var started=Time.get_ticks_msec();capsule.radius=.28;capsule.height=1.72
	game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	game.front_menu.selected_map=id;game._start_from_title()
	for i in 8:await physics_frame
	map=game.active_sandbox_map;actor=game.player
	if not is_instance_valid(map) or not map.map_ready:finish(args[1],{"passed":false,"error":"map not ready"});return
	freeze(game);actor.xr_enabled=false;actor.input_enabled=true
	var points:Array[Vector3]=[]
	for p in map.spawn_points:points.append(p+Vector3.UP*.48)
	for p in map.loot_points:
		var unique=true
		for old in points:
			if old.distance_to(p)<1:unique=false;break
		if unique:points.append(p)
	var edges:Array=[];edges.resize(points.size())
	for i in points.size():edges[i]=[]
	var tested=0;var passed=0
	for i in points.size():
		var near:Array=[]
		for j in points.size():
			if i!=j and absf(points[i].y-points[j].y)<3:near.append({"id":j,"distance":points[i].distance_squared_to(points[j])})
		near.sort_custom(func(a:Dictionary,b:Dictionary):return a.distance<b.distance)
		for entry in near.slice(0,6):
			var j=int(entry.id)
			if j<i or entry.distance>1600:continue
			tested+=1
			if walk_edge(points[i],points[j]):edges[i].append(j);edges[j].append(i);passed+=1
		if i%40==0:await process_frame
	var component:Array=[];component.resize(points.size());component.fill(-1);var sizes:Array=[]
	for i in points.size():
		if component[i]!=-1:continue
		var index=sizes.size();var queue=[i];component[i]=index;var size=0
		while not queue.is_empty():
			var j=int(queue.pop_back());size+=1
			for next in edges[j]:
				if component[next]==-1:component[next]=index;queue.append(next)
		sizes.append(size)
	var enclosed:Array=[];var escapes=0;var movements:Array=[];var grabs=0
	for index in mini(8,map.spawn_points.size()):
		var p:Vector3=map.spawn_points[index];var exits=0
		for direction in 16:
			var angle=TAU*direction/16.0
			if sweep(p+Vector3.UP*.84,Vector3(cos(angle),0,sin(angle))*2)>.9:exits+=1
		var vertical=sweep(p+Vector3.UP*.84,Vector3.UP*4)
		if exits==0 and vertical<.2:enclosed.append({"spawn":index,"point":p,"up_fraction":vertical})
		else:escapes+=1
		for direction in 2:
			actor.global_position=p;actor.reset_motion();var heading=Vector3.RIGHT if direction==0 else Vector3.FORWARD;var origin=p
			for tick in 90:
				actor.apply_locomotion_motion(heading,heading,false,false,1.0/90)
				if tick%4==0:await physics_frame
			movements.append({"spawn":index,"direction":heading,"distance":actor.global_position.distance_to(origin),"finite":actor.global_position.is_finite(),"outside":map._outside(actor.global_position)})
			if not actor.global_position.is_finite():failures.append("nonfinite actual player motion")
		var hit=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(p+Vector3.UP*1.2,p+Vector3.UP*1.2+Vector3.FORWARD*4,1,[actor.get_rid()]))
		if not hit.is_empty() and hit.collider.is_in_group("climbable"):
			if not actor.find_grab_surface(hit.position+hit.normal*.025).is_empty():grabs+=1
	var high:Array=map._meta.bounds_max;actor.global_position=map.global_position+Vector3(float(high[0])+100,100,0);map._physics_process(.2);var recovered=actor.global_position.distance_to(map.spawn)<.01
	if not recovered:failures.append("outside recovery failed")
	var hazard_tested=false;var hazard_recovered=false
	for area in map.find_children("*","Area3D",true,false):
		if not area.is_in_group("archive_killzone"):continue
		for collider in area.get_children():
			if collider is CollisionShape3D and collider.shape is ConvexPolygonShape3D:
				var center=Vector3.ZERO
				for p in collider.shape.points:center+=p
				center=collider.to_global(center/collider.shape.points.size())
				if map._hazard(center):actor.global_position=center-Vector3.UP*.8;map._physics_process(.2);hazard_tested=true;hazard_recovered=actor.global_position.distance_to(map.spawn)<.01;break
		if hazard_tested:break
	if hazard_tested and not hazard_recovered:failures.append("hazard recovery failed")
	facts={"points":points.size(),"walking_components":sizes.size(),"component_sizes":sizes,"spawn_components":component.slice(0,map.spawn_points.size()),"walk_edges_tested":tested,"walk_edges_passed":passed,"sampled_spawns":mini(8,map.spawn_points.size()),"local_escape_candidates":escapes,"enclosure_candidates":enclosed,"actual_movement_routes":movements,"actual_wall_grabs_found":grabs,"outside_recovered":recovered,"hazard_tested":hazard_tested,"hazard_recovered":hazard_recovered,"milliseconds":Time.get_ticks_msec()-started}
	finish(args[1],{"passed":failures.is_empty(),"map":id,"failures":failures,"facts":facts,"scope":"sampled actual AshPlayer locomotion (90 ticks per direction, four manual calls per awaited physics frame), capsule walking graph including boundary collisions, local enclosure diagnostics and recovery. Walking islands are not proof of trapping: climbing/flight/native-script routes require separate acceptance. XR off; no physical controller input."})
func finish(path:String,result:Dictionary)->void:
	var f=FileAccess.open(path,FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("TRAVERSAL1004 "+str(result.get("passed",false)));quit(0 if result.get("passed",false) else 1)
