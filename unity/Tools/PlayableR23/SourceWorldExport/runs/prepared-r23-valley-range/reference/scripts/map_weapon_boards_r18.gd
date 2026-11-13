extends RefCounted
## Source-proven signs only. Return a fresh local list; physics validates access at activation.
const DATA_PATH="res://assets/data/map_fixtures_r18.json"
const DATA_SHA="6db93c272cf6efacbd8dbe928905f002dfefbd8ff23ef6d8a672fbceb5fabb38"
static var _data:Dictionary={}
static func catalog()->Dictionary:
 if _data.is_empty():
  var bytes:PackedByteArray=FileAccess.get_file_as_bytes(DATA_PATH)
  var hash=HashingContext.new();hash.start(HashingContext.HASH_SHA256);hash.update(bytes)
  if hash.finish().hex_encode()!=DATA_SHA:return {}
  # Share the immutable parsed fixture catalog instead of retaining a second
  # full1344-sign provenance tree on memory-constrained rendered clients.
  _data=preload("res://scripts/map_fixtures_r18.gd").catalog()
 return _data
static func _vector(value:Variant)->Vector3:
 if not value is Array or value.size()!=3:return Vector3.INF
 return Vector3(float(value[0]),float(value[1]),float(value[2]))
static func merge(existing:Array,map_id:String,fingerprint:String)->Array:
 var result:Array=existing.duplicate(true)
 var slug:String=map_id.trim_prefix("archive:")
 var row:Dictionary=catalog().get("maps",{}).get(slug,{})
 if fingerprint.is_empty() or str(row.get("cache_fingerprint",""))!=fingerprint:return result
 var staged:Array=[]
 for source in row.get("weapon_boards_r18",{}).get("boards",[]):
  var lo:Vector3=_vector(source.get("box_min",[]));var hi:Vector3=_vector(source.get("box_max",[]));var normal:Vector3=_vector(source.get("normal",[]))
  if not lo.is_finite() or not hi.is_finite() or not normal.is_finite():return result
  if (hi-lo).x<=0 or (hi-lo).y<=0 or (hi-lo).z<=0 or absf(normal.length()-1)>.001 or absf(normal.y)>.01:return result
  if source.get("word","")!="WEAPONS" or source.get("letters",[]).size()!=7 or int(source.get("selector",{}).get("total_letter_triangles",0))!=688:return result
  var candidate:Dictionary={"box":AABB(lo,hi-lo),"normal":normal,"board_id":str(source.board_id),"source_selector_r18":source.selector.duplicate(true)}
  if source.has("floor_hint") and is_finite(float(source.floor_hint)):candidate.floor_hint=float(source.floor_hint)
  staged.append(candidate)
 for candidate in staged:
  var duplicate=false
  for old in result:
   if old.box.get_center().distance_squared_to(candidate.box.get_center())<1.0 and (old.normal as Vector3).normalized().dot(candidate.normal)>.95:duplicate=true;break
  if not duplicate:result.append(candidate)
 return result
