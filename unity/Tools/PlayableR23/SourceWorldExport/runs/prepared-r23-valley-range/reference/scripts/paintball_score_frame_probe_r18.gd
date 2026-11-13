extends RefCounted
## Capture-fixture compatibility entry point; production uses the same native-verified source helper.
static func rows(geometry:Node,map_id:String,fingerprint:String)->Array:
 return preload("res://scripts/archive_score_surfaces_r18.gd").faces(geometry,map_id,fingerprint)
