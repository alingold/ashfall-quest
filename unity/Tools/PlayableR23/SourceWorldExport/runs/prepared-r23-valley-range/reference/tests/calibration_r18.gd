extends SceneTree
var game:Node
var actor:Node
var net:Node
var checks=0
var failures:Array=[]
var facts:Dictionary={}
var raw:Transform3D
var hands:Array=[]
var baseline:float
var lift:float
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func wait_stage(stage:String)->bool:
 var deadline:int=Time.get_ticks_msec()+120000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if net.stage==stage and net.world_ready and game.network_bridge.playing():
   for i in 3:await process_frame
   return true
 return false
func same_height(label:String):
 check(actor.vertical_combat_initialized and is_equal_approx(actor.vertical_combat_world_baseline_m,baseline),label+" initialized baseline")
 check(is_equal_approx(actor.xr_origin.position.y,lift),label+" calibrated origin lift")
 check(actor.xr_camera.transform==raw and actor.left_hand.transform==hands[0] and actor.right_hand.transform==hands[1],label+" tracked local poses untouched")
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game);actor=game.player;net=game.network_bridge.net
 actor.set_physics_process(false);actor.xr_enabled=true;actor.tracking_available=true
 actor.xr_camera.position=Vector3(0,1.65,0);actor.left_hand.position=Vector3(-.3,1.2,-.3);actor.right_hand.position=Vector3(.3,1.2,-.3)
 check(not actor.vertical_combat_initialized,"initialization remains manual")
 var gaze:Node=game.vertical_combat;gaze.set_process(false);gaze.begin(false);gaze._place_target()
 var eye:Vector3=actor.get_view_camera().global_position
 gaze.advance_gaze(1.30,eye,(gaze._panel.global_position-eye).normalized())
 check(gaze.state=="complete" and actor.vertical_combat_initialized,"actual gaze controller completes manual initialization")
 gaze._process(.60);baseline=actor.vertical_combat_world_baseline_m;lift=actor.vertical_combat_origin_lift
 raw=actor.xr_camera.transform;hands=[actor.left_hand.transform,actor.right_hand.transform]
 check(not gaze.active and not actor.vertical_combat_initializing,"gaze initialization returns normal input state")
 game.front_menu.selected_map="archive:alfa-aglae-souza";game.front_menu._explore_from_r16("lobby")
 var loader:Node=game.get_node("MapLoadingR18")
 check(loader.active and loader.visible and actor.has_meta("map_loading_r18"),"loading feedback and guard exist before heavy map work")
 var deadline:int=Time.get_ticks_msec()+120000
 while (not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready or loader.active) and Time.get_ticks_msec()<deadline:await process_frame
 check(is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not loader.active,"READY completes and clears loading feedback")
 same_height("READY Alpha lobby")
 check(is_equal_approx(actor.walk_speed_multiplier,2.5) and is_equal_approx(actor.fling_power,3),"READY applies requested all-map walk/fling defaults")
 game._return_home0992();for i in 4:await process_frame
 same_height("return to menu")
 check(net.host("R18 calibration",25428,"isolated")==OK,"isolated actual host")
 net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"revive","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0})
 check(await wait_stage("lobby"),"hosted Alpha warmup")
 same_height("host warmup")
 check(is_equal_approx(float(net.rules.walk),2.5) and is_equal_approx(float(net.rules.fling),3),"host applies same requested map defaults")
 net.request("start");check(await wait_stage("match"),"host starts actual team revive match")
 same_height("match sky platform")
 var launch:Node=game.network_bridge.launch_r12
 check(launch.ensure_layout() and launch.stations.size()==2,"two team launch platforms exist")
 var cabin:Transform3D=launch.pod_pose(1)
 net._relocate_r12(1,cabin.origin+Vector3.UP*.12);net._publish();game.network_bridge._sync_player()
 var plan:Dictionary=launch.plan(1,{"pod":net.players[1].pod_key_r12})
 check(not plan.is_empty(),"actual pod planner finds ground destination")
 if not plan.is_empty():
  check(plan.end.y<plan.cruise.y-10 and plan.end.distance_to(plan.ground_target)<.01,"pod continues below cruise to ground support")
  var floor:Vector3=game.active_sandbox_map._floor(plan.end+Vector3.UP*.3,1)
  check(floor.is_finite() and absf(plan.end.y-floor.y-.12)<.01,"pod endpoint lands on real source surface")
  check(net._pod_launch_r12(1,{"pod":plan.pod}),"host accepts actual cabin launch")
  var ride:Dictionary=net.pods_r12[1]
  ride.time=net.now()-ride.duration*.5;game.network_bridge._sync_player();launch._physics_process(.02)
  same_height("pod transit")
  check(actor.pod_carried_r12,"player is carried by active pod")
  ride.time=net.now()-ride.duration-.01;net._step_pods_r12();game.network_bridge._sync_player();launch._physics_process(.02)
  same_height("pod landing")
  check(ride.state=="landed" and not actor.pod_carried_r12,"arrival releases player at ground instead of midair")
  launch._refresh_flights();launch._refresh_flights()
  check(launch.flights.has(1) and is_instance_valid(launch.flights[1]),"landed pod remains after subsequent visual refreshes")
  if launch.flights.has(1):check(not launch.flights[1].get_node("PodControlsR18").visible,"landed pod hides eject panel")
 net.request("restart");check(await wait_stage("match"),"host restarts same map")
 same_height("same-map rematch")
 net.request("home");check(await wait_stage("lobby"),"host returns to warmup")
 same_height("match to warmup")
 game.network_bridge.leave_session();for i in 5:await process_frame
 same_height("leave to main menu")
 # Repeat the real tracked-head sweep under a low ceiling, then walk out.
 # A collision correction must constrain the current head without becoming a new calibration.
 game.network_bridge.set_process(false);game.set_process(false)
 var fixture=Node3D.new();root.add_child(fixture);fixture.position=Vector3(9000,2000,9000)
 var roof=StaticBody3D.new();fixture.add_child(roof);roof.position=Vector3(0,1.65,0)
 var shape=CollisionShape3D.new();var box=BoxShape3D.new();box.size=Vector3(3,.2,6);shape.shape=box;roof.add_child(shape)
 for i in 3:await physics_frame
 for target_eye in [baseline,2.15]:
  actor.vertical_combat_world_baseline_m=target_eye;actor.vertical_combat_origin_lift=target_eye-1.65
  actor.global_position=fixture.global_position;actor.xr_origin.position=Vector3(0,1.35-1.65,0);actor._last_head=fixture.global_position+Vector3.UP*1.35
  var safe=true;var constrained=0
  for step in 101:
   actor.global_position.x=fixture.global_position.x+step*.03
   actor._apply_vertical_combat_ceiling();actor._sync_body_to_head()
   if actor.is_head_position_obstructed(actor.xr_camera.global_position):safe=false
   if actor.xr_camera.global_position.y-fixture.global_position.y<target_eye-.05:constrained+=1
  check(safe,"swept calibrated head stays out of low ceiling at eye "+str(target_eye))
  check(constrained>30,"low ceiling genuinely constrained head at eye "+str(target_eye))
  check(absf(actor.xr_camera.global_position.y-actor.global_position.y-target_eye)<.001,"full calibrated height returns after ceiling exit at eye "+str(target_eye))
  check(actor.xr_camera.transform==raw,"ceiling correction preserves physical tracked camera")
  check(actor.standing_clearance_r18()>=target_eye+actor.AUTHORED_HEAD_TOP_TO_EYE_M,"spawn clearance includes calibrated head top")
 facts={"manual_gaze":true,"default_eye":baseline,"default_lift":lift,"calibration_transition_checks":checks,"scope":"XR-off real main/host/pod flow with simulated tracked local poses; no headset acceptance"}
 fixture.queue_free();game._quitting=true;game.queue_free();for i in 5:await process_frame
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/CALIBRATION-R18.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts},"\t"));f.close()
 print("CALIBRATION_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
