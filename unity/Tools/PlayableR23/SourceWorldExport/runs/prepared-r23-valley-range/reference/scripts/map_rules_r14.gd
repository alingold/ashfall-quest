extends RefCounted
## Pure map defaults. Caller applies these transiently; this never writes user preferences.
const Rules=preload("res://scripts/net_rules1000.gd")
static var _maps:Dictionary={}
static func _load()->void:
	if not _maps.is_empty():return
	var data=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_rules_r14.json"))
	if data is Dictionary:
		for row in data.get("maps",[]):_maps[row.id]=row
static func patch(map_id:String)->Dictionary:
	_load();var result:Dictionary=_maps.get(map_id,{}).get("patch",{}).duplicate(true)
	result.walk=2.5;result.fling=3.0 # Explicit user preference for every map, editable per session.
	return result
static func audit(map_id:String)->Dictionary:
	_load();return _maps.get(map_id,{}).duplicate(true)
static func resolve(map_id:String,base:Dictionary={},explicit_host:Dictionary={})->Dictionary:
	var values:Dictionary=Rules.DEFAULTS.duplicate(true);values.merge(base,true);values.merge(patch(map_id),true);values.merge(explicit_host,true);values.map=map_id
	return Rules.validate(values)
