extends SceneTree
const Session=preload("res://scripts/net_session1000.gd")
const Rack=preload("res://scripts/weapon_rack.gd")
const Data=preload("res://scripts/arsenal_data.gd")
var net:Node
var failures:Array=[]
var checks=0
var weapons:Array=[]
var grip=0
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func path_for(cat:int)->Array:
	if cat==8:return ["cover_open","box_removed","holding_box","needs_feed","box_seated","needs_charge","ready"]
	if cat==1:return ["open_empty","holding_shells","open_loaded","ready"]
	if cat==7:return ["cylinder_open","cylinder_loaded","ready"]
	if cat==5:return ["needs_mag","held_mag","needs_bolt","ready"]
	if cat in [11,12]:return ["needs_mag","ready"]
	return ["needs_mag","held_mag","needs_rack","ready"]
func gun(id:String,cat:int,owner:int,ammo:int=0)->Dictionary:
	var profile=Data.profile(cat);var cap=int(profile.magazine_per_hand)*(2 if cat in [11,12] else 1)
	return {"id":id,"kind":"weapon","catalog":cat,"owner":owner,"ammo":ammo,"capacity":cap,"reload":"ready","seated":ammo,"position":Vector3(0,.7,0),"basis":Basis.IDENTITY,"rocket_reserve":2 if cat==27 else 0}
