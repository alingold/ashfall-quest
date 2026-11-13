extends RefCounted
## Original procedural launch hardware; no copied assets. Open launch face is -Z.
static var materials:Array[Material]=[]
static var pod_mesh:ArrayMesh
static var deck_mesh_r14:ArrayMesh
static func palette()->Array[Material]:
	if not materials.is_empty():return materials
	for color in ["26343f","c53b40","b8c4c6","e8bb52","101b24","68dbe2","465b68","79292e"]:
		var mat:=StandardMaterial3D.new();mat.albedo_color=Color(color);mat.metallic=.5;mat.roughness=.76
		if color=="68dbe2":mat.emission_enabled=true;mat.emission=Color(color);mat.emission_energy_multiplier=.7
		materials.append(mat)
	return materials
static func box(parts:Array,at:Vector3,size:Vector3,color:int,basis:Basis=Basis.IDENTITY,solid:bool=true):
	parts.append({"at":at,"size":size,"color":color,"basis":basis,"solid":solid})
static func beam(parts:Array,a:Vector3,b:Vector3,width:float,color:int,solid:bool=true):
	var direction:Vector3=b-a;var axis:Vector3=Vector3.RIGHT if absf(direction.normalized().dot(Vector3.UP))>.98 else Vector3.UP
	box(parts,(a+b)*.5,Vector3(width,width,direction.length()),color,Basis.looking_at(direction,axis),solid)
static func profile(parts:Array,at:Vector3,outline:Array,thickness:float,color:int,solid:bool=true):
	# Closed thick panels extruded along X. Outlines are (Z,Y), leaving real holes.
	var points:=PackedVector2Array();for p in outline:points.append(p)
	parts.append({"at":at,"profile":points,"thickness":thickness,"color":color,"basis":Basis.IDENTITY,"solid":solid})
static func _vertices(part:Dictionary)->PackedVector3Array:
	var result:=PackedVector3Array()
	if part.has("profile"):
		for x in [-float(part.thickness)*.5,float(part.thickness)*.5]:
			for p in part.profile:result.append(Vector3(x,p.y,p.x))
	else:
		var cube:=BoxMesh.new();cube.size=part.size;result=cube.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	return result
static func _faces(part:Dictionary)->PackedVector3Array:
	var result:=PackedVector3Array();var points:PackedVector3Array=_vertices(part)
	if part.has("profile"):
		var n:int=part.profile.size();var tris:PackedInt32Array=Geometry2D.triangulate_polygon(part.profile)
		for i in range(0,tris.size(),3):
			for j in [0,1,2]:result.append(points[tris[i+j]])
			for j in [2,1,0]:result.append(points[n+tris[i+j]])
		for i in n:
			var next:int=(i+1)%n
			for j in [i,i+n,next+n,i,next+n,next]:result.append(points[j])
		# Match Godot's clockwise solid winding on both caps and all four-edge walls.
		for i in range(0,result.size(),3):
			var first:Vector3=result[i];result[i]=result[i+2];result[i+2]=first
	else:
		var cube:=BoxMesh.new();cube.size=part.size;var indices:PackedInt32Array=cube.surface_get_arrays(0)[Mesh.ARRAY_INDEX]
		for j in indices:result.append(points[j])
	for i in result.size():result[i]=part.basis*result[i]+part.at
	return result
static func mesh(parts:Array)->ArrayMesh:
	var result:=ArrayMesh.new();var mats:Array=palette()
	for color in mats.size():
		var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES);var count=0
		for part in parts:
			if int(part.color)!=color:continue
			var faces:PackedVector3Array=_faces(part)
			for i in range(0,faces.size(),3):
				var normal:Vector3=(faces[i+2]-faces[i]).cross(faces[i+1]-faces[i]).normalized()
				for j in 3:tool.set_normal(normal);tool.add_vertex(faces[i+j]);count+=1
		if count>0:tool.index();tool.commit(result);result.surface_set_material(result.get_surface_count()-1,mats[color])
	return result
