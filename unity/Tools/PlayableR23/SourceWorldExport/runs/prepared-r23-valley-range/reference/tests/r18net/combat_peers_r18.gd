extends "res://tests/r18net/peer_base_r18.gd"
## R18 host + two real client Mains (roles host,p1,p2) over loopback ENet, in a real match.
## Weapon pickup race/ownership/rarity/ammo, drop/repickup without refill, mounted gun pose
## replication, paired-Uzi shot acceptance under delivery jitter (finite ammunition),
## moving-shooter shot origin/direction and shared remote tracer, voice protocol
## (route/mute/listen/range/silence/rate), and non-admin live-rule rejection.
## Shots and voice frames are synthetic but travel through the real bridge/session/voice code.
const RarityR6=preload("res://scripts/weapon_rarity_r6.gd")
const GUN:="r18_cx4"
var shots_by:Dictionary={}     # shooter -> Array of {origin,direction,draw_start,local_emitter}
var picked:Dictionary={}
func run():
	net.event_received.connect(_probe)
	host_or_join(role,"r18combat")
	if role=="host":net.request("rules",{"map":"range","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":0,"score_limit":0,"protection":0.0,"respawn_delay":3.0})
	check(await wait_for(loaded_warmup,60),"three peers in warmup")
	check(await barrier(1),"barrier 1")
	if role=="host":net.request("start")
	check(await wait_for(loaded_match,60),"match running on every peer")
	var revision:int=int(net.players[net.me()].spawn_revision)
	check(await barrier(2),"barrier 2")
	var p1:int=peer_named("p1");var p2:int=peer_named("p2")
	# ---------- staging: three bodies in a row, 2 m apart ----------
	if role=="host":
		var base:Vector3=game.active_sandbox_map.spawn
		var row:Array=[1,p1,p2]
		for i in 3:net._relocate_r12(row[i],base+Vector3(i*2.0,0,0))
		net._publish()
	check(await wait_for(func():return int(net.players[net.me()].spawn_revision)>revision,8),"authoritative staging arrives")
	await seconds(1.2)
	check(await barrier(3),"barrier 3")
	# ---------- pickup race, ownership, rarity, ammunition ----------
	if role=="host":
		var at:Vector3=(net._position(p1)+net._position(p2))*.5+Vector3.UP*1.1
		var capacity:int=RarityR6.capacity(0,4)
		net.register_world([{"id":GUN,"kind":"weapon","catalog":0,"rarity":4,"owner":0,"ammo":7,"capacity":capacity,"reload":"ready","seated":7,"position":at,"basis":Basis.IDENTITY,"rocket_reserve":0}])
		facts.cx4_capacity_r4=capacity
	check(await wait_for(func():return net.entities.has(GUN) and bridge.world_props.has(GUN)),"rarity-4 CX4 replicated as ground loot")
	await frames(3)
	check(bridge.world_props.has(GUN) and bridge.world_props[GUN].is_visible_in_tree(),"ground prop visible before pickup")
	check(await barrier(4),"barrier 4")
	if role in ["p1","p2"]:net.request("pickup",{"id":GUN})
	check(await wait_for(func():return int(net.entities.get(GUN,{}).get("owner",0))>0,6),"contested pickup resolves")
	await seconds(.5)
	var owner:int=int(net.entities[GUN].owner);var other:int=p2 if owner==p1 else p1
	var e:Dictionary=net.entities[GUN]
	check(owner in [p1,p2] and int(e.rarity)==4 and int(e.ammo)==7 and int(e.capacity)==RarityR6.capacity(0,4),"one owner, rarity 4, ammo 7 preserved "+str([owner,e.rarity,e.ammo,e.capacity]))
	var claims:=0
	for id in ids():
		if net.players[id].inventory.has(GUN):claims+=1
	check(claims==1,"exactly one inventory holds the contested gun")
	if net.me()==owner:
		var idx:int=int(bridge.indices[GUN]);var gun:Node=game.weapon.weapons[idx]
		check(game.weapon_inventory.owned.has(idx) and int(gun.ammo)==7 and RarityR6.level(gun)==4,"owner's local gun has authority ammo/rarity")
		game.weapon.selected=idx;game.weapon.is_holstered=false;game.weapon._apply_selection()
	else:
		check(not bridge.world_props[GUN].is_visible_in_tree(),"picked gun no longer shown on the ground")
	check(await barrier(5,{"owner":owner}),"barrier 5")
	var agreed:=true
	for id in ids():agreed=agreed and int(sync.fact(id,5).get("owner",-1))==owner
	check(agreed,"all peers agree on the owner")
	# ---------- mounted pose replication ----------
	await seconds(1.0)
	if net.me()!=owner:
		var avatar:Node=bridge.avatars[owner]
		check(int(avatar._catalog)==0 and is_instance_valid(avatar.gun) and avatar.gun.is_visible_in_tree() and int(avatar._rarity_r6)==4,"observer sees the owner's CX4 with rarity 4 mounted")
		if net.poses[owner].has("gun_pose") and is_instance_valid(avatar.gun):
			var pose:Transform3D=net.poses[owner].gun_pose
			var angle:float=rad_to_deg(Quaternion(avatar.gun.global_basis.orthonormalized()).angle_to(Quaternion(pose.basis.orthonormalized())))
			facts.mounted_angle_deg=angle;facts.mounted_offset_m=avatar.gun.global_position.distance_to(pose.origin)
			check(angle<3.0 and avatar.gun.global_position.distance_to(pose.origin)<.05,"remote gun basis/origin follows owner's replicated gun pose")
		else:check(false,"owner pose carries gun_pose")
	check(await barrier(6),"barrier 6")
	# ---------- drop and re-pickup: no refill, rarity kept ----------
	var drop_at:Vector3=(net._position(owner)+net._position(other))*.5+Vector3.UP*.8
	if net.me()==owner:net.request("drop",{"id":GUN,"position":drop_at})
	check(await wait_for(func():return int(net.entities[GUN].owner)==0,6),"drop returns gun to the world")
	await frames(4)
	check(int(net.entities[GUN].ammo)==7 and int(net.entities[GUN].rarity)==4,"dropped gun keeps ammo and rarity")
	check(bridge.world_props[GUN].is_visible_in_tree() and bridge.world_props[GUN].global_position.distance_to(net.entities[GUN].position)<.1,"dropped prop visible at authority position on this peer")
	check(await barrier(7),"barrier 7")
	if net.me()==other:net.request("pickup",{"id":GUN})
	check(await wait_for(func():return int(net.entities[GUN].owner)==other,6),"other player picks the dropped gun up")
	check(int(net.entities[GUN].ammo)==7,"re-pickup does not refill")
	check(await barrier(8),"barrier 8")
	# ---------- paired Uzi: legit cadence delivered with jitter must stay finite and exact ----------
	var uzi:String="r18_uzi_"+str(p1)
	if role=="host":
		var cap:int=RarityR6.capacity(11,1)
		net.entities[uzi]={"id":uzi,"kind":"weapon","catalog":11,"owner":p1,"rarity":1,"ammo":cap,"capacity":cap,"reload":"ready","position":Vector3.ZERO,"basis":Basis.IDENTITY};net._normalize_weapon(net.entities[uzi]);net.players[p1].inventory.append(uzi);net._publish()
		facts.uzi_hands_start=net.entities[uzi].hands.duplicate()
	check(await wait_for(func():return net.entities.has(uzi) and bridge.indices.has(uzi),6),"paired Uzi granted")
	check(await barrier(9),"barrier 9")
	if role=="p1":
		var origin:Vector3=game.player.global_position+Vector3.UP*1.3
		var t:float=net.now()
		# Two right-hand rounds fired 80 ms apart (13 rps = 77 ms), arriving in one packet burst.
		_shoot(uzi,origin,t-.16,1);_shoot(uzi,origin,t-.08,1)
		await seconds(.4)
		# Fabricated burst: ten left-hand rounds stamped with the same instant.
		t=net.now()
		for i in 10:_shoot(uzi,origin,t,0)
		await seconds(.4)
		# Remaining right-hand rounds at true 80 ms cadence, delivered two at a time.
		var sent:=2
		while sent<22:
			t=net.now();_shoot(uzi,origin,t-.08,1);_shoot(uzi,origin,t,1);sent+=2
			await seconds(.16)
		await seconds(.5)
	check(await barrier(10),"barrier 10")
	if role=="host":
		var hands:Array=net.entities[uzi].hands
		facts.uzi_hands_end=hands
		check(int(hands[0])==18,"same-instant fabricated burst of ten accepts only one round plus one catch-up round ("+str(hands[0])+" left)")
		check(int(hands[1])==0,"20 legit right rounds delivered in jittered pairs all accepted and the 21st/22nd refused ("+str(hands[1])+" left)")
	if role=="p1":
		await frames(4)
		var pair:Node=game.weapon.weapons[int(bridge.indices[uzi])]
		var authority:Array=net.entities[uzi].hands
		check(int(pair.guns[0].ammo)==int(authority[0]) and int(pair.guns[1].ammo)==int(authority[1]),"client pair mirrors authority "+str([pair.guns[0].ammo,pair.guns[1].ammo])+" vs "+str(authority))
	check(await barrier(11),"barrier 11")
	# ---------- shot timestamp policy over real ENet (separate WHITE Uzis per case) ----------
	var cases:={"forged":"r18_uzi_f_"+str(p1),"backlog":"r18_uzi_k_"+str(p1),"both":"r18_uzi_b_"+str(p1),"fast":"r18_uzi_c_"+str(p1)}
	if role=="host":
		var cap:int=RarityR6.capacity(11,1)
		for eid in cases.values():
			net.entities[eid]={"id":eid,"kind":"weapon","catalog":11,"owner":p1,"rarity":1,"ammo":cap,"capacity":cap,"reload":"ready","position":Vector3.ZERO,"basis":Basis.IDENTITY};net._normalize_weapon(net.entities[eid]);net.players[p1].inventory.append(eid)
		net._publish()
	check(await wait_for(func():return net.entities.has(cases.fast),6),"policy Uzis granted")
	check(await barrier(30),"barrier 30")
	if role=="p1":
		var origin:Vector3=game.player.global_position+Vector3.UP*1.3
		# forged: non-finite, far future, stale; then one valid round and an out-of-order one.
		var t:float=net.now()
		for bad in [NAN,INF,-INF,t+1.0,t-2.0]:_shoot(cases.forged,origin,bad,0)
		await seconds(.3)
		t=net.now()-.06;_shoot(cases.forged,origin,t,0);_shoot(cases.forged,origin,t-.1,0)
		await seconds(.3)
		# backlog: twelve correctly spaced (77 ms) rounds from the last 0.95 s delivered at once.
		t=net.now()
		for k in 12:_shoot(cases.backlog,origin,t-.95+k*.077,0)
		await seconds(.4)
		# both hands at 13 rps, stamps 80 ms apart, delivered in delayed batches of three steps.
		var step:=0
		while step<20:
			t=net.now()
			for j in mini(3,20-step):
				var at:float=t-.06-(2-j)*.08
				_shoot(cases.both,origin,at,0);_shoot(cases.both,origin,at,1)
			step+=3
			await seconds(.24)
		await seconds(.3)
		# faster clock: 25 rounds sent every 40 ms but stamped 80 ms apart (client clock runs 2x).
		var start:float=net.now()-.06
		for k in 25:
			_shoot(cases.fast,origin,start+k*.08,1)
			await seconds(.04)
		await seconds(.5)
	check(await barrier(31),"barrier 31")
	if role=="host":
		var f:Array=net.entities[cases.forged].hands;var k:Array=net.entities[cases.backlog].hands;var b:Array=net.entities[cases.both].hands;var c:Array=net.entities[cases.fast].hands
		facts.policy_hands={"forged":f,"backlog":k,"both":b,"fast":c}
		check(int(f[0])==19 and int(f[1])==20,"NaN/INF/future/stale/out-of-order stamps rejected; one valid round; other hand untouched "+str(f))
		check(int(k[0])==14 and int(k[1])==20,"delayed backlog: stale rounds (>0.75 s) rejected, burst capped at 6 per 0.25 s "+str(k))
		check(int(b[0])==0 and int(b[1])==0,"both hands independently consume exactly 20 each under delayed batches "+str(b))
		check(20-int(c[1])<=15 and int(c[0])==20,"faster client clock cannot exceed the 15/s wall-clock budget ("+str(20-int(c[1]))+" accepted)")
	check(await barrier(32),"barrier 32")
	# ---------- moving shooter: origin/direction/tracer on observers ----------
	var shooter:int=p2
	if net.me()==shooter:
		var starter:String=""
		for eid in net.players[shooter].inventory:
			if int(net.entities.get(eid,{}).get("catalog",-1))==0 and eid!=GUN:starter=eid
		if bridge.indices.has(starter):
			game.weapon.selected=int(bridge.indices[starter]);game.weapon.is_holstered=false;game.weapon._apply_selection()
		game.player.set_physics_process(false)
		var sent:Array=[];var begin:int=Time.get_ticks_msec();var last:int=begin;var next_shot:=0.3
		while Time.get_ticks_msec()-begin<2400:
			await process_frame
			var now:int=Time.get_ticks_msec();var dt:float=(now-last)*.001;last=now
			game.player.global_position+=Vector3(0,0,9.0)*dt;game.player.velocity=Vector3(0,0,9)
			if (now-begin)*.001>=next_shot and sent.size()<6:
				next_shot+=.3
				var muzzle:Transform3D=game.weapon.weapons[game.weapon.selected].get_shot_transform()
				var dir:Vector3=(-muzzle.basis.z).normalized()
				bridge.shot(muzzle.origin,[dir]);sent.append({"origin":muzzle.origin,"direction":dir})
		game.player.set_physics_process(true)
		facts.moving_shots_sent=sent.size()
		check(sent.size()==6,"six shots fired while moving 9 m/s")
		await seconds(.6)
		sync.report(42,{"sent":sent})
	check(await wait_for(func():return sync.all_reached([shooter],42),15),"shooter finished")
	check(await barrier(43),"barrier 43")
	if net.me()!=shooter:
		var sent:Array=sync.fact(shooter,42).get("sent",[]);var got:Array=shots_by.get(shooter,[])
		check(got.size()==sent.size() and sent.size()==6,"every moving shot accepted and broadcast ("+str(got.size())+"/"+str(sent.size())+")")
		var worst_origin:=0.0;var worst_dir:=0.0;var worst_draw:=0.0;var emitters:=0
		for i in mini(got.size(),sent.size()):
			worst_origin=maxf(worst_origin,(got[i].origin as Vector3).distance_to(sent[i].origin))
			worst_dir=maxf(worst_dir,rad_to_deg((got[i].direction as Vector3).angle_to(sent[i].direction)))
			worst_draw=maxf(worst_draw,(got[i].draw_start as Vector3).distance_to(got[i].origin))
			if got[i].local_emitter:emitters+=1
		facts.moving_origin_error=worst_origin;facts.moving_direction_error_deg=worst_dir;facts.tracer_start_error=worst_draw
		check(worst_origin<.001 and worst_dir<.01,"observers receive the shooter's exact muzzle origin and direction")
		check(worst_draw<.001 and emitters==0,"remote tracer starts at shooter muzzle, not attached to observer's gun")
		var lag:float=float(got.back().get("avatar_gap",-1.0)) if not got.is_empty() else -1.0
		facts.avatar_to_muzzle_gap_m=lag
	check(await barrier(44),"barrier 44")
	# ---------- voice protocol ----------
	bridge.voice.set_enabled(true)
	await frames(3)
	var talker:int=p1;var voice:Node=bridge.voice
	var base_rx:int=voice.received_packets
	if net.me()==talker:await _talk(15,.35)
	await seconds(.5)
	if net.me()!=talker:
		check(voice.received_packets-base_rx>=10 and voice.speakers.has(talker),"in-range peers receive and play talker voice ("+str(voice.received_packets-base_rx)+")")
		check(await wait_for(func():return voice.is_speaking(talker),1) or voice.speaking_until.has(talker),"speaking indicator set for talker")
	check(await barrier(45),"barrier 45")
	if role=="p2":voice.toggle_mute(talker)
	if role=="host":voice.listening=false
	await seconds(.3)
	check(await barrier(46),"barrier 46")
	base_rx=voice.received_packets
	if net.me()==talker:await _talk(15,.35)
	await seconds(.5)
	if role=="p2":
		check(voice.received_packets==base_rx and not voice.speakers.has(talker),"muted talker is not played")
		facts.indicator_while_muted=voice.speaking_until.has(talker) and int(voice.speaking_until[talker])>Time.get_ticks_msec()-900
	if role=="host":check(voice.received_packets==base_rx and voice.speakers.is_empty(),"listening off plays nobody")
	check(await barrier(47),"barrier 47")
	if role=="p2":voice.toggle_mute(talker)
	if role=="host":voice.listening=true
	await seconds(.3)
	check(await barrier(48),"barrier 48")
	if net.me()==talker:await _talk(15,0.0)
	await seconds(.9)
	if net.me()!=talker:check(not voice.is_speaking(talker),"silent frames do not light the speaking indicator")
	# Distance gate: move p2 60 m away, then talk again.
	revision=int(net.players[net.me()].spawn_revision)
	check(await barrier(49),"barrier 49")
	if role=="host":net._relocate_r12(p2,net._position(p2)+Vector3(0,0,60));net._publish()
	if role=="p2":await wait_for(func():return int(net.players[net.me()].spawn_revision)>revision,6)
	await seconds(1.0)
	if role=="host":facts.voice_gap_m=net.poses[p1].head.origin.distance_to(net.poses[p2].head.origin)
	check(await barrier(50),"barrier 50")
	base_rx=voice.received_packets
	if net.me()==talker:await _talk(15,.35)
	await seconds(.5)
	if role=="p2":
		check(voice.received_packets==base_rx,"server does not route voice beyond 42 m")
		facts.indicator_beyond_range=voice.speaking_until.has(talker) and int(voice.speaking_until[talker])>Time.get_ticks_msec()-900
	if role=="host":check(voice.received_packets>base_rx,"host still hears in-range talker")
	check(await barrier(51),"barrier 51")
	# Flood: 120 frames in one tick; server accepts at most 60 per second.
	if role=="host":facts.voice_rejected_before=voice.rejected_packets
	if net.me()==talker:
		for i in 120:voice.transmit(_tone(.35,i))
	await seconds(.6)
	if role=="host":
		facts.voice_flood_rejected=voice.rejected_packets-int(facts.voice_rejected_before)
		check(int(facts.voice_flood_rejected)>=40,"server drops most of a 120-frame microphone flood (60/s cap; window may roll over) ("+str(facts.voice_flood_rejected)+")")
	check(await barrier(52),"barrier 52")
	# ---------- live rules: only the admin ----------
	var dmg:float=float(net.rules.damage)
	if role=="p1":net.request("rules",{"damage":0.0,"walk":5.0})
	await seconds(.6)
	check(is_equal_approx(float(net.rules.damage),dmg),"non-admin cannot change live match rules")
	check(await barrier(53),"barrier 53")
	if role=="host":await seconds(1.5)
	await finish("COMBAT_PEERS_R18","Three actual Main processes over loopback ENet in a real match; synthetic shot/voice payloads through the real bridge/session/voice code; XR off, Dummy audio (no real capture/playback quality, no Internet/NAT/headset).")
