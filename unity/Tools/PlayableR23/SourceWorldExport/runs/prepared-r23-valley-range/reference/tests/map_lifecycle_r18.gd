extends SceneTree
var game:Node
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func settled():
 for i in 12:await process_frame
func wait_loaded()->bool:
 var deadline:int=Time.get_ticks_msec()+120000
 var loader:Node=game.get_node("MapLoadingR18")
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if not loader.active and is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready:return true
 return false
func snapshot(label:String)->Dictionary:
 return {"label":label,"nodes":int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)),"orphans":int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)),"resources":int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT)),"static_bytes":OS.get_static_memory_usage(),"pickups":game.weapon_inventory.pickups.size(),"instances":game.weapon.weapons.size()}
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game);game.player.set_physics_process(false)
 var registry=preload("res://scripts/cyber_bird_registration.gd")
 var loader=preload("res://scripts/map_loading_r18.gd").get_for(game)
 var baseline_nodes=0;var baseline_orphans=0
 await settled() # Match the real selector: let initial UI resources finish before background map I/O.
 # Cancel before instantiation and while the loader owns an asynchronous task.
 var token:int=loader.begin()
 loader.load_resource_r18("res://local_archive_r10/alfa-aglae-souza.res",token)
 check(loader.requests_r18.size()==1,"threaded load owned before cancellation")
 loader.cancel()
 var deadline:int=Time.get_ticks_msec()+120000
 while not loader.requests_r18.is_empty() and Time.get_ticks_msec()<deadline:await process_frame
 check(loader.requests_r18.is_empty(),"cancelled threaded request is reaped")
 check(not loader.visible and not loader.active and not game.player.has_meta("map_loading_r18"),"cancel clears loading view and physics guard")
 check(game.active_sandbox_map==null,"cancelled resource request never instantiates a map")
 for cycle in 6:
  var slug:String=["alfa-aglae-souza","afterlife-gods-alamo-hank","battlefield-paintball-gods-pitdevil"][cycle%3]
  game.front_menu.selected_map="archive:"+slug;game.front_menu._explore_from_r16("lobby")
  check(loader.visible and loader.active,"entry starts visible feedback cycle "+str(cycle))
  check(await wait_loaded(),"map completes cycle "+str(cycle))
  var map:Node=game.active_sandbox_map;var ids:Array=[]
  print("LOOT_R18 ",slug," sites=",map.gameplay_r8.sites.size()," supplemental=",map.statistics.get("supplemental_cache_sites_r18",0))
  for child in map.find_children("*","Node",true,false):ids.append(child.get_instance_id())
  ids.append(map.get_instance_id())
  check(loader.requests_r18.is_empty() and not game.player.has_meta("map_loading_r18"),"load request and guard released cycle "+str(cycle))
  var map_weak:WeakRef=weakref(map);map=null
  map_weak.get_ref().refresh_loot() # Exercise deferred refresh on a map being retired this frame.
  game._return_home0992();await settled()
  var remaining=0
  for id in ids:
   if is_instance_id_valid(id):remaining+=1
  check(remaining==0 and map_weak.get_ref()==null,"all previous map nodes retired cycle "+str(cycle))
  check(game.active_sandbox_map==null and game.weapon_inventory.pickups.is_empty() and game.weapon.weapons.size()==30,"map loot and transient weapon instances retired cycle "+str(cycle))
  var state:Dictionary=snapshot(slug+":"+str(cycle));facts.append(state);print("LIFECYCLE_R18 ",JSON.stringify(state))
  if cycle==0:baseline_nodes=state.nodes;baseline_orphans=state.orphans
  else:
   check(state.nodes<=baseline_nodes+5,"menu node count remains bounded cycle "+str(cycle))
   check(state.orphans<=baseline_orphans+2,"orphan node count remains bounded cycle "+str(cycle))
 game._quitting=true;game.queue_free();await settled()
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/MAP-LIFECYCLE-R18.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"XR-off repeated real READY/menu load/unload, task cancellation and object ownership. Memory counters are offline process measurements, not Quest performance."},"\t"));file.close()
 print("MAP_LIFECYCLE_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
