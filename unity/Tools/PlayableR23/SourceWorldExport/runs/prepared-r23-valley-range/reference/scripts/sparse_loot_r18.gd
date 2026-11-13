extends RefCounted
static func add_sites(map:Node,sites:Array[Vector3])->int:
 var original:int=sites.size();var low:Array=map._meta.bounds_min;var high:Array=map._meta.bounds_max
 var area:float=(float(high[0])-float(low[0]))*(float(high[2])-float(low[2]))
 if sites.size()>=maxi(16,int(area/(65*65))):return 0
 var candidates:Array[Vector3]=[]
 candidates.append_array(map.spawn_points)
 # Existing coarse rays are validated floors. Follow them down through several playable layers.
 for top in map.loot_points:
  candidates.append(top-Vector3.UP*.48)
  var previous:Vector3=top
  for layer in 3:
   var below:Vector3=map._floor(previous-Vector3.UP*.9,35)
   if not below.is_finite():break
   candidates.append(below+Vector3.UP*.12);previous=below
 for candidate in candidates:
  if not candidate.is_finite() or map._outside(candidate) or map._hazard(candidate+Vector3.UP*.8):continue
  if not map.safe_spawn_r8(candidate):continue
  var too_close=false
  for old in sites:
   if absf(old.y-candidate.y)<3 and Vector2(old.x-candidate.x,old.z-candidate.z).length_squared()<28*28:too_close=true;break
  if too_close:continue
  sites.append(candidate+Vector3.UP*.53)
  if sites.size()-original>=160:break
 return sites.size()-original
