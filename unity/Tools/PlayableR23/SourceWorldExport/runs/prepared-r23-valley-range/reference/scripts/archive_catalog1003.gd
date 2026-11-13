extends RefCounted
const ROOT="res://local_archive/"
static var _catalog:Dictionary={}
static var _hashes:Dictionary={}
static var mounted_pack_r7=""
static var _mount_checked_r7=false
static var mounted_adapters_r8=""
static func mount_local_pack_r7()->void:
	if _mount_checked_r7:return
	_mount_checked_r7=true
	var paths:Array[String]=[]
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--local-maps-pack="):paths.append(arg.trim_prefix("--local-maps-pack="))
	# Android native executables live in a read-only APK location. Optional library
	# packs are installed into the app files directory, without broad storage access.
	if OS.get_name()=="Android":paths.append(OS.get_user_data_dir().path_join("map-packs/ASHFALL-Local-Maps.pck"))
	paths.append(OS.get_executable_path().get_base_dir().path_join("ASHFALL-Local-Maps.pck"))
	for path in paths:
		if FileAccess.file_exists(path) and ProjectSettings.load_resource_pack(path,true):
			mounted_pack_r7=path
			var adapters:String=path.get_base_dir().path_join("ASHFALL-Map-Adapters-R10.pck")
			if FileAccess.file_exists(adapters) and ProjectSettings.load_resource_pack(adapters,true):mounted_adapters_r8=adapters
			break
static func catalog()->Dictionary:
	mount_local_pack_r7()
	if _catalog.is_empty() and FileAccess.file_exists(ROOT+"catalog.json"):
		var row=JSON.parse_string(FileAccess.get_file_as_string(ROOT+"catalog.json"));_catalog=row if row is Dictionary else {}
	return _catalog
static func rows()->Array:return catalog().get("maps",[])
static func info(id:String)->Dictionary:
	for row in rows():
		if row.id==id:return row
	return {}
static func playable(id:String)->bool:
	var row=info(id);var status=str(row.get("status",""))
	return (status=="ready" or (status=="validating" and OS.get_cmdline_user_args().has("--archive-validation"))) and FileAccess.file_exists(scene(id))
static func slug(id:String)->String:return str(info(id).get("slug",""))
static func scene(id:String)->String:return ROOT+"maps/"+slug(id)+"/converted/map.scn"
static func manifest(id:String)->Dictionary:
	var path=ROOT+"maps/"+slug(id)+"/converted/manifest.json"
	return JSON.parse_string(FileAccess.get_file_as_string(path)) if FileAccess.file_exists(path) else {}
static func image(id:String)->Texture2D:
	var path=ROOT+"maps/"+slug(id)+"/converted/preview.png"
	if not FileAccess.file_exists(path):path=ROOT+"maps/"+slug(id)+"/source/thumb.jpg"
	if not FileAccess.file_exists(path):return null
	var image:=Image.new();var bytes:=FileAccess.get_file_as_bytes(path)
	var result:int=image.load_png_from_buffer(bytes) if path.ends_with(".png") else image.load_jpg_from_buffer(bytes)
	return ImageTexture.create_from_image(image) if result==OK else null
static func fingerprint(id:String)->String:
	if not FileAccess.file_exists(scene(id)):return "not-installed"
	if not _hashes.has(id):
		var m=manifest(id);var value=FileAccess.get_sha256(scene(id))+"\n"
		for dependency in m.get("dependencies",[]):value+=str(dependency.path)+":"+FileAccess.get_sha256(str(dependency.path))+"\n"
		_hashes[id]=str(m.get("cache_fingerprint","invalid-manifest")) if value.sha256_text()==str(m.get("local_cache_hash","")) else "invalid-cache:"+value.sha256_text()
	return _hashes[id]
static func verify(id:String)->bool:
	if fingerprint(id)!=str(manifest(id).get("cache_fingerprint","missing")):return false
	var path:String="res://local_archive_r10/"+slug(id)+".res"
	if not FileAccess.file_exists(path) or not FileAccess.file_exists("res://assets/data/archive_adapters_r10.json"):return false
	var index:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/archive_adapters_r10.json"))
	return str(index.get("files",{}).get(slug(id),""))==FileAccess.get_sha256(path)
static func library_id()->String:return str(catalog().get("content_id","no-archive"))
