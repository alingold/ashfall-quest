extends SceneTree
var checks=0
var failures:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func ray(at:Vector3)->Dictionary:return root.world_3d.direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(at+Vector3(0,0,.5),at-Vector3(0,0,.5),1))
func run():
	var map:=Node3D.new();root.add_child(map);var geometry:=Node3D.new();map.add_child(geometry)
	var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	for point in [Vector3(-7,0,.12),Vector3(7,0,.12),Vector3(7,3,.12),Vector3(-7,0,.12),Vector3(7,3,.12),Vector3(-7,3,.12)]:
		tool.set_normal(Vector3.FORWARD);tool.set_uv(Vector2(point.x,point.y));tool.set_color(Color(.3,.4,.5));tool.add_vertex(point)
	var view:=MeshInstance3D.new();view.mesh=tool.commit();var material:=StandardMaterial3D.new();material.resource_name="vc";material.vertex_color_use_as_albedo=true;view.material_override=material;geometry.add_child(view)
	var body:=StaticBody3D.new();body.collision_layer=1;geometry.add_child(body);var shape:=CollisionShape3D.new();var original:=ConcavePolygonShape3D.new();original.backface_collision=true;original.set_faces(view.mesh.get_faces());shape.shape=original;body.add_child(shape)
	var leaf:=SurfaceTool.new();leaf.begin(Mesh.PRIMITIVE_TRIANGLES)
	for point in [Vector3(-.65,.05,0),Vector3(.65,.05,0),Vector3(.65,2.7,0),Vector3(-.65,.05,0),Vector3(.65,2.7,0),Vector3(-.65,2.7,0)]:leaf.add_vertex(point)
	var mesh:ArrayMesh=leaf.commit();var row={"glass":false,"box":mesh.get_aabb(),"mesh":mesh,"material":material,"faces":mesh.get_faces()}
	var result:Dictionary=preload("res://scripts/archive_passage_cut_r9.gd").apply(geometry,[row]);check(result.triangles==2,"both welded wall triangles are split");check(row.get("attachments",[]).size()==1,"cut wall fragments belong to one passage assembly")
	var panel:=preload("res://scripts/archive_panel_r8.gd").new();map.add_child(panel);panel.setup(map,"test",row);panel.set_physics_process(false)
	await physics_frame;await physics_frame
	for x in [-.25,0,.25]:check(not ray(Vector3(x,1.3,0)).is_empty(),"intact combined wall remains solid "+str(x))
	panel._apply(true);await physics_frame;await physics_frame
	for x in [-.25,0,.25]:
		for y in [.5,1.3,2.1]:check(ray(Vector3(x,y,0)).is_empty(),"broken passage is clear across aperture "+str(Vector2(x,y)))
	for x in [-6,-2,2,6]:check(not ray(Vector3(x,1.3,0)).is_empty(),"surrounding wall preserved "+str(x))
	for y in [.02,2.9]:check(not ray(Vector3(0,y,0)).is_empty(),"wall below/above aperture preserved "+str(y))
	var query:=PhysicsShapeQueryParameters3D.new();var capsule:=CapsuleShape3D.new();capsule.radius=.3;capsule.height=1.7;query.shape=capsule;query.transform=Transform3D(Basis.IDENTITY,Vector3(0,1.3,0));query.collision_mask=1;check(root.world_3d.direct_space_state.intersect_shape(query).is_empty(),"full player capsule fits through broken opening")
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/PASSAGE-CUT-R9.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"geometry":result,"scope":"real split render and physics geometry; intact surface, open player capsule and retained wall outside doorway; XR off"},"\t"));file.close();print("PASSAGE_CUT_R9 checks=",checks," failures=",failures.size())
	await create_timer(1.5).timeout;map.queue_free();for i in 4:await process_frame
	call_deferred("quit",0 if failures.is_empty() else 1)