static func cabin_parts_r14()->Array:
	var p:Array=[]
	box(p,Vector3(0,.02,0),Vector3(2.3,.24,2.9),0)
	box(p,Vector3(0,.18,.08),Vector3(1.85,.08,2.48),4)
	for side in [-1,1]:
		# Four thick shell sections surround each empty side window. No glass plane.
		profile(p,Vector3(side*1.035,0,0),[Vector2(-1.36,.15),Vector2(1.32,.15),Vector2(1.32,1.0),Vector2(.86,1.12),Vector2(-.90,1.04),Vector2(-1.36,.72)],.23,1)
		profile(p,Vector3(side*1.035,0,0),[Vector2(-1.31,2.12),Vector2(-.82,2.94),Vector2(.66,3.18),Vector2(1.28,2.79),Vector2(1.28,2.30),Vector2(.80,2.20)],.23,1)
		profile(p,Vector3(side*1.035,0,0),[Vector2(-1.36,.72),Vector2(-.90,1.04),Vector2(-.95,2.21),Vector2(-1.31,2.12)],.23,0)
		profile(p,Vector3(side*1.035,0,0),[Vector2(.84,1.10),Vector2(1.32,1.0),Vector2(1.28,2.79),Vector2(.80,2.20)],.23,0)
		for pair in [[Vector3(side*1.17,1.09,-.88),Vector3(side*1.17,1.16,.80)],[Vector3(side*1.17,2.12,-.94),Vector3(side*1.17,2.22,.78)],[Vector3(side*1.17,1.09,-.88),Vector3(side*1.17,2.12,-.94)],[Vector3(side*1.17,1.16,.80),Vector3(side*1.17,2.22,.78)]]:beam(p,pair[0],pair[1],.065,2,false)
		beam(p,Vector3(side*.99,2.13,-1.31),Vector3(side*.99,2.94,-.82),.19,2)
		beam(p,Vector3(side*.99,2.94,-.82),Vector3(side*.99,3.18,.66),.19,0)
		beam(p,Vector3(side*.99,3.18,.66),Vector3(side*.99,2.79,1.28),.19,2)
		box(p,Vector3(side*1.185,.55,-.1),Vector3(.03,.13,1.85),7,Basis.IDENTITY,false)
		box(p,Vector3(side*1.205,.78,-.14),Vector3(.03,.035,1.30),2,Basis.IDENTITY,false)
		box(p,Vector3(side*1.23,.69,.88),Vector3(.28,.86,.62),0)
		for y in [.39,.54,.69,.84,.99]:box(p,Vector3(side*1.385,y,.88),Vector3(.025,.055,.45),2,Basis.IDENTITY,false)
		for z in [-.67,-.28,.11]:box(p,Vector3(side*1.17,2.55,z),Vector3(.045,.16,.075),4,Basis(Vector3.RIGHT,-.13),false)
		beam(p,Vector3(side*.91,.32,1.27),Vector3(side*.91,2.69,1.27),.11,2)
		box(p,Vector3(side*.825,1.22,.86),Vector3(.065,.065,.44),3,Basis.IDENTITY,false)
		box(p,Vector3(side*.91,2.37,1.34),Vector3(.045,.36,.03),5,Basis.IDENTITY,false)
		box(p,Vector3(side*.94,.26,-1.35),Vector3(.29,.12,.20),3,Basis.IDENTITY,false)
	# Faceted shell leaves over 2.5m usable central headroom; entrance stays open.
	box(p,Vector3(0,3.05,-.08),Vector3(1.92,.20,1.5),0,Basis(Vector3.RIGHT,-.16))
	box(p,Vector3(0,2.76,-1.06),Vector3(1.94,.18,.96),1,Basis(Vector3.RIGHT,-.54))
	box(p,Vector3(0,2.96,1.01),Vector3(1.94,.18,.73),1,Basis(Vector3.RIGHT,.56))
	box(p,Vector3(0,3.19,.07),Vector3(.54,.08,.91),7,Basis(Vector3.RIGHT,-.16),false)
	for x in [-.64,-.38,.38,.64]:box(p,Vector3(x,3.19,.06),Vector3(.055,.065,.85),2,Basis(Vector3.RIGHT,-.16),false)
	box(p,Vector3(0,2.77,.80),Vector3(1.5,.045,.09),5,Basis.IDENTITY,false)
	box(p,Vector3(0,.66,-1.22),Vector3(1.86,1.04,.16),7)
	box(p,Vector3(0,1.12,-1.13),Vector3(1.77,.17,.45),0,Basis(Vector3.RIGHT,-.22))
	box(p,Vector3(0,.22,1.30),Vector3(1.74,.10,.24),3)
	for x in [-.61,0,.61]:box(p,Vector3(x,.228,.40),Vector3(.025,.012,1.30),6,Basis.IDENTITY,false)
	box(p,Vector3(0,-.22,.58),Vector3(.85,.26,.85),4)
	box(p,Vector3(0,-.39,.58),Vector3(.61,.10,.61),5,Basis.IDENTITY,false)
	return p
