extends Node
const Art=preload("res://scripts/launch_art_r12.gd")
var bridge:Node
var game:Node
var owner_map:Node3D
var structure:Node3D
var stations:Array=[]
var flights:Dictionary={}
var layout_key=""
var top=0.0
var _control_held=true
var _ride_key=""
var _board_key=""
var _visual_clock=0.0
var _destination_rng:=RandomNumberGenerator.new()
const SLOTS=12
const CORNERS=[Vector2(0,0),Vector2(1,1),Vector2(1,0),Vector2(0,1),Vector2(.5,0),Vector2(.5,1),Vector2(0,.5),Vector2(1,.5)]
const DIRECTIONS=[Vector3.BACK,Vector3.FORWARD,Vector3.LEFT,Vector3.RIGHT,Vector3.BACK,Vector3.FORWARD,Vector3.RIGHT,Vector3.LEFT]
func setup(b:Node):
	bridge=b;game=b.game;name="TeamLaunchR12";process_physics_priority=-10
	_destination_rng.randomize()
	bridge.net.pod_provider_r12=plan
func eligible()->bool:
	return bridge.live() and uses_sky_platform(str(bridge.net.rules.map)) and bridge.net.rules.mode=="revive" and int(bridge.net.rules.teams)>0 and bridge.net.stage in ["lobby","countdown","match"]
static func uses_sky_platform(map_id:String)->bool:
	return map_id not in ["archive:battlefield-paintball-gods-pitdevil","archive:skb-paintball-gods-skblong"]
func clear():
	if is_instance_valid(structure):structure.get_parent().remove_child(structure);structure.queue_free()
	structure=null;owner_map=null;stations.clear();flights.clear();layout_key="";_ride_key="";_board_key="";_control_held=true
	if is_instance_valid(game):game.player.pod_carried_r12=false
