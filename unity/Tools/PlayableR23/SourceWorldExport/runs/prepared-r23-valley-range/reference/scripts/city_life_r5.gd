extends Node3D
## Deterministic ambient city life. No combatants, damage, navigation server or traffic lights.
## Both peers use the room clock; all geometry lives and dies with the city.
const MODELS=[preload("res://assets/cyber_bird/traffic_r5/raven_taxi_argon.glb"),preload("res://assets/cyber_bird/traffic_r5/heron_van.glb"),preload("res://assets/cyber_bird/traffic_r5/kestrel_compact.glb")]
var game:Node
var cars:Array[Dictionary]=[]
var walkers:Array[Dictionary]=[]
var meshes:Dictionary={}
var routes:Array[Curve3D]=[]
var carry:Node
var simulation_time_override:float=-1.0 # Offline QA only; production follows the room clock.
var vehicle_materials:Dictionary={}
var traffic_by_rid:Dictionary={}
var _near_clock=0.0
func setup(owner_game:Node)->void:
	game=owner_game;name="AmbientCityLifeR5"
	process_physics_priority=-40
	# Separate opposing lanes stay on the authored 88m avenue grid. Equal speeds
	# and evenly spaced phases avoid overtaking or overlapping moving colliders.
	for radius in [352.7,349.8,266.2,261.8,90.2,85.8]:routes.append(_rounded_loop(radius,8))
	for lane in routes.size():
		var count:int=[14,14,12,12,6,6][lane]
		for i in count:
			var body:=AnimatableBody3D.new();body.name="Traffic_%d_%d"%[lane,i];body.collision_layer=1;body.collision_mask=0;body.sync_to_physics=false
			body.add_to_group("traffic_carry_r6")
			add_child(body)
			var type:int=(lane+i)%3
			var model:Node3D=MODELS[type].instantiate();body.add_child(model);_detail(model,150);_share_vehicle_materials(model)
			# Low body and useful cabin/cargo roof, rather than a full-height box
			# around empty hood space. All remain ordinary climb/build layer 1.
			_hull(body,Vector3(1.80,.70,3.65 if type==2 else 4.65),Vector3(0,.53,0))
			var roof:float=[1.51,2.16,1.45][type]
			var cabin_z:float=.65 if type==1 else .05
			_hull(body,Vector3(1.76 if type==1 else 1.36,roof-.82,2.55 if type==1 else 2.15),Vector3(0,(roof+.82)*.5,cabin_z))
			body.set_meta("traffic_roof_r6",Vector3(0,roof,cabin_z))
			var wheels:Array=[]
			for key in ["Wheel_FL","Wheel_FR","Wheel_RL","Wheel_RR"]:wheels.append(model.find_child(key,true,false))
			_driver(model)
			var record:Dictionary={"body":body,"model":model,"wheels":wheels,"radius":[.34,.35,.31][type],"route":routes[lane],"phase":float(i)/count,"speed":4.2 if lane<4 else 3.6,"direction":1.0 if lane%2==0 else -1.0,"previous_global_pose":Transform3D.IDENTITY,"pose_ready":false}
			cars.append(record);traffic_by_rid[body.get_rid()]=record
	# Six sheltered sidewalk promenades, four couriers each, outside the warmup gathering.
	for block in 6:
		var x:float=[-264,-176,-88,88,176,264][block]
		var path:=Curve3D.new();path.bake_interval=1.0
		# Mid-block stretch avoids intersections, street furniture and building facades.
		path.add_point(Vector3(x+9.0,0,-245));path.add_point(Vector3(x+9.0,0,-195))
		for i in 4:
			var person:=Node3D.new();person.name="Courier_%d_%d"%[block,i];add_child(person)
			var parts:=_courier(person,block+i)
			walkers.append({"body":person,"parts":parts,"path":path,"phase":i*.25+block*.073,"lane":float(i%2)*.85,"speed":1.05+block*.045})
	_physics_process(0)
	carry=preload("res://scripts/traffic_carry_r6.gd").new();add_child(carry);carry.setup(game.player,self)
func _hull(body:AnimatableBody3D,size:Vector3,at:Vector3)->void:
	var shape:=BoxShape3D.new();shape.size=size
	var collider:=CollisionShape3D.new();collider.shape=shape;collider.position=at;body.add_child(collider)
func _share_vehicle_materials(node:Node)->void:
	if node is MeshInstance3D:
		for i in node.mesh.get_surface_count():
			var mat:Material=node.mesh.surface_get_material(i)
			if mat==null:continue
			var key:String=mat.resource_name
			if key.is_empty():continue
			if not vehicle_materials.has(key):vehicle_materials[key]=mat
			node.set_surface_override_material(i,vehicle_materials[key])
	for child in node.get_children():_share_vehicle_materials(child)
func _rounded_loop(radius:float,corner:float)->Curve3D:
	var path:=Curve3D.new();path.bake_interval=.5
	for c in 4:
		var center:Vector3=[Vector3(radius-corner,0,-radius+corner),Vector3(radius-corner,0,radius-corner),Vector3(-radius+corner,0,radius-corner),Vector3(-radius+corner,0,-radius+corner)][c]
		for i in 13:
			var angle:float=(-90.0+c*90.0+i*7.5)*PI/180
			path.add_point(center+Vector3(cos(angle)*corner,0,sin(angle)*corner))
	path.add_point(path.get_point_position(0));return path
func clock_seconds()->float:
	if simulation_time_override>=0:return simulation_time_override
	if is_instance_valid(game.network_bridge) and game.network_bridge.live():return game.network_bridge.net.now()
	return Time.get_ticks_msec()*.001
