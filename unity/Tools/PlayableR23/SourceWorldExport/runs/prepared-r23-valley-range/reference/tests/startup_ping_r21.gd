extends SceneTree
class SessionFixture extends Node:
 var active=true
 var epoch=0
 var rules={"map":"cyber_bird"}
 var players={}
 var poses={}
 var marks={}
 var zone={}
 func me():return 1
 func now():return 100.0
 func allied(_a:int,b:int):return b in [1,2]
var checks=0
var failures:Array=[]
func check(value:bool,label:String):
 checks+=1
 if not value:failures.append(label);print("FAIL ",label)
func _initialize():call_deferred("run")
func run():
 var game=load("res://scripts/main.gd").new();root.add_child(game);game.set_process(false)
 var actor=game.player;actor.set_physics_process(false)
 var boot=game.vertical_combat;boot.set_process(false)
 actor.xr_enabled=true;actor.tracking_available=false;actor.xr_camera.position=Vector3(0,1.65,0)
 var raw:Transform3D=actor.xr_camera.transform;var hands=[actor.left_hand.transform,actor.right_hand.transform]
 check(not boot.poll_boot_r19(1.0) and not actor.vertical_combat_initialized,"absent tracking cannot initialize")
 actor.tracking_available=true
 check(not boot.poll_boot_r19(.1),"first valid sample waits briefly for stable height")
 actor.xr_camera.position.y=1.75;check(not boot.poll_boot_r19(.1),"moving startup height restarts stabilization")
 actor.xr_camera.position.y=1.65;boot.poll_boot_r19(.1);boot.poll_boot_r19(.1)
 check(boot.poll_boot_r19(.1) and actor.vertical_combat_initialized,"startup initializes automatically without gaze/menu")
 check(actor.xr_camera.transform==raw and actor.left_hand.transform==hands[0] and actor.right_hand.transform==hands[1],"runtime tracking poses untouched")
 var baseline:float=actor.vertical_combat_tracking_baseline;var lift:float=actor.vertical_combat_origin_lift
 actor.xr_camera.position.y=1.25;actor.tracking_available=false;boot.poll_boot_r19(.1);actor.tracking_available=true
 check(not boot.poll_boot_r19(1) and actor.vertical_combat_tracking_baseline==baseline and actor.vertical_combat_origin_lift==lift,"tracking return/crouch never repeats boot calibration")
 check(not boot.active and not actor.vertical_combat_initializing,"boot calibration does not open gaze UI or retain pause")
 boot.begin(false);check(boot.active and not boot.boot_pending_r19,"manual reinitialize stays available");boot.cancel()
 var loader=load("res://scripts/map_loading_r18.gd").get_for(game);var token:int=loader.begin()
 loader.update_phase("PREPARING",.99)
 check(loader.text.text=="LOADING MAP" and loader.progress==null,"static loading text with no progress geometry")
 check(loader.get_child_count()==3 and loader.get_child(0).material_override is StandardMaterial3D,"physical R18 material loading veil avoids custom clip-space shader")
 loader.finish(token);check(not loader.active and not loader.visible,"loading completion clears veil")
 actor.xr_enabled=false;actor.global_position=Vector3(0,0,2400);game.front_menu.selected_map="cyber_bird"
 var marks=game.building_input.team_marks;marks.set_physics_process(false)
 marks._create_mark(Vector3(20,1,2400),Vector3.UP,4)
 var icon=marks._visuals[0].get_node("BasePingIcon");check(icon.multiplier_r19==5 and icon.no_depth_test,"target stamp5x and visible through geometry")
 var anchor:Vector3=icon.anchor_r19;var camera=actor.get_view_camera();var reference=-1.0
 for distance in [1.0,10.0,100.0,1000.0,10000.0]:
  camera.global_position=anchor+Vector3(0,0,distance);icon._process(0)
  var projection:float=icon.texture.get_width()*icon.pixel_size*icon.global_basis.x.length()/camera.global_position.distance_to(icon.global_position)
  if reference<0:reference=projection
  check(absf(projection-reference)<.00001,"constant projected marker diameter at"+str(distance)+"m")
  check(icon.global_position.distance_to(anchor)<.0001,"rendered marker remains at actual world point at"+str(distance)+"m")
  check(icon.anchor_r19==anchor,"authoritative marker anchor preserved at"+str(distance)+"m")
 var map=game.network_bridge.tactical;map.set_process(false);map.set_physics_process(false)
 check(map.ping_rows_r19().size()==1 and map.ping_rows_r19()[0].owner_point==actor.global_position,"offline B mark appears on map with own player-line origin")
 var fixture=SessionFixture.new();root.add_child(fixture);fixture.poses[2]={"root":Transform3D(Basis.IDENTITY,Vector3(50,0,2400))};fixture.poses[3]={"root":Transform3D.IDENTITY}
 fixture.marks={1:{"point":Vector3.ZERO,"color":2,"catalog":-1,"until":110.0},2:{"point":Vector3(80,0,2400),"color":7,"catalog":-1,"until":110.0},3:{"point":Vector3.ZERO,"color":1,"catalog":-1,"until":110.0}}
 var old=map.session;map.session=fixture
 var rows:Array=map.ping_rows_r19();check(rows.size()==2,"local immediate mark deduplicates authority; enemy marks excluded")
 check(rows[1].owner_point==fixture.poses[2].root.origin,"ally ping line uses that ally position")
 fixture.epoch=1;check(map.ping_rows_r19()[0].point==Vector3.ZERO,"new epoch rejects stale local mark and uses current authority")
 fixture.marks[2].until=90;check(map.ping_rows_r19().size()==1,"expired network mark excluded")
 map.session=old;fixture.queue_free()
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/STARTUP-PING-R21.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"scope":"Actual Main startup/loading/local ping plus authority-map fixture; XRoff/mock pose, physical headset acceptance separate."},"\t"));f.close()
 game._quitting=true;game.queue_free();for i in 8:await process_frame
 print("STARTUP_PING_R21 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