func ensure_layout()->bool:
	if not eligible():
		if structure!=null:clear()
		return false
	var map:Node3D=game.active_sandbox_map if is_instance_valid(game.active_sandbox_map) else game.world
	if "map_ready" in map and not map.map_ready:return false
	var key:String=str(map.get_instance_id())+":"+str(bridge.net.epoch)+":"+str(bridge.net.rules.teams)
	if key==layout_key and is_instance_valid(structure):return true
	clear();owner_map=map;layout_key=key
	var info:Dictionary=preload("res://scripts/map_catalog1000.gd").info(str(bridge.net.rules.map));var low:Vector2=info.min;var high:Vector2=info.max
	top=info.origin.y+40
	if "deployment_top_r12" in map:top=float(map.deployment_top_r12)
	elif "_meta" in map and map._meta.has("solid_max"):top=map.global_position.y+float(map._meta.solid_max[1])
	else:
		# Physics shapes retain their bounds with both native and Dummy rendering.
		for collider in map.find_children("*","CollisionShape3D",true,false):
			if collider.disabled or collider.shape==null:continue
			var shape:Shape3D=collider.shape;var points:=PackedVector3Array()
			if shape is ConcavePolygonShape3D:points=shape.get_faces()
			elif shape is ConvexPolygonShape3D:points=shape.points
			elif shape is BoxShape3D:top=maxf(top,(collider.global_transform*AABB(-shape.size*.5,shape.size)).end.y)
			elif shape is SphereShape3D:top=maxf(top,collider.global_position.y+shape.radius)
			elif shape is CapsuleShape3D or shape is CylinderShape3D:top=maxf(top,collider.global_position.y+shape.height*.5+shape.radius)
			var frame:Transform3D=collider.global_transform
			for vertex in points:top=maxf(top,(frame*vertex).y)
	structure=Node3D.new();structure.name="OriginalTeamLaunchPlatformsR12";map.add_child(structure);structure.global_transform=Transform3D.IDENTITY
	var shared_deck:ArrayMesh=Art.deck()
	for team in int(bridge.net.rules.teams):
		var direction:Vector3=DIRECTIONS[team];var margin:=Vector2(25.5,10.0) if absf(direction.z)>.5 else Vector2(10.0,25.5)
		# Fit the complete 49 m deck at the perimeter, including its rotated corners.
		margin.x=minf(margin.x,(high.x-low.x)*.5);margin.y=minf(margin.y,(high.y-low.y)*.5)
		var uv:Vector2=CORNERS[team];var xz:Vector2=low+margin+(high-low-margin*2)*uv;var y:float=top+65+(team*18 if minf(high.x-low.x,high.y-low.y)<100 else 0)
		var at:Vector3=info.origin+Vector3(xz.x,0,xz.y);at.y=y
		var frame:=Transform3D(Basis.looking_at(direction),at)
		var station:=Node3D.new();station.name="Team_"+str(team+1);structure.add_child(station);station.transform=frame
		var deck:=MeshInstance3D.new();deck.mesh=shared_deck;station.add_child(deck)
		var body:=StaticBody3D.new();body.collision_layer=1;body.collision_mask=0;body.add_to_group("climbable");body.add_to_group("construction_terrain");station.add_child(body);Art.collision(body,Vector3(0,-.35,0),Vector3(49,.7,18))
		var frame_collision:=CollisionShape3D.new();var frame_shape:=ConcavePolygonShape3D.new();frame_shape.backface_collision=true;frame_shape.set_faces(shared_deck.get_faces());frame_collision.shape=frame_shape;body.add_child(frame_collision)
		# Open front/side routes: these visible structural braces do not fence players onto the deck.
		Art.label(station,"TEAM "+str(team+1)+"  /  SKY DECK",Vector3(0,3.7,5.8),.014)
		var instructions:Label3D=Art.label(station,"TAKE ANY AVAILABLE POD\nOR JUMP / GLIDE OFF THE PLATFORM",Vector3(0,2.6,5.8),.0058);instructions.rotation.y=PI
		var multimesh:=MultiMesh.new();multimesh.transform_format=MultiMesh.TRANSFORM_3D;multimesh.mesh=Art.cabin();multimesh.instance_count=SLOTS
		var pods:=MultiMeshInstance3D.new();pods.multimesh=multimesh;station.add_child(pods)
		var bodies:Array=[];var labels:Array=[]
		for slot in SLOTS:
			var pose:=Transform3D(Basis.IDENTITY,Vector3((slot-5.5)*3.6,.14,-6.0));multimesh.set_instance_transform(slot,pose)
			var cabin:PhysicsBody3D=Art.pod_body(station);cabin.transform=pose;bodies.append(cabin)
			labels.append(Art.control(cabin,"LAUNCH"))
		stations.append({"node":station,"frame":frame,"pods":multimesh,"bodies":bodies,"labels":labels})
	return true
func ceiling()->float:
	return top+65+int(bridge.net.rules.teams)*18+20 if is_instance_valid(structure) else -INF
func staging(point:Vector3)->bool:
	if not is_instance_valid(structure):return false
	for station in stations:
		if AABB(Vector3(-25,-8.5,-10),Vector3(50,28,20)).has_point(station.frame.affine_inverse()*point):return true
	return false
func authorized_flight(point:Vector3)->bool:
	if not is_instance_valid(structure):return false
	for ride in bridge.net.pods_r12.values():
		if ride.get("state","")=="flying" and point.distance_to(bridge.net.pod_position_r12(ride))<3:return true
	return false
func spawn_for(id:int,players:Dictionary)->Vector3:
	if not ensure_layout():return Vector3.INF
	var team:int=clampi(int(players[id].team),0,stations.size()-1);var reserved:Array=[]
	for other in players:
		if other!=id and int(players[other].get("launch_team_r12",-1))==team:reserved.append(int(players[other].get("launch_slot_r12",-1)))
	var slot=0
	while slot in reserved and slot<SLOTS-1:slot+=1
	players[id].launch_team_r12=team;players[id].launch_slot_r12=slot;players[id].pod_key_r12="%d:%d:%d"%[bridge.net.epoch,team,slot];players[id].deployment_r12="deck"
	return stations[team].frame*Vector3((slot-5.5)*3.6,.12,3.0)
