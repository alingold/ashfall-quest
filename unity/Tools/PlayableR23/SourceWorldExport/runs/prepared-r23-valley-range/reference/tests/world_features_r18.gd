extends SceneTree
class ZoneFixture extends Node:
 var active=true
 var stage="match"
 var dedicated=false
 var zone:Dictionary={}
 var rules:Dictionary={}
 func now():return Time.get_ticks_msec()*.001
var game:Node
var checks=0
var failures:Array=[]
var facts:Dictionary={}
var output=""
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func load_map(slug:String)->Node:
 if not game.front_end_active:game._return_home0992()
 for i in 4:await process_frame
 game.front_menu.selected_map="archive:"+slug;game.front_menu._explore_from_r16("game")
 var deadline:int=Time.get_ticks_msec()+120000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready and not game.get_node("MapLoadingR18").active:return game.active_sandbox_map
 return null
func shot(title:String,at:Vector3,target:Vector3):
 if DisplayServer.get_name()=="headless":return
 var camera:Camera3D=game.player.desktop_camera;camera.global_position=at;camera.look_at(target)
 for i in 4:game.weapon_inventory._stream_visuals_r8()
 await create_timer(.4).timeout
 for i in 8:await process_frame
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png(output+"/R18-"+title+".png")
 print("R18_CAPTURE ",title)
