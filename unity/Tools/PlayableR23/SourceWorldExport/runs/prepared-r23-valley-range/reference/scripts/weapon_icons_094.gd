extends RefCounted
static var _cache:Dictionary={}
static func icon(index:int)->Texture2D:
	if not _cache.has(index):
		var path:="res://assets/ui/catalog0991/weapon_%d.png"%index
		if not ResourceLoader.exists(path):path="res://assets/ui/catalog094/weapon_%d.png"%index
		if not ResourceLoader.exists(path):path="res://assets/ui/wheel_weapon_%d.svg"%index
		_cache[index]=load(path)
	return _cache[index]
