extends RefCounted
## All match-affecting settings live here. Personal VR preferences never enter this schema.
const DEFAULTS := {"map":"range","mode":"revive","max_players":8,"teams":2,"friendly_fire":false,"damage":1.0,"npc_damage":1.0,"build_damage":1.0,"headshots":1.0,"walk":2.5,"run":1.0,"flight":1.0,"fling":3.0,"stamina":1.0,"starting_health":100,"starting_shield":100,"starting_builds":999,"builds":true,"infinite_builds":false,"infinite_ammo":false,"loot":true,"catalog":false,"respawn_delay":5.0,"protection":3.0,"retain_inventory":true,"downed_seconds":60.0,"time_limit":600,"score_limit":25,"rounds":1,"late_join":true,"spectators":true,"locked":false,"loadout":"standard","zone_enabled":true,"zone_damage":5.0,"zone_speed":2.0,"zone_wait":30.0}
const LIMITS := {"max_players":[2,8],"teams":[0,8],"damage":[0.0,3.0],"npc_damage":[0.0,3.0],"build_damage":[0.0,3.0],"headshots":[0.0,3.0],"walk":[.25,5.0],"run":[.25,3.0],"flight":[.25,3.0],"fling":[0.0,3.0],"stamina":[.25,3.0],"starting_health":[1,100],"starting_shield":[0,100],"starting_builds":[0,100000],"respawn_delay":[1.0,30.0],"protection":[0.0,10.0],"downed_seconds":[10.0,180.0],"time_limit":[0,3600],"score_limit":[0,500],"rounds":[1,10],"zone_damage":[0.0,50.0],"zone_speed":[.1,20.0],"zone_wait":[0.0,300.0]}
const CHOICES := {"map":["valley","range","cyber_bird","community_local"],"mode":["revive","respawn"],"loadout":["standard","pistols","swords"]}
static func validate(input:Dictionary)->Dictionary:
	var result =DEFAULTS.duplicate(true)
	for key in DEFAULTS:
		if not input.has(key):continue
		var v=input[key]
		if CHOICES.has(key):
			if key=="map" and v is String and v.begins_with("archive:"):
				if preload("res://scripts/archive_catalog1003.gd").playable(v):result[key]=v
				continue
			if key=="map" and v=="community_local" and not preload("res://scripts/community_catalog1002.gd").installed():continue
			if v is String and v in CHOICES[key]:result[key]=v
		elif DEFAULTS[key] is bool:
			if v is bool:result[key]=v
		elif LIMITS.has(key) and (v is int or v is float) and is_finite(float(v)):
			result[key]=clampi(int(v),int(LIMITS[key][0]),int(LIMITS[key][1])) if DEFAULTS[key] is int else clampf(float(v),float(LIMITS[key][0]),float(LIMITS[key][1]))
	return result
static func preset(id:String)->Dictionary:
	var r =DEFAULTS.duplicate(true)
	match id:
		"practice":r.merge({"mode":"respawn","damage":0.0,"catalog":true,"infinite_ammo":true,"infinite_builds":true,"score_limit":0,"time_limit":0,"zone_enabled":false},true)
		"team_respawn":r.mode="respawn"
		"free_for_all":r.mode="respawn";r.teams=0
	return r
static func starting_catalog(r:Dictionary)->Array:
	if str(r.map) in ["archive:magnum-chaosvr","archive:magnum-3-chaosvr"]:return [7]
	match str(r.loadout):
		"pistols":return [2,7,19]
		"swords":return [14,15,16]
	return [0,2,9]

const LIVE_R5=["walk","run","flight","fling","stamina","damage","npc_damage","build_damage","headshots","zone_damage","time_limit","score_limit"]
static func editable(stage:String,key:String)->bool:return stage in ["lobby","countdown"] or (stage=="match" and key in LIVE_R5)