func run():
 output=OS.get_cmdline_user_args()[0];game=load("res://scripts/main.gd").new();root.add_child(game);game.player.set_physics_process(false)
 var map:Node=await load_map("alfa-aglae-souza")
 check(map!=null,"Alpha loads with combined render/art/fixture passes")
 check(map.statistics.tower_grates_r18.get("appended_panels",0)==8,"all eight native tower grates integrated after art/markers")
 check(map.statistics.art_r17.get("guard_skips",-1)==0,"all original art chunk guards remain valid alongside grates")
 var grates:Array=[]
 for panel in map.find_children("*","StaticBody3D",true,false):
  if panel.get("sound_r18")=="res://assets/audio/sfx/r18/metal_grate.wav":grates.append(panel)
 check(grates.size()==8,"metal grate panels use distinct sound hook")
 if not grates.is_empty():
  var panel:Node=grates[0];var center:Vector3=panel.to_global(panel.box.get_center())
  game.player.global_position=center-Vector3.UP*.9;game.player.reset_motion();game.player.input_enabled=true
  panel._physics_process(.2)
  check(panel._broken and panel.collision_layer==0,"walking into actual runtime grate breaks its collision")
  panel._apply(false);panel.take_damage(1)
  check(panel._broken and panel.collision_layer==0,"shooting actual runtime grate breaks it")
 var health:Dictionary={};var stand:=Vector3.INF
 for row in map.semantics_r14.health_pads:
  var floor:Vector3=map._floor(row.center+Vector3.UP*.7,2)
  if floor.is_finite() and not map.semantics_r14.stations_r18.station_at(floor+Vector3.UP*.12).is_empty():health=row;stand=floor+Vector3.UP*.12;break
 check(stand.is_finite(),"station has reachable native floor")
 if stand.is_finite():
  game.player.global_position=stand;game.player.desktop_camera.position=Vector3(0,game.player.get_standing_eye_height(),0);game.player.input_enabled=true;game.health=20;game.shield=0;game.damage_timer=100
  var stations:Node=map.semantics_r14.stations_r18;stations._physics_process(.11)
  check(game.health==100 and game.shield==100 and stations.ready_at.has(health.station_key_r18),"offline station instantly restores both pools")
  var i:int=map.semantics_r14.health_pads.find(health)
  check(stations.rings.instance_count==map.semantics_r14.health_pads.size() and stations.rings.get_instance_custom_data(i).r>.99,"shared instance ring receives full recharge fraction")
  print("STATION_LABEL_R18 visible=",stations.labels[i].visible," text=",stations.labels[i].text," distance=",stations.labels[i].global_position.distance_to(game.player.get_view_camera().global_position))
  check(stations.labels[i].visible and stations.labels[i].text.to_int()>0 and stations.labels[i].text.to_int()<=15,"cross timer visible during recharge")
  game.damage_timer=0
  await shot("healing-station-recharge",health.center+Vector3(0,1.5,3.3),health.center+Vector3.UP*.8)
 var fake=ZoneFixture.new();root.add_child(fake);fake.rules={"map":map.map_id}
 var rect=Rect2(Vector2(-450,-130),Vector2(300,300));fake.zone={"from":rect,"next":rect.grow(-50),"hold_until":fake.now()+100,"ends":fake.now()+200,"index":0,"final":false}
 var wall=load("res://scripts/zone_danger_r5.gd").new();root.add_child(wall);wall.setup(game,fake);wall._process(0)
 check(wall.visible and wall.walls.size()==4,"four visible translucent current-zone walls")
 check(wall.walls[0].mesh.size.y>600 and wall.find_children("*","CollisionObject3D",true,false).is_empty(),"zone spans sky height without invisible collision")
 await shot("zone-sky-wall",map.global_position+Vector3(-360,85,-220),map.global_position+Vector3(-250,85,-70))
 wall.queue_free();fake.queue_free();await process_frame
 facts.alpha={"grates":grates.size(),"art":map.statistics.art_r17,"health_stations":map.semantics_r14.health_pads.size()}
 map=await load_map("afterlife-gods-alamo-hank")
 check(map!=null,"Afterlife loads")
 check(int(map.statistics.get("supplemental_cache_sites_r18",0))>=100 and map.gameplay_r8.sites.size()>150,"Afterlife has broadly distributed supplemental caches")
 var site_index:int=map.gameplay_r8.sites.size()-1;var point:Vector3=map.gameplay_r8.sites[site_index]
 for i in 40:
  map.gameplay_r8.activate_near([point])
  if map.gameplay_r8.cache_ids.has(site_index):break
 check(map.gameplay_r8.cache_ids.has(site_index),"supplemental cache activates near player")
 if map.gameplay_r8.cache_ids.has(site_index):
  var ids:Array=map.gameplay_r8.cache_ids[site_index];var actual=ids.size()==6
  for id in ids:actual=actual and game.weapon_inventory.pickups.has(id)
  check(actual,"supplemental cache has six actual represented pickups")
 facts.afterlife={"sites":map.gameplay_r8.sites.size(),"supplemental":map.statistics.supplemental_cache_sites_r18}
 map=await load_map("melipop-gods-poplightning")
 check(map!=null,"MellyPop loads")
 var rack:Node=map.gameplay_r8.get_node("WeaponWallRacksR18");var count:int=game.weapon.ACTIVE_WEAPON_INDICES.filter(func(id):return preload("res://scripts/weapon_rarity_r6.gd").tiered(id)).size()
 for board in rack.boards_r18:rack.activate_near_r18([map.global_position+board.box.get_center()])
 check(rack.ids.size()==count*rack.anchors_r18.size() and rack.anchors_r18.size()>=2,"one of each gun mounted at each floor-validated original weapon sign")
 var red=true;var tangent=true;var clear=true;var settled=true
 for id in rack.ids:
  var prop:Node3D=game.weapon_inventory.pickups[id];var normal:Vector3=-prop.global_basis.x
  red=red and preload("res://scripts/weapon_rarity_r6.gd").level(game.weapon.weapons[id])==5
  tangent=tangent and absf(prop.global_basis.z.dot(normal))<.001 and prop.global_basis.y.dot(Vector3.UP)>.999
  clear=clear and game.weapon_inventory._line_clear(prop.global_position+normal*.8,prop.global_position)
  var approach:Vector3=map._floor(prop.global_position+normal*.8+Vector3.UP*.5,5)
  clear=clear and approach.is_finite() and map.safe_spawn_r8(approach+Vector3.UP*.12) and game.weapon_inventory._line_clear(approach+Vector3.UP*1.5,prop.global_position)
  var before:Transform3D=prop.global_transform;game.weapon_inventory._settle_on_surface(prop);settled=settled and prop.global_transform==before
 check(red,"all board guns are red rarity")
 check(tangent,"gun muzzle axes follow wall tangent and remain upright")
 check(clear,"wall gun grip origins can be reached from in front")
 check(settled,"mounts stay attached instead of falling during deferred settling")
 var first:Node3D=game.weapon_inventory.pickups[rack.ids[0]];var outward:Vector3=-first.global_basis.x
 var anchor:Dictionary=rack.anchors_r18[0];var view_at:Vector3=anchor.center+outward*3+Vector3.UP*1.5
 await shot("red-wall-mounted-guns",view_at,anchor.center+Vector3.UP*1.35)
 var id:int=rack.ids[0];game.weapon_inventory.capacity=6
 check(game.weapon_inventory.pickup_weapon(id) and game.weapon_inventory.owned.has(id),"real mounted gun enters carried inventory")
 check(not game.weapon_inventory.pickups.has(id),"pickup retires represented wall prop")
 facts.racks={"gun_types":count,"mounted":rack.ids.size(),"boards":map.gameplay_r8._weapon_boards_r9.size()}
 map=await load_map("aftermath-light-vs-dark-gods-badbar")
 check(map!=null,"Aftermath loads")
 var paths:Array=map._geometry.find_children("AftermathSafePathR18*","StaticBody3D",true,false)
 check(paths.size()>50,"winding source ground hazards become real support")
 var supported=0;var harmless=0
 for body in paths:
  var collision:CollisionShape3D=body.get_child(0);var box:AABB=collision.global_transform*collision.shape.get_debug_mesh().get_aabb();var top:Vector3=box.get_center();top.y=box.end.y+.5
  var hit:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(top,top-Vector3.UP*(box.size.y+2),1))
  if not hit.is_empty():
   supported+=1
   if not map._hazard(hit.position+Vector3.UP*.8):harmless+=1
 check(supported>50 and harmless==supported,"converted walkable paths support player without hazard respawn")
 facts.aftermath={"converted_shapes":paths.size(),"reported_converted":map.statistics.get("after_math_safe_paths_r18",0),"supported_probes":supported,"harmless_probes":harmless}
 var loader:Node=game.get_node("MapLoadingR18");game._return_home0992();for i in 6:await process_frame
 check(not loader.active and not game.player.has_meta("map_loading_r18"),"final return clears loading guard")
 var f=FileAccess.open(output+"/WORLD-FEATURES-R18.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"Actual map gameplay/render integration, native PC captures when available. XR off; physical Quest acceptance separate."},"\t"));f.close()
 game._quitting=true;game.queue_free();for i in 6:await process_frame
 print("WORLD_FEATURES_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
