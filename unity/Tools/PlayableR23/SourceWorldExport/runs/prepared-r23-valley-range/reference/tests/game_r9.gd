extends SceneTree
## Actual peers synchronize test stages through replicated map marks, not fixed sleeps.
var game:Node
var net:Node
var role:String
var output:String
var checks:=0
var failures:Array=[]
var steps:Dictionary={}
var initial_rack:=0
var previous_extra_r8:WeakRef
var match_epoch=0
var city_reference:WeakRef
var voice_clock=0.0
var debug_clock=0.0
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func barrier(color:int)->bool:
	if net.marks.size()!=2:return false
	for mark in net.marks.values():
		if int(mark.color)<color:return false
	return true
func rarity_glow_count(n:Node)->int:
	var count=0
	if n is GeometryInstance3D and n.material_overlay is ShaderMaterial and n.material_overlay.shader.resource_path.ends_with("rarity_r6/glow.gdshader"):count+=1
	for child in n.get_children():count+=rarity_glow_count(child)
	return count
func report(color:int):net.request("mark",{"point":game.player.global_position,"normal":Vector3.UP,"color":color})
func run():
	var args:=OS.get_cmdline_user_args();role=args[0];output=args[1];var port:=int(args[2]);var started:=Time.get_ticks_msec()*.001
	game=load("res://scripts/main.gd").new();root.add_child(game)
	for i in 4:await process_frame
	game.front_menu.gallery.style=6
	net=game.network_bridge.net
	if role=="host":
		check(net.host("Player One",port,"friends")==OK,"listen host starts")
		net.request("rules",{"map":"archive:atrocious-gods-trashpandakiller","mode":"respawn","protection":0.0,"time_limit":0,"score_limit":0,"respawn_delay":1.0,"catalog":true,"zone_enabled":true,"zone_wait":1.0,"zone_speed":20.0})
	else:
		await create_timer(3).timeout
		check(net.join("127.0.0.1","Player Two",port,"friends")==OK,"friend joins matching build")
	while Time.get_ticks_msec()*.001-started<115:
		await process_frame
		debug_clock+=.03
		if debug_clock>5:
			debug_clock=0;print("R4_STATE ",role," stage=",net.stage," epoch=",net.epoch," prep=",game.network_bridge._preparing," voice=",game.network_bridge.voice.sent_packets,"/",game.network_bridge.voice.received_packets," players=",net.players.size()," steps=",steps)
		if net.stage=="lobby" and not game.network_bridge._preparing and net.players.size()==2 and not steps.has("match"):
			if not steps.has("warmup"):
				steps.warmup=true;city_reference=weakref(game.active_sandbox_map)
				check(not game.front_end_active and net.zone.is_empty(),"Warmup is a real map without zone")
				check(game.player.global_position.distance_to(game.active_sandbox_map.warmup_spawn_r8)<10,"Warmup gathering spawn")
				check(not game.network_bridge.voice.enabled,"Microphone defaults off")
			voice_clock+=.03
			if voice_clock>.04:
				voice_clock=0
				var samples:=PackedVector2Array();samples.resize(960)
				for i in samples.size():samples[i]=Vector2.ONE*sin(float(i)*.08)*.2
				game.network_bridge.voice.transmit(load("res://scripts/voice_r4.gd").encode(samples))
			if game.network_bridge.voice.received_packets>2 and not steps.has("voice"):
				steps.voice=true;check(true,"Synthetic proximity voice crosses real ENet peers without mic");report(8)
			if net.is_host() and barrier(8) and net.can_start():net.request("start")
		if net.stage=="match" and not game.network_bridge._preparing:
			if not steps.has("match"):
				steps.match=true;match_epoch=int(net.epoch);initial_rack=game.weapon.weapons.size()
				if initial_rack>30:previous_extra_r8=weakref(game.weapon.weapons[initial_rack-1])
				check(game.active_sandbox_map.safe_spawn_r8(game.player.global_position),"actual hosted match starts on supported floor")
				if role=="host":
					for group in game.active_sandbox_map.gameplay_r8._panels_r8.values():
						if not group.is_empty():group[0].take_damage(1);break
				check(not game.front_end_active and game.active_sandbox_map.name=="LocalArchiveMap","START enters real city")
				check(game.weapon_inventory.owned.size()==3 and game.weapon_inventory.pickups.size()>=25,"starting ownership and shared grabbable drops")
				check(not net.zone.is_empty() and net.zone.next.size.x>0 and net.rules.zone_speed==20.0,"authoritative next square and speed reach game")
			if not steps.has("avatars") and game.network_bridge.avatars.size()==1 and net.poses.size()==2:
				steps.avatars=true
				var avatar:Node=game.network_bridge.avatars.values()[0]
				check(avatar._style==6 and avatar.suit.get_meta("character_style",-1)==6,"fourth Claude skin reaches actual remote body")
				check(avatar.suit.pieces.size()==13 and avatar.gloves.size()==2,"remote Argon body and skinned hands")
				game.network_bridge.tactical._desktop_hold_r5=true;game.network_bridge.tactical.consume_trigger(true,true);await process_frame
				check(game.network_bridge.tactical.opened and game.network_bridge.tactical.player_list_r5.ids.size()==2,"actual held tactical map and player roster")
				game.network_bridge.tactical._desktop_hold_r5=false;game.network_bridge.tactical.consume_trigger(false,true)
			if not steps.has("catalog_request"):
				steps.catalog_request=true;check(game.item_catalog_088.activate({"kind":"weapon","catalog":17,"rarity":5},false),"backpack grip uses authority")
			if not steps.has("catalog") and game.weapon.catalog_index(game.weapon.selected)==17 and not game.weapon.is_holstered and steps.has("avatars"):
				steps.catalog=true
				check(rarity_glow_count(game.weapon.weapons[game.weapon.selected])==0,"Local held five-star gun does not glow")
				check(game.weapon.weapons[game.weapon.selected].get_meta("rarity0991")==5 and game.weapon.weapons[game.weapon.selected].magazine_size==33,"Five-star MP5 rarity and capacity reach actual peer hand")
				var level_id:String=game.network_bridge.local_ids[game.weapon.selected]
				check(net.entities[level_id].rarity==5 and net.entities[level_id].capacity==33,"Authoritative replica stores identical rarity")
				check(game.weapon_inventory.owned.has(game.weapon.selected) and not game.weapon_inventory.pickups.has(game.weapon.selected),"same owned instance enters actual hand without duplicate")
				check(game.weapon_inventory.total_slots()<=game.weapon_inventory.capacity,"catalog respects capacity");report(0)
			if not steps.has("panel_state") and not net.archive_broken_r8.is_empty():
				steps.panel_state=true;check(true,"authoritative broken panel state crosses actual peers")
			if not steps.has("remote_normal") and steps.has("catalog"):
				for avatar in game.network_bridge.avatars.values():
					if is_instance_valid(avatar.gun) and avatar._catalog==17:
						check(rarity_glow_count(avatar.gun)==0,"Remote held five-star gun does not glow");steps.remote_normal=true
			if role=="host" and barrier(0) and not steps.has("target_sent"):
				steps.target_sent=true;game.practice_dummy.take_damage(25.0)
				var gun:Node=game.weapon.weapons[game.weapon.selected];gun.cooldown=0;game.weapon._draw_delay=0;gun._requires_trigger_release=false;game.weapon.fire()
			if game.practice_dummy.total_damage==25.0 and not steps.has("target"):
				steps.target=true;check(true,"shared practice damage arrives");report(1)
			if role=="host" and barrier(1) and not steps.has("grenade_sent"):
				steps.grenade_sent=true;net._event("projectile",{"id":9901,"player":1,"kind":"frag","origin":game.player.global_position+Vector3(0,2,3),"velocity":Vector3(0,2,1)})
			if game.network_bridge._projectiles.has(9901) and not steps.has("projectile"):
				steps.projectile=true;check(true,"actual authoritative grenade replica appears");report(2)
			if role=="host" and barrier(2) and not steps.has("death_sent"):
				steps.death_sent=true;net.hurt(int(net.players.keys()[1]),250,1,false,false,10)
			if not steps.has("feed") and not net.combat_feed.is_empty() and int(net.match_scores.get(0,0))==1:
				steps.feed=true;check(net.combat_feed[0].catalog==10 and net.combat_feed[0].attacker_name=="Player One" and net.combat_feed[0].victim_name=="Player Two","P90 knockdown names and team total replicate")
			if steps.has("feed") and not steps.has("respawn"):
				var alive:=true
				for p in net.players.values():alive=alive and p.life=="alive"
				if alive:steps.respawn=true;check(true,"actual death and autorespawn cycle");report(3)
			if role=="host" and barrier(3) and not steps.has("restart_sent"):steps.restart_sent=true;net.request("restart")
			if int(net.epoch)>match_epoch and net.stage=="match" and not game.network_bridge._preparing and not steps.has("rematch"):
				steps.rematch=true
				check(previous_extra_r8==null or previous_extra_r8.get_ref()==null,"rematch retires previous round weapon instances before activating fresh caches")
				check(not net.projectile_poses.has(9901) and net.combat_feed.is_empty() and net.match_scores.is_empty(),"rematch clears projectile feed and scores")
				check(not net.zone.is_empty() and int(net.zone.index)==0,"fresh rematch square");report(4)
			if role=="host" and int(net.epoch)>match_epoch and barrier(4) and not steps.has("home"):
				steps.home=true;net.rules.time_limit=1;net.match_started=net.now()-2
		if net.stage=="lobby" and steps.has("match"):
			if not steps.has("returned_warmup"):
				steps.returned_warmup=true
				check(not game.front_end_active and game.player.xr_origin!=null,"Round end returns to in-map warmup retaining calibrated origin")
				check(city_reference.get_ref()==game.active_sandbox_map,"Rounds reuse one city instance")
				check(net.zone.is_empty() and game.player.global_position.distance_to(game.active_sandbox_map.warmup_spawn_r8)<10,"Round end clears zone and gathers players");report(8)
			if barrier(8):steps.final_barrier=true;break
		await create_timer(.03).timeout
	for key in ["panel_state","remote_normal","warmup","voice","match","avatars","catalog","target","projectile","feed","respawn","rematch","returned_warmup","final_barrier"]:check(steps.has(key),"Completed stage "+key)
	check(net.stage=="lobby","sequence reaches lobby before watchdog")

	print("GAME_WIRE1000 role=",role," checks=",checks," failures=",failures.size())
	# Let reliable final snapshots drain through the impairment relay before closing.
	await create_timer(2).timeout
	game.network_bridge.leave_session();await process_frame;await process_frame
	check(game.front_end_active and not is_instance_valid(game.active_sandbox_map) and city_reference.get_ref()==null,"Leaving session unloads actual map")
	var f:=FileAccess.open(output,FileAccess.WRITE);f.store_string(JSON.stringify({"role":role,"checks":checks,"passed":failures.is_empty(),"failures":failures,"stages":steps,"synthetic_voice":{"sent":game.network_bridge.voice.sent_packets,"received":game.network_bridge.voice.received_packets,"microphone_opened":false}},"\t"));f.close()
	game._quitting=true;game.queue_free();await process_frame;await process_frame;quit(0 if failures.is_empty() else 1)