static func cabin()->ArrayMesh:
	if pod_mesh==null:pod_mesh=mesh(cabin_parts_r14())
	return pod_mesh
static func deck_parts_r14()->Array:
	var p:Array=[]
	box(p,Vector3(0,-.35,0),Vector3(49,.7,18),0)
	box(p,Vector3(0,-.76,0),Vector3(47.8,.23,16.8),4)
	for slot in 12:
		var x:float=(slot-5.5)*3.6
		for z in [-5.9,-1.7,2.5,6.6]:box(p,Vector3(x,.009,z),Vector3(3.42,.018,3.94),6,Basis.IDENTITY,false)
		box(p,Vector3(x,.027,-3.95),Vector3(2.2,.025,.10),3,Basis.IDENTITY,false)
		box(p,Vector3(x,-1.05,0),Vector3(.36,.72,18.3),0)
		for z in [-3.4,-2.4,-1.4]:box(p,Vector3(x,.028,z),Vector3(.16,.025,.44),5,Basis.IDENTITY,false)
		for side in [-1,1]:box(p,Vector3(x+side*1.35,.026,-6),Vector3(.055,.025,3.1),3,Basis.IDENTITY,false)
	for z in [-8.82,8.82]:
		box(p,Vector3(0,-.21,z),Vector3(49.3,.44,.28),2)
		box(p,Vector3(0,-.66,z),Vector3(49.5,.20,.20),1)
	# Monumental angled perimeter framework. Front is entirely open to launch/glide.
	for side in [-1,1]:
		for z in [-1.7,3.35,8.35]:
			beam(p,Vector3(side*24,-.05,z),Vector3(side*21.5,8.7,z),.55,0)
			beam(p,Vector3(side*23.77,.10,z),Vector3(side*21.36,8.66,z),.12,2)
			box(p,Vector3(side*23.7,.85,z),Vector3(.85,1.6,.95),1)
			beam(p,Vector3(side*24,-.9,z),Vector3(side*19,-5.0,z),.48,0)
		# Exit gaps are z[-6.3,-2.8] and [-.1,2.6], no hidden perimeter fence.
		for segment in [[-8.9,-6.3],[-2.8,-.1],[2.6,8.9]]:
			var middle:float=(segment[0]+segment[1])*.5;var length:float=segment[1]-segment[0]
			box(p,Vector3(side*24.1,.55,middle),Vector3(.24,1.10,length),4)
			box(p,Vector3(side*24.0,1.16,middle),Vector3(.19,.12,length),2)
		beam(p,Vector3(side*24,1.35,-1.7),Vector3(side*21.5,8.7,3.35),.30,6)
		beam(p,Vector3(side*24,1.35,8.35),Vector3(side*21.5,8.7,3.35),.30,6)
		beam(p,Vector3(side*21.5,8.7,-1.7),Vector3(side*21.5,8.7,8.5),.48,0)
	# Rear wall: opaque armored base, large open trusses above, two rear exits.
	for segment in [[-24,-15],[-12,12],[15,24]]:
		var x:float=(segment[0]+segment[1])*.5;var width:float=segment[1]-segment[0]
		box(p,Vector3(x,.68,8.73),Vector3(width,1.36,.34),4)
		box(p,Vector3(x,1.42,8.70),Vector3(width,.16,.42),2)
	for x in [-21.5,-10.8,0,10.8,21.5]:
		beam(p,Vector3(x,1.5,8.65),Vector3(x,8.7,8.65),.38,0)
		box(p,Vector3(x,3.4,8.77),Vector3(.16,1.5,.08),5,Basis.IDENTITY,false)
	for x in [-16.15,-5.4,5.4,16.15]:
		beam(p,Vector3(x-5.1,1.6,8.65),Vector3(x+5.1,8.7,8.65),.27,6)
		beam(p,Vector3(x+5.1,1.6,8.65),Vector3(x-5.1,8.7,8.65),.27,6)
	# Canopy ends before z=-2.3, away from every rising pod's flight envelope.
	for z in [-2.0,3.35,8.6]:
		beam(p,Vector3(-21.6,8.7,z),Vector3(0,9.55,z),.48,0)
		beam(p,Vector3(0,9.55,z),Vector3(21.6,8.7,z),.48,0)
		beam(p,Vector3(-21.4,8.25,z),Vector3(21.4,8.25,z),.18,2)
	for side in [-1,1]:
		for lane in 3:
			var x:float=side*(3.55+lane*7.15)
			box(p,Vector3(x,9.36-absf(x)*.039,3.2),Vector3(6.85,.15,10.9),0,Basis(Vector3.FORWARD,side*.039))
			box(p,Vector3(x,9.48-absf(x)*.039,3.2),Vector3(6.5,.08,10.3),6,Basis(Vector3.FORWARD,side*.039),false)
			box(p,Vector3(x,8.96-absf(x)*.039,1.1),Vector3(2.0,.06,.13),5,Basis.IDENTITY,false)
	for x in [-14.3,-7.15,0,7.15,14.3]:box(p,Vector3(x,9.43-absf(x)*.039,3.2),Vector3(.22,.28,11.2),2)
	box(p,Vector3(0,5.5,8.35),Vector3(8.2,1.15,.26),0)
	for side in [-1,1]:
		beam(p,Vector3(0,5.45,8.18),Vector3(side*1.1,5.76,8.18),.09,5,false)
		beam(p,Vector3(side*1.1,5.76,8.18),Vector3(side*2.3,5.45,8.18),.09,5,false)
		for i in 6:box(p,Vector3(side*(2.8+i*.16),5.45,8.18),Vector3(.06,.42,.04),4,Basis.IDENTITY,false)
	box(p,Vector3(0,-1.75,0),Vector3(42,.45,.6),0)
	return p