func _shoot(eid:String,origin:Vector3,time:float,side:int)->void:
	net.request("shot",{"id":eid,"origin":origin,"directions":[Vector3.FORWARD],"time":time,"side":side})
func _tone(amplitude:float,phase:int)->PackedByteArray:
	var p:=PackedByteArray();p.resize(bridge.voice.BYTES)
	for i in bridge.voice.FRAMES:p.encode_s16(i*2,roundi(sin(float(i+phase*320)*.2)*amplitude*32767))
	return p
func _talk(count:int,amplitude:float)->void:
	for i in count:
		bridge.voice.transmit(_tone(amplitude,i))
		await create_timer(.02).timeout
func _probe(kind:String,data:Dictionary)->void:
	if kind!="shot" or int(data.player)==net.me():return
	var index:int=posmod(game._tracer_cursor-1,game.EFFECT_POOL_SIZE);var trace:Node3D=game._tracers[index]
	var row:={"origin":data.origin,"direction":(data.directions[0] as Vector3),"draw_start":trace.global_position-trace.global_basis.y*.5,"local_emitter":game._tracer_emitters[index].get_ref()!=null}
	if bridge.avatars.has(int(data.player)) and is_instance_valid(bridge.avatars[int(data.player)].gun):row.avatar_gap=bridge.avatars[int(data.player)].gun.global_position.distance_to(data.origin)
	if not shots_by.has(int(data.player)):shots_by[int(data.player)]=[]
	shots_by[int(data.player)].append(row)
