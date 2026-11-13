extends RefCounted
## Optional local data slot. No imported map or textures belong to this source license.
const ROOT="res://local_community/converted/"
static var _scene_hash=""
static func installed()->bool:return FileAccess.file_exists(ROOT+"manifest.json") and FileAccess.file_exists(ROOT+"map.scn")
static func manifest()->Dictionary:
	if not installed():return {}
	var data=JSON.parse_string(FileAccess.get_file_as_string(ROOT+"manifest.json"))
	return data if data is Dictionary else {}
static func fingerprint()->String:
	if not installed():return "not-installed"
	if _scene_hash.is_empty():_scene_hash=FileAccess.get_sha256(ROOT+"map.scn")
	return _scene_hash
static func image()->Texture2D:
	var path=ROOT+"preview.png"
	if not FileAccess.file_exists(path):path="res://local_community/source/poster.jpg"
	return ImageTexture.create_from_image(Image.load_from_file(path)) if FileAccess.file_exists(path) else null
