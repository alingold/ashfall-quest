extends SceneTree
class Probe:
	extends "res://scripts/net_session1000.gd"
	var override_id:=""
	func content_id()->String:return super.content_id() if override_id.is_empty() else override_id
var net:Node
var role:String
var output:String
var port:int
var checks:=0
var failures:Array=[]
var reasons:Array=[]
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func wait_for(condition:Callable,seconds:=6.0)->bool:
	var end:float=Time.get_ticks_msec()*.001+seconds
	while Time.get_ticks_msec()*.001<end:
		if condition.call():return true
		await create_timer(.025).timeout
	return false
func run()->void:
	var args:=OS.get_cmdline_user_args();role=args[0];output=args[1];port=int(args[2]);net=Probe.new();root.add_child(net)
	net.disconnected.connect(func(reason:String):reasons.append(reason))
	if role=="server":
		check(net.host("Auth fixture",port,"private",true)==OK,"authentication host starts")
		check(await wait_for(func():return net.players.size()==1),"only compatible client admitted")
		if net.players.size()==1:check(net.players.values()[0].name=="Compatible","rejected identities never enter roster")
		await create_timer(.4).timeout
	else:
		net.join("127.0.0.1","Wrong password",port,"bad")
		check(await wait_for(func():return reasons.size()==1),"wrong password rejected")
		check(not net.active and net.players.is_empty() and str(reasons[0]).contains("password"),"wrong-password cleanup and reason")
		net.override_id=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/network_content1000.json")).content_id
		net.join("127.0.0.1","Public unpatched client",port,"private")
		check(await wait_for(func():return reasons.size()==2),"released public fingerprint rejected")
		check(not net.active and net.players.is_empty() and str(reasons[1]).contains("Different ASHFALL build"),"build rejection cleanup and reason")
		net.override_id="";net.join("127.0.0.1","Compatible",port,"private")
		check(await wait_for(func():return net.active and net.players.has(net.me())),"fixed candidate connects successfully")
	var report:Dictionary={"role":role,"checks":checks,"failures":failures,"passed":failures.is_empty(),"reasons":reasons,"scope":"real local ENet handshake; public fingerprint supplied by test override, not a launched public binary"}
	var file:=FileAccess.open(output,FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close();print("HANDSHAKE1001 ",JSON.stringify(report));net.close(false);net.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
