extends RefCounted
## Explicit, reviewed action routing. Unknown cues retain the authored bank.
const PATH:="res://assets/audio/reference0992/manifest.json"
static var _manifest:Dictionary={}
static var _cache:Dictionary={}
static func enabled()->bool:return not OS.get_cmdline_user_args().has("--authored-audio")
static func manifest()->Dictionary:
	if _manifest.is_empty():_manifest=JSON.parse_string(FileAccess.get_file_as_string(PATH))
	return _manifest
static func variant(paths:Array,index:int,loop:bool=false)->AudioStreamWAV:
	if paths.is_empty():return null
	var path:String=paths[posmod(index,paths.size())]
	var cache_key:String=path+(":loop" if loop else ":once")
	if not _cache.has(cache_key):
		var sound:AudioStreamWAV=AudioStreamWAV.load_from_file(path) if FileAccess.file_exists(path) else load(path) as AudioStreamWAV
		if sound==null:return null
		sound=sound.duplicate();sound.loop_mode=AudioStreamWAV.LOOP_DISABLED
		if loop:sound.loop_mode=AudioStreamWAV.LOOP_FORWARD;sound.loop_begin=0;sound.loop_end=sound.data.size()/2
		_cache[cache_key]=sound
	return _cache[cache_key]
static func generic(kind:String,index:int)->AudioStreamWAV:
	if not enabled():return null
	if kind=="revive_charge_full_r21":return variant(["res://assets/audio/reference_r21/revive_charge_full.wav"],0)
	return variant(manifest().generic.get(kind,[]),index,kind in ["katana_shield","glide","build_lightning"])
static func weapon(kind:String,owner_gun:Node3D,index:int)->AudioStreamWAV:
	if not enabled():return null
	var id:=str(owner_gun.get_meta("reference_id",""))
	if id=="uzi":id="uzis"
	var specific:Array=manifest().generic.get("bolt",[]) if id=="awp" and kind=="bolt" else manifest().weapons.get(id,{}).get(kind,[])
	if specific.is_empty() and kind in ["mag_eject","pouch","mag_insert","bolt","open","close","latch","pump","shell_insert"]:specific=manifest().generic.get(kind,[])
	return variant(specific,index)
static func action(kind:String,subject:String,index:int)->AudioStreamWAV:
	if not enabled():return null
	return variant(manifest().actions.get(subject,{}).get(kind,[]),index,kind=="shield_sustain")
