extends SceneTree
var game:Node
var net:Node
var role=""
var output=""
var checks=0
var failures:Array=[]
var facts:Dictionary={}
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",role," ",label)
func wait_for(test:Callable,seconds:float=70)->bool:
 var deadline:int=Time.get_ticks_msec()+int(seconds*1000)
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if test.call():return true
 return false
func mark(color:int):net.request("mark",{"point":game.player.global_position,"normal":Vector3.UP,"color":color})
func barrier(color:int,count:int)->bool:
 if net.players.size()!=count:return false
 for id in net.players:
  if not net.marks.has(id) or int(net.marks[id].color)<color:return false
 return true
func run():
 var args=OS.get_cmdline_user_args();output=args[0];var port=25448
 for arg in args:
  if arg.begins_with("--role="):role=arg.trim_prefix("--role=")
 game=load("res://scripts/main.gd").new();root.add_child(game);net=game.network_bridge.net
 if role=="host":
  check(net.host("Station host",port,"r18-station")==OK,"real listen host")
  net.request("rules",{"map":"archive:alfa-aglae-souza","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0})
 else:check(net.join("127.0.0.1",role,port,"r18-station")==OK,"real ENet join")
 check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not game.network_bridge._preparing),"actual Alpha warmup loaded")
 game.player.set_physics_process(false)
 if role!="late":mark(1)
 var key=""
 if net.server:
  check(await wait_for(func():return barrier(1,2)),"two peers ready before shared station use")
  var map:Node=game.active_sandbox_map;var stand:=Vector3.INF
  for row in map.semantics_r14.health_pads:
   var floor:Vector3=map._floor(row.center+Vector3.UP*.7,2)
   if floor.is_finite() and not map.semantics_r14.stations_r18.station_at(floor+Vector3.UP*.12).is_empty():stand=floor+Vector3.UP*.12;break
  check(stand.is_finite(),"physical native station contact")
  var other:int=net.players.keys().filter(func(id):return id!=1)[0]
  for id in [1,other]:net._relocate_r12(id,stand);net.players[id].health=40.0;net.players[id].shield=10.0
  net.station_ready_r18.clear();net._publish()
  check(await wait_for(func():return not net.station_ready_r18.is_empty(),4),"authority restores first user")
  key=str(net.station_ready_r18.keys()[0]);facts.station_key=key
  check(net.players[1].health==100 and net.players[1].shield==100,"first user instant health and shield")
  check(net.players[other].health==40 and net.players[other].shield==10,"same station cannot simultaneously restore second user")
  check(float(net.station_ready_r18[key])-net.now()>14,"actual station starts15second recharge")
  FileAccess.open(output+"/STATION-READY-R18.json",FileAccess.WRITE).store_string(JSON.stringify({"key":key,"time":net.now()}))
  mark(2)
 else:
  check(await wait_for(func():return not net.station_ready_r18.is_empty()),"shared recharge replicated")
  key=str(net.station_ready_r18.keys()[0]);facts.station_key=key
  check(float(net.station_ready_r18[key])-net.now()>0,"replicated timer is still recharging")
  check(net.players[1].health==100 and net.players[1].shield==100,"restored health/shield replicated")
  if role!="late":check(net.players[net.me()].health==40 and net.players[net.me()].shield==10,"second user remains damaged during shared cooldown")
  else:check(net.players.size()==3,"late join receives current full station snapshot")
  mark(2)
 check(await wait_for(func():return barrier(2,3)),"late join confirms shared timer")
 if net.server:
  # Advance only the deterministic station fixture clock; real default duration was checked above.
  net.station_ready_r18[key]=net.now()-.1;net._publish()
 check(await wait_for(func():
  for p in net.players.values():
   if p.health<100 or p.shield<100:return false
  return true),"second user restores after shared recharge")
 mark(3)
 if net.server:
  check(await wait_for(func():return barrier(3,3)),"all peers observe recharge use")
  net.players[1].life="downed";net.players[1].health=0;net.players[1].shield=0;net.station_ready_r18.clear();net._publish()
  await create_timer(.35).timeout
  check(net.players[1].health==0 and net.players[1].shield==0 and net.station_ready_r18.is_empty(),"ghost cannot restore; full users do not consume station")
  net.request("home")
 check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not game.network_bridge._preparing and net.station_ready_r18.is_empty() and net.players[1].life=="alive"),"round reset clears shared station cooldown")
 mark(4)
 if net.server:
  check(await wait_for(func():return barrier(4,3)),"all peers finish station reset")
  mark(5)
 else:check(await wait_for(func():return net.marks.has(1) and int(net.marks[1].color)>=5),"host acknowledges complete peer checks")
 facts.scope="Actual Alpha listen host+two ENet clients, late join, station authority/shared cooldown/reset. XR off; no headset/WAN acceptance."
 var f=FileAccess.open(output+"/STATION-PEERS-R18-"+role+".json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts},"\t"));f.close()
 if net.server:await create_timer(.7).timeout
 net.close(false);game._quitting=true;game.queue_free();for i in 6:await process_frame
 print("STATION_PEERS_R18 ",role," checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
