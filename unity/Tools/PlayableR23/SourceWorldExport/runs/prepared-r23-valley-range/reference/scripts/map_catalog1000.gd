extends RefCounted
const IDS := ["valley","range","cyber_bird","community_local"]
const DATA := {
	"valley":{"title":"RELAY VALLEY","subtitle":"Industrial ridges · long sightlines","min":Vector2(-100,-130),"max":Vector2(100,70),"origin":Vector3.ZERO},
	"range":{"title":"SANDBOX RANGE","subtitle":"Practice arenas · weapons and revival","min":Vector2(-50,-50),"max":Vector2(50,50),"origin":Vector3(0,0,600)},
	"cyber_bird":{"title":"CYBER BIRD","subtitle":"Avian megacity · 144 buildings · open windows","min":Vector2(-400,-400),"max":Vector2(400,400),"origin":Vector3(0,0,2400)}}
static func info(id:String)->Dictionary:
	if id.begins_with("archive:"):
		var row=preload("res://scripts/archive_catalog1003.gd").info(id);var meta=preload("res://scripts/archive_catalog1003.gd").manifest(id);var low:Array=meta.get("bounds_min",[-1,0,-1]);var high:Array=meta.get("bounds_max",[1,0,1])
		return {"title":str(row.get("title",id)),"subtitle":str(row.get("creator","Unknown"))+" · Local archive","min":Vector2(low[0],low[2]),"max":Vector2(high[0],high[2]),"origin":Vector3(0,0,4800)}
	if id=="community_local":
		var m=preload("res://scripts/community_catalog1002.gd").manifest()
		var low:Array=m.get("bounds_min",[-450,0,-510]);var high:Array=m.get("bounds_max",[460,80,500])
		return {"title":str(m.get("title","LOCAL COMMUNITY")),"subtitle":str(m.get("creator","Not installed"))+" · Local test","min":Vector2(low[0],low[2]),"max":Vector2(high[0],high[2]),"origin":Vector3(0,0,4800)}
	return DATA.get(id,DATA.range)
static func image(id:String)->Texture2D:
	if id.begins_with("archive:"):return preload("res://scripts/archive_catalog1003.gd").image(id)
	if id=="community_local":return preload("res://scripts/community_catalog1002.gd").image()
	var path ="res://assets/maps1000/%s.png"%id
	return load(path) if ResourceLoader.exists(path) else null
static func ids()->Array:
	var result:Array=["valley","range","cyber_bird"]
	if preload("res://scripts/community_catalog1002.gd").installed():result.append("community_local")
	for row in preload("res://scripts/archive_catalog1003.gd").rows():result.append(row.id)
	return result
static func uv(id:String,position:Vector3)->Vector2:
	var row =info(id);var local:Vector3=position-row.origin
	return (Vector2(local.x,local.z)-row.min)/(row.max-row.min)