func pod_pose(id:int)->Transform3D:
	var p:Dictionary=bridge.net.players.get(id,{});var team:int=int(p.get("launch_team_r12",-1));var slot:int=int(p.get("launch_slot_r12",-1))
	if team<0 or team>=stations.size() or slot<0 or slot>=SLOTS:return Transform3D(Basis.IDENTITY,Vector3.INF)
	return stations[team].frame*Transform3D(Basis.IDENTITY,Vector3((slot-5.5)*3.6,.14,-6))
func cabin_at(id:int,point:Vector3)->Dictionary:
	var p:Dictionary=bridge.net.players.get(id,{});var team:int=int(p.get("team",-1))
	if team<0 or team>=stations.size():return {}
	for slot in SLOTS:
		var pose:Transform3D=stations[team].frame*Transform3D(Basis.IDENTITY,Vector3((slot-5.5)*3.6,.14,-6));var local:Vector3=pose.affine_inverse()*point
		if absf(local.x)<.95 and absf(local.z)<1.3 and absf(local.y)<.7:return {"pose":pose,"pod":"%d:%d:%d"%[bridge.net.epoch,team,slot],"slot":slot}
	return {}
func plan(id:int,data:Dictionary)->Dictionary:
	var net:Node=bridge.net
	if not ensure_layout() or net.stage!="match" or not net.players.has(id):return {}
	var p:Dictionary=net.players[id]
	if p.get("deployment_r12","")!="deck":return {}
	var cabin:Dictionary=cabin_at(id,net._position(id))
	if cabin.is_empty() or str(data.get("pod",""))!=str(cabin.pod):return {}
	var pose:Transform3D=cabin.pose
	if not pose.origin.is_finite():return {}
	var local:Vector3=pose.affine_inverse()*net._position(id)
	if absf(local.x)>.95 or absf(local.z)>1.3 or absf(local.y)>.65:return {}
	var candidates:Array=[];var map:Node=game.active_sandbox_map
	if is_instance_valid(map) and "spawn_points" in map:candidates.append_array(map.spawn_points)
	if is_instance_valid(map) and "loot_points" in map:
		for at in map.loot_points:candidates.append(at-Vector3.UP*.48)
	if candidates.is_empty():candidates.append(bridge._arena_spawn_r12(id,net.players,net.rules))
	for i in range(candidates.size()-1,0,-1):
		var selected:int=_destination_rng.randi_range(0,i);var previous:Vector3=candidates[i];candidates[i]=candidates[selected];candidates[selected]=previous
	var target:=Vector3.INF
	var info:Dictionary=preload("res://scripts/map_catalog1000.gd").info(str(net.rules.map));var span:Vector2=info.max-info.min;var landing_rect:=Rect2(info.min,span).grow(-minf(4,minf(span.x,span.y)*.1))
	# Built-in arenas do not carry archive spawn/loot arrays. Search their actual
	# floors rather than relying on one random spawn that may be behind this pod.
	if not str(net.rules.map).begins_with("archive:"):
		for z in 9:
			for x in 9:
				var point:Vector3=info.origin+Vector3(landing_rect.position.x+landing_rect.size.x*(x+.5)/9,0,landing_rect.position.y+landing_rect.size.y*(z+.5)/9)
				var ray:=PhysicsRayQueryParameters3D.create(Vector3(point.x,top+5,point.z),Vector3(point.x,info.origin.y-1000,point.z),1)
				var hit:Dictionary=game.get_world_3d().direct_space_state.intersect_ray(ray)
				if not hit.is_empty() and hit.normal.y>.7:candidates.append(hit.position+Vector3.UP*.12)
	for point in candidates:
		if not landing_rect.has_point(Vector2(point.x-info.origin.x,point.z-info.origin.z)):continue
		if (point-pose.origin).dot(-pose.basis.z)<minf(15.0,minf(span.x,span.y)*.15):continue
		if is_instance_valid(map) and map.has_method("safe_spawn_r8") and not map.safe_spawn_r8(point):continue
		if Vector2(point.x-pose.origin.x,point.z-pose.origin.z).length()<8 and candidates.size()>1:continue
		if not target.is_finite():target=point
		if Vector2(point.x-pose.origin.x,point.z-pose.origin.z).length()>=minf(60,minf(span.x,span.y)*.25):target=point;break
	if not target.is_finite():return {}
	# Clear the row through its open front before level travel above the highest geometry.
	# Only translation changes; never rotate the headset or its calibration origin.
	var start:Vector3=pose.origin+Vector3.UP*.12;var clear:Vector3=start+pose.basis*Vector3(0,5,-10);var end:=Vector3(target.x,clear.y,target.z)
	var landing:Vector3=target
	var query:=PhysicsRayQueryParameters3D.create(end+Vector3.UP*3,Vector3(target.x,info.origin.y-2000,target.z),1)
	var hit:Dictionary=game.get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty() or hit.normal.y<.7:return {}
	landing=hit.position+Vector3.UP*.12
	var exit_target:Vector3=landing+pose.basis*Vector3(0,0,2.0)
	var exit_ray:=PhysicsRayQueryParameters3D.create(exit_target+Vector3.UP*3,exit_target-Vector3.UP*5,1)
	var exit_hit:Dictionary=game.get_world_3d().direct_space_state.intersect_ray(exit_ray)
	if not exit_hit.is_empty() and exit_hit.normal.y>.7 and absf(exit_hit.position.y-landing.y)<1:exit_target=exit_hit.position+Vector3.UP*.12
	else:exit_target=landing+Vector3.UP*.3
	var duration:float=clampf((start.distance_to(clear)+clear.distance_to(end)+end.distance_to(landing))/35,1.5,120)
	return {"pod":cabin.pod,"slot":cabin.slot,"start":start,"clear":clear,"end":landing,"cruise":end,"exit_target":exit_target,"ground_target":landing,"duration":duration,"basis":pose.basis,"team":int(p.team)}