func run()->void:
	net=Session.new();root.add_child(net);net.active=true;net.server=true;net.stage="match";net.validator=func(_kind:String,_id:int,_data:Dictionary)->bool:return true;net.rules.infinite_ammo=false;net.rules.infinite_builds=false;net.rules.protection=0;net.rules.score_limit=0;net.rules.teams=2
	for id in [1,2]:net._add_player(id,"Fixture","");net.players[id].loaded=true;net.players[id].spawn=Vector3.ZERO
	for cat in Rack.ACTIVE_WEAPON_INDICES:
		if cat in [14,15,16,26,27,28,29]:continue
		net.entities.clear();net.players[1].inventory.clear();net.players[2].inventory.clear();var eid="gun_"+str(cat);var e=gun(eid,cat,1);net.entities[eid]=e;net.players[1].inventory=[eid];net._normalize_weapon(e)
		var family:String=Rack.AMMO_FAMILIES[cat];net.players[1].reserves[family]=3;var before=checks;var reused_rejected=0
		for side in (2 if cat in [11,12] else 1):
			for state in path_for(cat):
				var stroke=(cat==8 and state in ["needs_feed","box_seated","needs_charge","ready"]) or (cat not in [1,7,8,11,12] and state in ["needs_rack","needs_bolt","ready"])
				if stroke:grip+=1;net._strokes[1]=grip
				var request={"id":eid,"state":state,"side":side if cat in [11,12] else -1,"xr":true,"grip_serial":grip}
				check(net._reload(1,request),str(cat)+" valid reload "+state)
				if stroke and state in ["needs_feed","box_seated","needs_charge","needs_rack","needs_bolt"]:
					var next="ready" if state in ["needs_charge","needs_rack","needs_bolt"] else ("box_seated" if state=="needs_feed" else "needs_charge")
					var held=request.duplicate();held.state=next
					var accepted=net._reload(1,held);check(not accepted,str(cat)+" held grip cannot skip release before "+next)
					if not accepted:reused_rejected+=1
		check(e.ammo==mini(3,int(e.capacity)) and int(net.players[1].reserves[family])==3-int(e.ammo),str(cat)+" finite reload conserves three available rounds within capacity")
		check(not net._reload(2,{"id":eid,"state":"needs_mag"}),str(cat)+" other player cannot reload")
		check(net._shoot(1,{"id":eid,"origin":Vector3(0,1.3,0),"directions":[Vector3.FORWARD],"side":0}),str(cat)+" loaded gun fires")
		var after_shot=int(e.ammo);check(not net._shoot(1,{"id":eid,"origin":Vector3(0,1.3,0),"directions":[Vector3.FORWARD],"side":0}),str(cat)+" same-tick cadence rejects second shot");check(e.ammo==after_shot,str(cat)+" rejected shot consumes nothing")
		check(net._drop(1,{"id":eid,"position":Vector3(0,.7,0)}) and net._pickup(2,eid,{}),str(cat)+" cross-player transfer")
		check(e.ammo==after_shot and int(e.owner)==2,str(cat)+" transfer preserves remaining ammo")
		weapons.append({"catalog":cat,"id":Data.profile(cat).id,"checks":checks-before,"fresh_grip_rejections":reused_rejected})
	# Empty staged weapons remain transferable; inventory saturation cannot block supplies.
	net.entities.clear();net.players[1].inventory.clear();net.players[2].inventory.clear()
	for i in 6:
		var e=gun("slot"+str(i),8 if i==0 else 0,1);e.reload="needs_charge" if i==0 else "ready";net.entities[e.id]=e;net.players[1].inventory.append(e.id)
	var extra=gun("extra",0,0);net.entities.extra=extra;check(not net._pickup(1,"extra",{}),"seventh gun requires explicit replacement")
	check(net._pickup(1,"extra",{"replace":"slot0","drop":Vector3(0,.7,0)}),"six-slot replacement accepts an unloaded staged M60")
	check(net.entities.slot0.ammo==0 and net.entities.slot0.reload=="needs_charge","dropping unloaded M60 preserves stage")
	check(net._pickup(2,"slot0",{}) and net.entities.slot0.ammo==0,"repicking empty M60 does not refill")
	for family in ["pistol","smg","rifle","shells","sniper","builds"]:
		var amount=int(preload("res://scripts/supply_catalog_088.gd").AMOUNTS[family]);var eid="supply_"+family
		if family=="builds":net.players[1].builds=1000000
		else:net.players[1].reserves[family]=1000000
		net.entities[eid]={"id":eid,"kind":"builds" if family=="builds" else "ammo","family":family,"amount":amount,"owner":0,"position":Vector3(0,.7,0),"basis":Basis.IDENTITY}
		check(net._pickup(1,eid,{}),family+" supply pickup with full gun inventory");check(not net._pickup(2,eid,{}),family+" cannot award twice")
		check((net.players[1].builds if family=="builds" else net.players[1].reserves[family])==1000000+amount,family+" uncapped exact quantity")
	net.players[1].builds=6;var build={"entries":[{"kind":"wall","key":"one","at":Transform3D(Basis.IDENTITY,Vector3(0,1,2)),"support":true},{"kind":"floor","key":"two","at":Transform3D(Basis.IDENTITY,Vector3(0,0,3)),"support":true}]}
	check(net._build(1,build) and net.players[1].builds==4 and net.builds.size()==2,"finite two-piece build spends two charges")
	for bid in net.builds.keys():net.hurt_build(bid,1000)
	check(net.builds.is_empty(),"authoritative build damage removes pieces")
	net.players[2].team=1;net.players[1].team=0;net.players[2].health=100;net.players[2].shield=100;net.players[2].protection_until=0
	net.hurt(2,25,1);check(net.players[2].shield==75 and net.players[2].health==100,"damage drains shield before health")
	net.hurt(2,100,1);check(net.players[2].shield==0 and net.players[2].health==75,"damage overflow reaches health")
	var health=net.players[2].health;net.hurt(2,NAN,1);net.hurt(2,-10,1);check(net.players[2].health==health,"nonfinite and negative damage rejected")
	net.close(false);var result={"passed":failures.is_empty(),"checks":checks,"failures":failures,"weapons":weapons,"scope":"actual authoritative session reload/fire/inventory/build/damage methods; 3-round finite pools, controlled grip serials and permissive geometry validator. Actual game gun art/physical gestures and terrain/climbing are separate coverage."};var f=FileAccess.open(OS.get_cmdline_user_args()[0],FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("MECHANICS1004 "+JSON.stringify(result));quit(0 if failures.is_empty() else 1)
