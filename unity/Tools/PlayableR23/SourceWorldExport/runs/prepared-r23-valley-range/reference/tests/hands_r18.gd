extends SceneTree
var checks=0
var failures:Array=[]
var actor:Node
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func thin_wall(center:Vector3,width:float,height:float)->StaticBody3D:
 var body=StaticBody3D.new();body.position=center;body.collision_layer=1;body.add_to_group("climbable")
 var shape=CollisionShape3D.new();var mesh=ConcavePolygonShape3D.new();mesh.backface_collision=true
 mesh.set_faces(PackedVector3Array([Vector3(-width/2,0,0),Vector3(width/2,0,0),Vector3(width/2,height,0),Vector3(-width/2,0,0),Vector3(width/2,height,0),Vector3(-width/2,height,0)]));shape.shape=mesh;body.add_child(shape);root.add_child(body);return body
func sample(head_side:float,raw_z:float,wall_z:float,label:String):
 actor.reset_motion();actor.global_position=Vector3(0,0,head_side);actor.xr_origin.position=Vector3.ZERO;actor.xr_camera.position=Vector3(0,1.65,0)
 actor.left_hand.global_position=Vector3(0,1.35,raw_z);var raw:Transform3D=actor.left_hand.transform
 var resolved:Vector3=actor.hand_walls_r2.resolve(actor,actor.left_hand)
 check((resolved.z-wall_z)*(head_side-wall_z)>0,label+" proxy stays on headset side")
 check(absf(resolved.z-wall_z)>=.06,label+" proxy respects hand radius")
 check(actor.left_hand.transform==raw,label+" tracked local controller pose untouched")
 check(not actor.find_grab_surface(resolved).is_empty(),label+" near-side contact is climbable")
 actor._update_grab(actor.left_hand,true)
 check(actor._grabs.has(actor.left_hand),label+" actual grip acquires wall from a passed-through physical hand")
 actor.animate_hand(actor.left_hand,1,0,.02)
 check(actor._hand_visuals[actor.left_hand].global_position.distance_to(resolved)<.001,label+" free glove renders at collision proxy")
 check(actor.left_hand.transform==raw,label+" visual correction never moves tracked pose")
 actor._update_grab(actor.left_hand,false,false)
 check(not actor._grabs.has(actor.left_hand),label+" release removes anchor without stuck grip")
func run():
 actor=preload("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);actor.set_process(false);actor.set_physics_process(false);actor.xr_enabled=true;actor.tracking_available=true;actor.input_enabled=true
 var wall=thin_wall(Vector3(0,0,-1),6,3)
 for i in 3:await physics_frame
 sample(0,-1.6,-1,"thin hollow front")
 sample(-2,-.4,-1,"thin hollow inside")
 # Walk around an edge/teleport to the opposite side: an old proxy must not trap the hand.
 for i in 20:sample(0 if i%2==0 else -2,-1.5 if i%2==0 else -.5,-1,"opposite-side rebase "+str(i))
 wall.free();await physics_frame
 # Empty window aperture stays empty; its surrounding architecture remains grabbable.
 var left=thin_wall(Vector3(-1.5,0,-1),1,3);var right=thin_wall(Vector3(1.5,0,-1),1,3)
 var sill=thin_wall(Vector3(0,0,-1),2,.7);var top=thin_wall(Vector3(0,2.5,-1),2,.5)
 for i in 3:await physics_frame
 actor.reset_motion();actor.global_position=Vector3.ZERO;actor.xr_origin.position=Vector3.ZERO;actor.xr_camera.position=Vector3(0,1.65,0);actor.left_hand.position=Vector3(0,1.4,-1.5)
 var raw:Transform3D=actor.left_hand.transform;var resolved:Vector3=actor.hand_walls_r2.resolve(actor,actor.left_hand)
 check(resolved.distance_to(actor.left_hand.global_position)<.001,"open window hole has no invisible hand wall")
 check(actor.left_hand.transform==raw,"window traversal preserves tracked pose")
 actor.left_hand.position=Vector3(-1.5,1.4,-1.5);resolved=actor.hand_walls_r2.resolve(actor,actor.left_hand)
 check(resolved.z>-.95 and not actor.find_grab_surface(resolved).is_empty(),"window side frame remains a useful climbing surface")
 for body in [left,right,sill,top]:body.free()
 actor.free();for i in 3:await process_frame
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/HANDS-R18.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"scope":"Production hand proxy, glove and grip code against real thin concave collision and open window aperture, simulated tracked poses; XR off, no physical headset claim."},"\t"));f.close()
 print("HANDS_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
