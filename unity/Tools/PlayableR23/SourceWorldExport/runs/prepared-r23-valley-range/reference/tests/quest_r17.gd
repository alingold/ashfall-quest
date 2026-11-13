extends SceneTree
var game:Node
var failures:Array=[]
var checks=0
var facts:Dictionary={}
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func wait_map():
 var deadline=Time.get_ticks_msec()+120000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready:return true
 return false
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game)
 var actor:Node=game.player
 check(not actor.vertical_combat_initialized,"initialization remains manual")
 var measurement:Dictionary=actor.initialize_vertical_combat_system()
 check(not measurement.is_empty() and actor.vertical_combat_initialized,"manual initialization succeeds in main menu")
 var baseline:float=actor.vertical_combat_world_baseline_m
 game.front_menu.selected_map="archive:alfa-aglae-souza";game.front_menu._explore_from_r16("lobby")
 check(await wait_map(),"Alpha READY lobby loads")
 var map:Node=game.active_sandbox_map
 check(actor.vertical_combat_initialized and is_equal_approx(actor.vertical_combat_world_baseline_m,baseline),"initialization and accepted height retained across menu to Alpha")
 check(not actor.has_meta("archive_loading_r17"),"loading physics guard releases at validated floor")
 check(actor.global_position.distance_to(map.warmup_spawn_r8)<2,"native lobby spawn remains selected")
 check(is_equal_approx(actor.get_view_camera().position.y,baseline),"camera baseline retained after loading")
 var panels:Array=[];var batch:Node=map.find_child("PanelBatchesR17",false,false)
 check(batch!=null and batch.source_views>3000,"thousands of panel views batched")
 for body in map.find_children("*","StaticBody3D",true,false):
  if body.has_meta("archive_breakable_r8") and body.batch_r17!=null:panels.append(body)
 var tested:Dictionary={}
 for panel in panels:
  var kind="glass" if panel.glass_r10 else "door"
  if tested.has(kind):continue
  var keys:Array=batch.membership[panel.get_instance_id()];var key:String=keys[0]
  if batch.buckets[key].view==null:continue
  var before:int=batch.buckets[key].view.mesh.get_faces().size()
  panel._apply(true);await process_frame
  var after:int=batch.buckets[key].view.mesh.get_faces().size()
  check(after<before and panel.collision_layer==0 and not panel.view.visible,"batch removes actual "+kind+" visual and collision")
  panel._apply(false);await process_frame
  check(batch.buckets[key].view.mesh.get_faces().size()==before and panel.collision_layer==1,"batch resets "+kind+" without closing other passages")
  tested[kind]=true
  if tested.size()==2:break
 check(tested.size()==2,"real source doors and windows exercised")
 var walls:Array=[]
 for body in map.find_children("*","StaticBody3D",true,false):
  if body.has_meta("alpha_boundary_r17"):walls.append(body)
 check(walls.size()==4,"Alpha four invisible solid boundaries")
 for wall in walls:
  var center:Vector3=wall.global_position;var direction:Vector3=Vector3.RIGHT if wall.get_child(0).shape.size.x<3 else Vector3.BACK
  var query:=PhysicsRayQueryParameters3D.create(center-direction*3,center+direction*3,1)
  var hit:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(query)
  check(not hit.is_empty() and hit.collider==wall,"solid Alpha boundary intercepts collision ray")
 var low:Array=map._meta.bounds_min
 check(not map._outside(map.global_position+Vector3(float(low[0])-15,map.spawn.y-map.global_position.y,0)),"Alpha horizontal edge no longer respawns")
 check(map._outside(map.spawn-Vector3.UP*2000),"Alpha vertical fall recovery retained")
 # Exercise standing support with the real initialized player, rather than only geometric point samples.
 var rates:Array=[];var heals:Array=[];var pad_rows:Array=map.semantics_r14.health_pads
 var selected:Array=[]
 for row in pad_rows:
  var local:Vector3=row.center-map.global_position
  if local.x<float(map._meta.bounds_min[0])+2 or local.x>float(map._meta.bounds_max[0])-2 or local.z<float(map._meta.bounds_min[2])+2 or local.z>float(map._meta.bounds_max[2])-2:continue
  var floor:Vector3=map._floor(row.center+Vector3.UP*.65,2)
  if floor.is_finite() and map.semantics_r14.healing_rate_at(floor+Vector3.UP*.12)>0:selected.append(row)
  if selected.size()==5:break
 for row in selected:
  var floor:Vector3=map._floor(row.center+Vector3.UP*.65,2)
  actor.global_position=floor+Vector3.UP*.15;actor.reset_motion();actor.input_enabled=true
  game.health=20;game.shield=0;game.damage_timer=100
  for i in 45:await physics_frame
  var rate:float=map.semantics_r14.healing_rate_at(actor.get_feet_position_r16())
  rates.append({"pad":str(row.center-map.global_position),"feet":str(actor.get_feet_position_r16()-map.global_position),"rate":rate,"hp":game.health})
  check(rate>0 and game.health>22,"actual settled actor restores HP on native pad")
  check(map.semantics_r14.healing_rate_at(row.center+Vector3.UP*4)==0,"pad cannot heal another storey")
 facts.standing_health=rates
 actor.set_physics_process(false)
 check(int(map.statistics.recovered_cache_sites_r17)>0,"split red rings contribute additional actual cache sites")
 var sem:Node=map.semantics_r14;var target:=Vector3.INF
 for local in sem.weapon_sites_r17:
  var at:Vector3=map._floor(map.global_position+local+Vector3.UP*1.1,4)
  if not at.is_finite():continue
  for i in map.gameplay_r8.sites.size():
   if map.gameplay_r8.sites[i].distance_to(at+Vector3.UP*.65)<.1 and not map.gameplay_r8.active_sites.has(i):target=map.gameplay_r8.sites[i];break
  if target.is_finite():break
 map.gameplay_r8.activate_near([target])
 var found=false
 for i in map.gameplay_r8.cache_ids:
  if map.gameplay_r8.sites[i].distance_to(target)<.1:
   var ids:Array=map.gameplay_r8.cache_ids[i];found=ids.size()==6
   for id in ids:found=found and game.weapon_inventory.pickups.has(id)
 check(found,"recovered source hexagon provides six actual weapon pickups")
 # XR runtime poses and crouch are preserved by restoration; no automatic reinitialization.
 var old_camera:Transform3D=actor.xr_camera.transform;actor.xr_enabled=true;actor.tracking_available=true;actor.xr_camera.position.y=1.65
 actor.initialize_vertical_combat_system();var raw:Transform3D=actor.xr_camera.transform;var lift:float=actor.vertical_combat_origin_lift
 actor.xr_origin.position.y-=.7;actor.restore_calibration_r17()
 check(is_equal_approx(actor.xr_origin.position.y,lift) and actor.xr_camera.transform==raw,"restore origin lift without rewriting tracked camera pose")
 actor.xr_camera.position.y-=.1;actor.restore_calibration_r17()
 check(is_equal_approx(actor.get_view_camera().global_position.y-actor.global_position.y,actor.vertical_combat_world_baseline_m-.1),"physical crouch remains one to one after restoration")
 actor.xr_enabled=false;actor.xr_camera.transform=old_camera;actor.restore_calibration_r17()
 actor.walk_speed_multiplier=5;check(is_equal_approx(actor.effective_walk_speed(),actor.WALK_SPEED*5),"personal walk multiplier reaches five")
 check(preload("res://scripts/net_rules1000.gd").LIMITS.walk[1]==5,"host authority permits five times walk")
 var holder=Node3D.new();root.add_child(holder);var panel=preload("res://scripts/ammo_panel.gd").new();holder.add_child(panel);var label=Label3D.new();holder.add_child(label);panel.setup(label)
 panel.update_display(30,1000000,false,actor.get_view_camera());check(panel.rows[1].text=="-" and panel.rows[0].text=="30","infinite ammo uses short dash below finite magazine")
 panel.update_display(12,91,false,actor.get_view_camera());check(panel.rows[1].text=="91","finite ammo count remains exact");holder.queue_free()
 check(game.radio_audio==null,"no story voice player exists")
 facts.render=map.statistics.get("panel_batches_r17",{});facts.textures=map.statistics.get("mobile_textures_r17",{});facts.caches=map.statistics.get("recovered_cache_sites_r17",0)
 var ids:Array=[]
 for node in map.find_children("*","Node",true,false):ids.append(node.get_instance_id())
 actor.set_physics_process(true);game._return_home0992()
 for i in 8:await process_frame
 var retained=0
 for id in ids:
  if is_instance_id_valid(id):retained+=1
 check(retained==0,"all map-owned nodes including batches unload")
 check(actor.vertical_combat_initialized,"return to menu retains initialized state")
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/QUEST-R17.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"Offline XR-off real runtime and simulated tracked poses. Human Quest rendering/healing/calibration acceptance remains pending."},"	"));f.close()
 print("QUEST_R17 checks=",checks," failures=",failures.size())
 game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
