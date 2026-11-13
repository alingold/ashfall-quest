extends SceneTree
## Shared helpers for R18 real-Main multi-process ENet peers.
## User args: <role> <receipt.json> <port> <peer_count> [ordinary game flags]
const SyncR18=preload("res://tests/r18net/sync_r18.gd")
var game:Node
var net:Node
var bridge:Node
var sync:Node
var role:String
var receipt:String
var port:int
var peer_count:int
var checks:=0
var failures:Array=[]
var facts:Dictionary={}
var started_ms:=0
func _initialize():call_deferred("_boot")
func _boot():
	var args:=OS.get_cmdline_user_args()
	role=args[0];receipt=args[1];port=int(args[2]);peer_count=int(args[3]);started_ms=Time.get_ticks_msec()
	sync=SyncR18.new();root.add_child(sync)
	game=load("res://scripts/main.gd").new();root.add_child(game);bridge=game.network_bridge;net=bridge.net
	await run()
func run():pass
func check(ok:bool,label:String)->bool:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",role," ",label)
	return ok
func wait_for(test:Callable,seconds:float=35.0)->bool:
	var limit:int=Time.get_ticks_msec()+int(seconds*1000)
	while Time.get_ticks_msec()<limit:
		await process_frame
		if test.call():return true
	return false
func frames(n:int):
	for i in n:await process_frame
func seconds(t:float):await create_timer(t).timeout
func ids()->Array:return net.players.keys()
## Every current roster member reports phase, then waits for all of them.
func barrier(phase:int,data:Dictionary={},timeout:float=60.0)->bool:
	sync.report(phase,data)
	return await wait_for(func():return sync.all_reached(ids(),phase),timeout)
func peer_named(label:String)->int:
	for id in net.players:
		if str(net.players[id].name)==label:return int(id)
	return 0
func map_id()->String:
	var m:Node=game.active_sandbox_map
	if not is_instance_valid(m):return ""
	if "map_id" in m:return str(m.map_id)
	if m.get_script().get_script_constant_map().has("MAP_ID"):return str(m.get_script().get_script_constant_map().MAP_ID)
	return "range"
func loaded_warmup()->bool:
	return net.stage=="lobby" and net.world_ready and not bridge._preparing and bridge._epoch==int(net.epoch) and net.players.size()==peer_count and net.players.get(net.me(),{}).get("loaded",false)
func loaded_match()->bool:
	return net.stage=="match" and not bridge._preparing and bridge._epoch==int(net.epoch) and net.players.get(net.me(),{}).get("loaded",false)
func host_or_join(label:String,secret:String)->void:
	if role=="host":check(net.host("R18 Host",port,secret)==OK,"listen host binds UDP "+str(port))
	else:check(net.join("127.0.0.1",role,port,secret)==OK,"client "+role+" joins")
func finish(tag:String,scope:String)->void:
	var result:={"role":role,"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"seconds":(Time.get_ticks_msec()-started_ms)*.001,"scope":scope}
	var f:=FileAccess.open(receipt,FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close()
	print(tag," ",JSON.stringify(result))
	if net.active:bridge.leave_session()
	game._quitting=true;game.queue_free();await process_frame;await process_frame
	quit(0 if failures.is_empty() else 1)