func _physics_process(delta:float):
	if not ensure_layout():return
	var net:Node=bridge.net;var me:int=net.me();var player:Node=game.player;var p:Dictionary=net.players.get(me,{})
	var ride:Dictionary=net.pods_r12.get(me,{})
	player.pod_carried_r12=not ride.is_empty() and ride.get("state","")=="flying" and p.get("life","") =="alive"
	if player.pod_carried_r12:
		var ride_key:String=str(ride.get("pod",""))+":"+str(ride.get("time",0))
		if ride_key!=_ride_key:player.reset_motion();player.pod_carried_r12=true;_ride_key=ride_key;_visual_clock=.1
		player.global_position=net.pod_position_r12(ride);player.velocity=Vector3.ZERO;player.input_enabled=false
	else:
		if not _ride_key.is_empty() and ride.get("state","")=="released":_sound(structure,"pod_exit_r12",player.global_position)
		_ride_key=""
	var boarded:Dictionary=cabin_at(me,player.global_position);var pod:Transform3D=boarded.get("pose",pod_pose(me));var inside:bool=not boarded.is_empty()
	var board_key:String=str(boarded.get("pod","")) if not player.pod_carried_r12 else ""
	if not board_key.is_empty() and board_key!=_board_key:_control_held=true
	_board_key=board_key
	var pressed:bool=Input.is_physical_key_pressed(KEY_E)
	if game.player.xr_enabled:
		pressed=false
		for hand in [player.right_hand,player.left_hand]:
			var frame:Transform3D=Transform3D(ride.get("basis",pod.basis),net.pod_position_r12(ride)-Vector3.UP*.12) if player.pod_carried_r12 else pod
			var control:Vector3=frame*Vector3(0,.56,-.8);var toward:Vector3=control-hand.global_position
			var aims:bool=toward.length()<.45 or (toward.length()<2 and (-hand.global_basis.z).dot(toward.normalized())>.97)
			if aims and (hand.get_float("trigger")>.65 or hand.get_float("grip")>.65):pressed=true
	if pressed and not _control_held and not game.field_menu.is_open and not game.front_menu.session_overlay:
		if player.pod_carried_r12:net.request("pod_eject")
		elif inside and p.get("deployment_r12","")=="deck":net.request("pod_launch",{"pod":boarded.pod})
	_control_held=pressed
	_visual_clock+=delta
	if _visual_clock>=.1:_visual_clock=0;_refresh_docks()
	_refresh_flights()
