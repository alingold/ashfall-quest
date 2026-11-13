extends RefCounted
## READY uses source multipliers without editing saved preferences.
const Source=preload("res://scripts/map_rules_r14.gd")
const KEY="solo_source_rules_r14"
static func _same(a:Variant,b:Variant)->bool:
	return is_equal_approx(float(a),float(b)) if (a is float or a is int) and (b is float or b is int) else a==b
static func _assign(state:Dictionary,obj:Node,key:String,value:Variant):
	state.entries.append({"object":weakref(obj),"key":key,"previous":obj.get(key),"applied":value});obj.set(key,value)
static func apply(game:Node,map_id:String):
	if game.network_bridge.live():return
	restore(game)
	var patch:Dictionary=Source.patch(map_id)
	if patch.is_empty():return
	var state:Dictionary={"map":map_id,"entries":[],"patch":patch,"reserves":game.weapon.reserves.duplicate(true)}
	var player:Node=game.player
	if patch.has("walk"):_assign(state,player,"walk_speed_multiplier",minf(1.0,player.walk_speed_multiplier)*float(patch.walk))
	if patch.has("run"):
		_assign(state,player,"sprint_slow_gain",player.sprint_slow_gain*float(patch.run));_assign(state,player,"sprint_fast_gain",player.sprint_fast_gain*float(patch.run))
	if patch.has("flight"):_assign(state,player,"network_flight_multiplier",float(patch.flight))
	if patch.has("fling"):_assign(state,player,"fling_power",float(patch.fling))
	if patch.has("stamina"):_assign(state,player,"sprint_stamina_seconds",player.sprint_stamina_seconds*float(patch.stamina))
	if patch.has("damage"):_assign(state,game.field_menu,"player_damage_multiplier",float(patch.damage))
	if patch.has("npc_damage"):_assign(state,game.field_menu,"enemy_damage_multiplier",float(patch.npc_damage))
	if patch.has("starting_builds"):_assign(state,game.field_builder,"charges",int(patch.starting_builds))
	if patch.has("starting_health"):game.health=float(patch.starting_health)
	if patch.has("starting_shield"):game.shield=float(patch.starting_shield)
	game.set_meta(KEY,state);tick(game)
static func tick(game:Node):
	if not game.has_meta(KEY):return
	var patch:Dictionary=game.get_meta(KEY).patch
	if patch.get("infinite_ammo",false):
		for key in game.weapon.reserves:game.weapon.reserves[key]=maxi(1000000,int(game.weapon.reserves[key]))
	if patch.get("infinite_builds",false):game.field_builder.charges=maxi(999,game.field_builder.charges)
static func restore(game:Node):
	if not game.has_meta(KEY):return
	var state:Dictionary=game.get_meta(KEY);game.remove_meta(KEY)
	for entry in state.entries:
		var obj:Node=entry.object.get_ref()
		if is_instance_valid(obj) and _same(obj.get(entry.key),entry.applied):obj.set(entry.key,entry.previous)
	if state.patch.get("infinite_ammo",false):game.weapon.reserves=state.reserves.duplicate(true)
static func personal_settings(game:Node,settings:Dictionary)->Dictionary:
	var result:Dictionary=settings.duplicate(true)
	if not game.has_meta(KEY):return result
	for entry in game.get_meta(KEY).entries:
		var obj:Node=entry.object.get_ref()
		if not is_instance_valid(obj) or (obj!=game.player and obj!=game.field_menu) or not result.has(entry.key):continue
		if _same(obj.get(entry.key),entry.applied):result[entry.key]=entry.previous
	return result

