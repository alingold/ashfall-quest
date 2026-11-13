extends SceneTree
## R14/private-content integration gate. This is not the public redistribution gate.
var failures:Array[String]=[]
var checks=0
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);printerr("QUEST_INTEGRATION_FAILED ",label)
func run():
	check(not XRServer.primary_interface or not XRServer.primary_interface.is_initialized(),"XR remains off during host checks")
	var exports:=ConfigFile.new();check(exports.load("res://export_presets.cfg")==OK,"export configuration parses")
	check(exports.get_value("preset.0","platform")=="Windows Desktop","Windows target retained")
	check(exports.get_value("preset.1","name")=="Quest 3","Quest preset merged")
	check(exports.get_value("preset.1.options","package/name")=="ASHFALL R23 Quest","current Android launcher label")
	for key in ["gradle_build/use_gradle_build","architectures/arm64-v8a","xr_features/enable_meta_plugin","meta_xr_features/quest_3_support","permissions/internet","permissions/record_audio"]:check(exports.get_value("preset.1.options",key,false)==true,key)
	for key in ["architectures/armeabi-v7a","architectures/x86","architectures/x86_64"]:check(not exports.get_value("preset.1.options",key,true),key+" excluded")
	check(exports.get_value("preset.1.options","xr_features/xr_mode")==1,"OpenXR selected")
	var flags:String=exports.get_value("preset.1.options","command_line/extra_args","")
	for flag in ["--require-vr","--extended","--playtest-catalog"]:check(flags.contains(flag),"full-content device flag "+flag)
	for flag in ["--desktop","--low-poly","--smoke"]:check(not flags.contains(flag),"no forced desktop/reduced-content flag "+flag)
	check(str(exports.get_value("preset.1","include_filter","")).contains("assets/data/*.json"),"all R14 gameplay catalogs included")
	for name in ["map_rules_r14","map_semantics_r14","map_art_audit_r14","archive_adapters_r10","network_content1000"]:
		var file:String="res://assets/data/"+name+".json";check(FileAccess.file_exists(file),name+" present");check(JSON.parse_string(FileAccess.get_file_as_string(file)) is Dictionary,name+" valid")
	check(preload("res://scripts/map_rules_r14.gd").patch("archive:alfa-aglae-souza").get("walk")==2.5,"requested default250percent walk")
	check(preload("res://scripts/map_semantics_r14.gd").catalog().get("maps",{}).size()==105,"105-map semantic library retained")
	for row in JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_rules_r14.json")).maps:
		var patch:Dictionary=preload("res://scripts/map_rules_r14.gd").patch(str(row.id))
		check(is_equal_approx(float(patch.walk),2.5) and is_equal_approx(float(patch.fling),3.0),"all-map preferred walk/fling "+str(row.id))
	for id in ["valley","range","cyber_bird"]:
		var patch:Dictionary=preload("res://scripts/map_rules_r14.gd").patch(id)
		check(is_equal_approx(float(patch.walk),2.5) and is_equal_approx(float(patch.fling),3.0),"built-in preferred walk/fling "+id)
	check(preload("res://scripts/team_launch_r12.gd").SLOTS==12,"twelve-pod platforms retained")
	check(preload("res://scripts/flight_trail0992.gd").RADIUS==.065,"thicker flight trails retained")
	check(ResourceLoader.exists("res://assets/ghost_r13.gdshader"),"team ghost shader retained")
	check(ProjectSettings.get_setting("rendering/textures/vram_compression/import_etc2_astc",false),"mobile texture variants enabled")
	check(ProjectSettings.get_setting("application/config/custom_user_dir_name")=="Ashfall-R14-MapSystems-Local","personal settings directory retained")
	var lod_index:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_lod_index_r16.json"))
	check(lod_index.maps.size()==105,"105 maps have distance LOD records")
	for row in lod_index.maps.values():check(FileAccess.get_sha256(str(row.path))==str(row.sha256),"LOD resource matches source hash "+str(row.path))
	check(not preload("res://scripts/team_launch_r12.gd").uses_sky_platform("archive:battlefield-paintball-gods-pitdevil"),"Paintball sky deployment disabled")
	var lock:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/quest_integration_r15.json"))
	check(ProjectSettings.get_setting("application/config/version")==lock.android_version,"current APK/source version matches dependency lock")
	check(not lock.initialize_is_manual,"R19 automatically initializes at stable tracked startup")
	for script in ["ping_sprite_r19","ping_style_r19","quest_pose_capture_r19","render_diagnostics_r19","archive_score_surfaces_r19","map_art_r19","postcut_lods_r19"]:
		check(ResourceLoader.exists("res://scripts/"+script+".gd"),"R19 runtime helper present "+script)
	var postcut:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/postcut_lods_r19/index.json"))
	for slug in postcut:check(FileAccess.get_sha256("res://assets/data/postcut_lods_r19/"+slug+".res")==postcut[slug],"R19 postcut cache hash "+slug)
	var art:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r19.json"))
	check(art.maps.size()==11,"R19 original art covers eleven matching maps")
	for row in art.maps.values():check(FileAccess.get_sha256(row.path)==row.sha256,"R19 art cache hash "+row.path)
	check(FileAccess.file_exists("res://assets/art_r19/ATTRIBUTION.json"),"R19 original art credits included")
	var repairs:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/hatch_backing_r21.json"))
	check(repairs.maps.size()==9,"R21 nine matching tower variants")
	for row in repairs.maps.values():check(FileAccess.get_sha256(row.path)==row.sha256,"R21 backing cache "+row.path)
	check(preload("res://scripts/ground_item_visibility_r21.gd").DISTANCE_M==40.0,"requested40m ground item visibility (R23)")
	for path in ["res://assets/art_r21/ATTRIBUTION.json","res://assets/audio/reference_r21/manifest.json","res://assets/audio/reference_r21/ATTRIBUTION.txt"]:check(FileAccess.file_exists(path),"R21 provenance "+path)
	if failures.is_empty():print("QUEST_INTEGRATION_OK checks=",checks)
	quit(0 if failures.is_empty() else 1)
