extends SceneTree
const Session=preload("res://scripts/net_session1000.gd")
var failures:Array=[]
var cases:Array=[]
var actor:Node
var rack:Node
var bridge:Node
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node)->void:
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func path_for(cat:int)->Array:
	if cat==8:return ["cover_open","box_removed","holding_box","needs_feed","box_seated","needs_charge","ready"]
	if cat==1:return ["open_empty","holding_shells","open_loaded","ready"]
	if cat==7:return ["cylinder_open","cylinder_loaded","ready"]
	if cat==5:return ["needs_mag","held_mag","needs_bolt","ready"]
	return ["needs_mag","held_mag","needs_rack","ready"]
func run()->void:
	actor=preload("res://scripts/player.gd").new();root.add_child(actor);actor.setup(false);freeze(actor)
	rack=preload("res://scripts/weapon_rack.gd").new();root.add_child(rack);rack.setup(actor);freeze(rack)
	bridge=preload("res://scripts/net_bridge1000.gd").new();bridge.net=Session.new();root.add_child(bridge.net);root.add_child(bridge);bridge.set_process(false)
	for i in 2:await process_frame
	var authority=Session.new();root.add_child(authority);authority.set_process(false);authority.active=true;authority.server=true;authority.stage="match";authority.rules.infinite_ammo=false;authority.validator=func(_kind:String,_id:int,_data:Dictionary)->bool:return true
	for id in [1,2]:authority._add_player(id,"Fixture","");authority.players[id].loaded=true;authority.players[id].spawn=Vector3.ZERO
	for cat in [0,5,8,1,7]:
		var gun:Node=rack.weapons[cat];gun.reset_weapon();rack.Arsenal.apply(gun,cat);var pool=1 if cat==1 else (2 if cat==5 else 3);var family:String=rack.AMMO_FAMILIES[cat];authority.players[1].reserves[family]=pool;authority.players[2].reserves[family]=0
		authority.entities.clear();authority.players[1].inventory.clear();authority.players[2].inventory.clear();var eid="finite_"+str(cat);var e={"id":eid,"kind":"weapon","catalog":cat,"owner":1,"ammo":0,"capacity":gun.magazine_size,"seated":0,"reload":"ready","position":Vector3(0,.7,0),"basis":Basis.IDENTITY};authority.entities[eid]=e;authority.players[1].inventory=[eid]
		var stage_samples:Array=[];var steps=path_for(cat)
		for state in steps.slice(0,steps.size()-1):
			check(authority._reload(1,{"id":eid,"state":state}),str(cat)+" authoritative insertion "+state)
			bridge._sync_gun(eid,e,gun)
			if state in ["needs_rack","needs_bolt","needs_feed","open_loaded","cylinder_loaded"]:
				stage_samples.append({"state":state,"authoritative_seated":e.seated,"actual_gun_ammo":gun.ammo,"reserve":authority.players[1].reserves[family]})
				check(e.seated==pool,str(cat)+" staged entity count matches actual inserted finite rounds")
				if cat in [5,7]:check(gun.ammo==pool,str(cat)+" real gun bridge displays finite staged count")
				if cat==1:check(gun.action.loaded==pool,"DT11 actual breech action contains only inserted shell")
		check(authority.players[1].reserves[family]==0,str(cat)+" inserted ammunition is allocated once before ownership transfer")
		if cat==5:
			rack.reserves[family]=0;gun.eject_magazine();gun._finish_cycle()
			check(gun.reload_state=="needs_mag" and gun.ammo==0 and rack.reserves[family]==pool,"AWP real bolt removal refunds its inserted finite rounds")
			check(authority._reload(1,{"id":eid,"state":"needs_mag"}),"AWP authority receives actual removal stage")
			bridge._sync_gun(eid,e,gun);rack.reserves[family]=authority.players[1].reserves[family]
			check(gun.ammo==0 and rack.reserves[family]==pool,"AWP server sync preserves actual removal and one-time refund")
			gun.grab_magazine();check(gun.reload_state=="held_mag","AWP actual grab remains possible with only its refunded rounds")
			check(authority._reload(1,{"id":eid,"state":"held_mag"}),"AWP authority accepts actual grab stage")
			gun.insert_magazine();check(gun.reload_state=="needs_bolt" and gun.ammo==pool and rack.reserves[family]==0,"AWP real insertion consumes the refunded rounds once")
			check(authority._reload(1,{"id":eid,"state":"needs_bolt"}),"AWP authority accepts actual reinsertion stage")
			check(authority.players[1].reserves[family]==0 and e.seated==pool,"AWP remove/reinsert does not duplicate or discard allocated rounds")
		check(authority._drop(1,{"id":eid,"position":Vector3(0,.7,0)}) and authority._pickup(2,eid,{}),str(cat)+" staged gun transfers through real authoritative inventory methods")
		check(authority._reload(2,{"id":eid,"state":"ready"}),str(cat)+" transferred inserted rounds complete without new owner spares")
		bridge._sync_gun(eid,e,gun)
		check(e.ammo==pool and gun.ammo==pool and authority.players[2].reserves[family]==0,str(cat)+" completed bridge count and finite economy agree")
		if cat in [0,1,5,8]:
			rack.reserves[family]=0;gun.eject_magazine()
			if cat==5:gun._finish_cycle()
			var removal="box_removed" if cat==8 else ("open_empty" if cat==1 else "needs_mag")
			check(gun.reload_state==removal and gun.ammo==0 and rack.reserves[family]==pool,str(cat)+" actual tactical removal returns only its loaded rounds")
			check(authority._reload(2,{"id":eid,"state":removal}),str(cat)+" authoritative tactical removal")
			bridge._sync_gun(eid,e,gun);rack.reserves[family]=authority.players[2].reserves[family]
			check(gun.ammo==0 and rack.reserves[family]==pool,str(cat)+" tactical refund remains available after synchronization")
			check(not authority._reload(2,{"id":eid,"state":removal}) and authority.players[2].reserves[family]==pool,str(cat)+" duplicate removal cannot refund twice")
			gun.grab_magazine();check(gun.reload_state==("holding_box" if cat==8 else ("holding_shells" if cat==1 else "held_mag")),str(cat)+" real tactical reload can grab with only returned rounds")
			check(authority._reload(2,{"id":eid,"state":gun.reload_state}),str(cat)+" actual tactical grab accepted")
			gun.insert_magazine();check(gun.reload_state==("needs_feed" if cat==8 else ("needs_bolt" if cat==5 else ("open_loaded" if cat==1 else "needs_rack"))) and rack.reserves[family]==0,str(cat)+" actual tactical insert consumes returned finite rounds once")
			check(authority._reload(2,{"id":eid,"state":gun.reload_state}) and e.seated==pool and authority.players[2].reserves[family]==0,str(cat)+" tactical insertion conserves total ammunition")
		cases.append({"catalog":cat,"stages":stage_samples,"transfer_ready_ammo":pool,"ending_stage":e.reload,"ending_authority_ammo":e.ammo,"ending_authority_seated":e.seated,"ending_actual_gun_ammo":gun.ammo})
	authority.close(false);var row={"passed":failures.is_empty(),"failures":failures,"cases":cases,"scope":"actual AshPlayer, weapon rack, real gun objects and bridge synchronization with a controlled authoritative Session; partial finite inserts, staged cross-player transfer and final counts. Minimal rig without Main/map; not physical controller or multi-process proof."};var f=FileAccess.open(OS.get_cmdline_user_args()[0].path_join("finite_bridge1004.json"),FileAccess.WRITE);f.store_string(JSON.stringify(row,"\t"));f.close();print("FINITE_BRIDGE1004 "+JSON.stringify(row));quit(0 if failures.is_empty() else 1)
