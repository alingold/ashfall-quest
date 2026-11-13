extends "res://tests/r18net/peer_base_r18.gd"
## Solo listen host (role host only): START is permitted with nobody else, warmup -> match ->
## rematch -> warmup, each with a fresh epoch/spawn revision and no previous-round entities,
## plus admin countdown cancel.
## Run: run-peers.ps1 -Roles host -Script tests/r18net/solo_start_r18.gd
func run():
	check(net.host("Solo Host",port,"solo")==OK,"solo listen host")
	net.request("rules",{"map":"range","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0})
	check(await wait_for(func():return net.stage=="lobby" and net.world_ready and not bridge._preparing and net.players[1].loaded,60),"solo shared warmup loads")
	check(net.can_start(),"solo host is allowed to START")
	var e0:int=net.epoch;var r0:int=int(net.players[1].spawn_revision)
	net.request("start")
	check(net.stage=="countdown","solo START enters countdown")
	check(await wait_for(func():return int(net.epoch)>e0 and loaded_match(),30),"solo match starts")
	check(int(net.players[1].spawn_revision)>r0 and game.player.input_enabled,"solo match spawn applied with controls")
	var first_ids:Array=net.entities.keys();var e1:int=net.epoch
	net.request("restart")
	check(await wait_for(func():return int(net.epoch)>e1 and loaded_match(),30),"solo rematch")
	var leaked:=0
	for eid in first_ids:
		if net.entities.has(eid):leaked+=1
	check(leaked==0,"rematch retires previous-round entities ("+str(leaked)+")")
	var e2:int=net.epoch
	net.request("home")
	check(await wait_for(func():return int(net.epoch)>e2 and net.stage=="lobby" and net.world_ready and not bridge._preparing,30),"solo return to shared warmup")
	check(not game.front_end_active and map_id()=="range","warmup stays in-map")
	# Countdown cancel is the admin's: START then CANCEL returns to warmup without a reload.
	var e3:int=net.epoch
	net.request("start");net.request("cancel")
	await seconds(3.6)
	check(net.stage=="lobby" and int(net.epoch)==e3,"cancelled countdown never loads a match")
	await finish("SOLO_START_R18","One real Main as solo listen host on loopback; XR off.")