static func deck()->ArrayMesh:
	if deck_mesh_r14==null:deck_mesh_r14=mesh(deck_parts_r14())
	return deck_mesh_r14
static func label(parent:Node3D,text:String,at:Vector3,size:float=.006)->Label3D:
	var node:=Label3D.new();node.text=text;node.font_size=48;node.pixel_size=size;node.position=at;node.modulate=Color("d8e4de");node.outline_size=6;node.no_depth_test=false;parent.add_child(node);return node
static func collision(parent:Node3D,at:Vector3,size:Vector3):
	var shape:=CollisionShape3D.new();var box_shape:=BoxShape3D.new();box_shape.size=size;shape.shape=box_shape;shape.position=at;parent.add_child(shape)
static func control(parent:Node3D,text:String,flying:bool=false)->Label3D:
	var controls:=Node3D.new();controls.name="PodControlsR18";parent.add_child(controls);parent=controls
	var panel:=MeshInstance3D.new();var screen:=BoxMesh.new();screen.size=Vector3(1.5,.56,.075);panel.mesh=screen;panel.material_override=palette()[4];parent.add_child(panel);panel.position=Vector3(0,.78,-.92)
	var button:=MeshInstance3D.new();var cube:=BoxMesh.new();cube.size=Vector3(.40,.16,.12);button.mesh=cube;button.material_override=palette()[1 if flying else 5];parent.add_child(button);button.position=Vector3(0,.56,-.8)
	for side in [-1,1]:
		var bezel:=MeshInstance3D.new();var trim:=BoxMesh.new();trim.size=Vector3(.055,.50,.08);bezel.mesh=trim;bezel.material_override=palette()[2];parent.add_child(bezel);bezel.position=Vector3(side*.78,.78,-.92)
	return label(parent,text,Vector3(0,.83,-.875),.0025)
static func pod_body(parent:Node3D,moving:bool=false)->PhysicsBody3D:
	var body:PhysicsBody3D=AnimatableBody3D.new() if moving else StaticBody3D.new();body.collision_layer=1;body.collision_mask=0;body.add_to_group("climbable");body.add_to_group("construction_terrain");parent.add_child(body)
	if moving:body.sync_to_physics=false
	for part in cabin_parts_r14():
		if not part.solid:continue
		var shape:=CollisionShape3D.new()
		if part.has("profile"):
			var convex:=ConvexPolygonShape3D.new();convex.points=_vertices(part);shape.shape=convex
		else:
			var cube:=BoxShape3D.new();cube.size=part.size;shape.shape=cube
		shape.transform=Transform3D(part.basis,part.at);body.add_child(shape)
	return body