func _refresh_docks():
	var used:Dictionary={}
	for ride in bridge.net.pods_r12.values():used[str(ride.pod)]=true
	for team in stations.size():
		var station:Dictionary=stations[team]
		for slot in SLOTS:
			var key:String="%d:%d:%d"%[bridge.net.epoch,team,slot];var consumed:bool=used.has(key)
			station.pods.set_instance_transform(slot,Transform3D(Basis.IDENTITY.scaled(Vector3.ZERO),Vector3.ZERO) if consumed else Transform3D(Basis.IDENTITY,Vector3((slot-5.5)*3.6,.14,-6)))
			station.bodies[slot].collision_layer=0 if consumed else 1;station.bodies[slot].visible=not consumed
			station.labels[slot].text="LAUNCH\n"+("TRIGGER / GRIP" if game.player.xr_enabled else "E")
func _refresh_flights():
	var net:Node=bridge.net
	for id in flights.keys():
		if not net.pods_r12.has(id) or ride_state_r18(net.pods_r12[id]) not in ["flying","landed"]:
			flights[id].queue_free();flights.erase(id)
	for id in net.pods_r12:
		var ride:Dictionary=net.pods_r12[id]
		if ride_state_r18(ride) not in ["flying","landed"]:continue
		if not flights.has(id):
			var body:PhysicsBody3D=Art.pod_body(structure,true);body.name="TransitPod_"+str(id);var view:=MeshInstance3D.new();view.mesh=Art.cabin();body.add_child(view);Art.control(body,"EJECT\n"+("TRIGGER / GRIP" if game.player.xr_enabled else "E"),true);flights[id]=body
			body.global_transform=Transform3D(ride.basis,net.pod_position_r12(ride)-Vector3.UP*.12)
			if ride_state_r18(ride)=="flying":_sound(body,"pod_launch_r12",net.pod_position_r12(ride));_sound(body,"pod_engine_r12",net.pod_position_r12(ride),true)
		flights[id].global_transform=Transform3D(ride.basis,net.pod_position_r12(ride)-Vector3.UP*.12)
		if ride_state_r18(ride)=="landed" and not flights[id].has_meta("landed_r18"):
			flights[id].set_meta("landed_r18",true)
			for child in flights[id].get_children():
				if child is AudioStreamPlayer3D:child.stop()
			var controls=flights[id].get_node_or_null("PodControlsR18")
			if controls!=null:controls.hide()
			if net.now()<float(ride.time)+float(ride.duration)+1.0:_sound(flights[id],"pod_land",flights[id].global_position)
func ride_state_r18(ride:Dictionary)->String:return str(ride.get("state",""))
func _sound(parent:Node3D,cue:String,at:Vector3,looping:bool=false):
	var audio:=AudioStreamPlayer3D.new();audio.stream=load("res://assets/audio/sfx/r18/"+cue.trim_suffix("_r12")+".wav");audio.bus="SFX";audio.unit_size=3;audio.max_distance=50;audio.volume_db=-11 if looping else -7;parent.add_child(audio)
	if looping and audio.stream is AudioStreamWAV:audio.stream.loop_mode=AudioStreamWAV.LOOP_FORWARD;audio.stream.loop_end=audio.stream.data.size()/2
	if not looping:audio.global_position=at;audio.finished.connect(audio.queue_free)
	audio.play()