func car_pose(car:Dictionary,time:float)->Transform3D:
	var path:Curve3D=car.route;var length:float=path.get_baked_length()
	var distance:float=fposmod(float(car.phase)*length+time*float(car.speed)*float(car.direction),length)
	var at:=path.sample_baked(distance,true)
	var direction:Vector3=(path.sample_baked(fposmod(distance+.2,length),true)-path.sample_baked(fposmod(distance-.2,length),true)).normalized()*float(car.direction)
	return Transform3D(Basis.looking_at(direction,Vector3.UP),at+Vector3.UP*.03)
func _physics_process(delta:float)->void:
	if not is_instance_valid(game):return
	var time:=clock_seconds()
	_near_clock+=delta
	var eye:Vector3=to_local(game.player.get_view_camera().global_position)
	for car in cars:
		var previous:Transform3D=car.body.global_transform
		car.body.transform=car_pose(car,time)
		car.previous_global_pose=previous if car.pose_ready else car.body.global_transform;car.pose_ready=true
		if _near_clock>=.2:car.model.visible=car.body.position.distance_squared_to(eye)<150*150
		if car.model.visible:
			for wheel in car.wheels:
				if is_instance_valid(wheel):wheel.rotation.x=-time*float(car.speed)/float(car.radius)
	for walker in walkers:
		var person:Node3D=walker.body
		var period:float=100.0/float(walker.speed)
		var travel:float=fposmod(time/period+float(walker.phase),1.0)*2
		var forward:bool=travel<1
		person.position=walker.path.sample_baked((travel if forward else 2-travel)*50,true)+Vector3(float(walker.lane),.12,0)
		person.rotation.y=PI if forward else 0.0
		if _near_clock>=.2:person.visible=person.position.distance_squared_to(eye)<75*75
		if person.visible:
			var stride:float=sin(time*6.5+float(walker.phase)*TAU)*.48
			walker.parts[0].rotation.x=stride;walker.parts[1].rotation.x=-stride
			walker.parts[2].rotation.x=-stride*.7;walker.parts[3].rotation.x=stride*.7
	if _near_clock>=.2:_near_clock=0
func previous_pose(body:Node3D)->Transform3D:
	var record:Dictionary=traffic_by_rid.get(body.get_rid(),{})
	return record.get("previous_global_pose",body.global_transform)
func body_from_hit(hit:Dictionary)->Node3D:
	var record:Dictionary=traffic_by_rid.get(hit.get("rid",RID()),{})
	return record.get("body",null)
func _material(key:String,color:String,glow:bool=false)->Material:
	if not meshes.has(key):
		var mat:=StandardMaterial3D.new();mat.albedo_color=Color(color);mat.roughness=.55;mat.metallic=.55
		if glow:mat.emission_enabled=true;mat.emission=Color(color);mat.emission_energy_multiplier=1.7
		meshes[key]=mat
	return meshes[key]
func _part(parent:Node3D,size:Vector3,at:Vector3,material:Material)->MeshInstance3D:
	var key:=str(size)
	if not meshes.has(key):var box:=BoxMesh.new();box.size=size;meshes[key]=box
	var mesh:=MeshInstance3D.new();mesh.mesh=meshes[key];mesh.material_override=material;mesh.position=at;mesh.visibility_range_end=75;parent.add_child(mesh);return mesh
func _courier(root_node:Node3D,style:int)->Array:
	var coat:=_material("coat_"+str(style%3),["182c3d","664b40","40405a"][style%3]);var dark:=_material("dark","111d27");var neon:=_material("neon_"+str(style%3),["48e5ee","ffbc72","f366ae"][style%3],true)
	_part(root_node,Vector3(.40,.61,.25),Vector3(0,1.13,0),coat)
	_part(root_node,Vector3(.26,.28,.27),Vector3(0,1.61,0),dark)
	_part(root_node,Vector3(.27,.055,.025),Vector3(0,1.64,-.144),neon)
	# Angular beak respirator and layered feather-shaped shoulder plates.
	var beak:=_part(root_node,Vector3(.13,.09,.16),Vector3(0,1.53,-.19),coat);beak.rotation.x=.3
	for side in [-1,1]:
		var feather:=_part(root_node,Vector3(.07,.30,.17),Vector3(side*.235,1.32,.02),neon);feather.rotation.z=side*.34
	_part(root_node,Vector3(.29,.39,.15),Vector3(0,1.15,.21),dark)
	var limbs:Array=[]
	for leg in [true,false]:
		for side in [-1,1]:
			var pivot:=Node3D.new();root_node.add_child(pivot);pivot.position=Vector3(side*(.11 if leg else .27),.84 if leg else 1.37,0)
			_part(pivot,Vector3(.15,.76,.17) if leg else Vector3(.12,.57,.14),Vector3(0,-.38 if leg else -.27,0),dark if leg else coat)
			if leg:_part(pivot,Vector3(.17,.10,.28),Vector3(0,-.76,-.04),dark)
			limbs.append(pivot)
	return limbs
func _driver(model:Node3D)->void:
	var dark:=_material("driver","17202a")
	_part(model,Vector3(.29,.32,.25),Vector3(-.36,1.24,-.5),dark)
	_part(model,Vector3(.24,.24,.24),Vector3(-.36,1.51,-.5),dark)
func _detail(node:Node,distance:float)->void:
	if node is GeometryInstance3D:node.visibility_range_end=distance;node.visibility_range_end_margin=0
	for child in node.get_children():_detail(child,distance)
