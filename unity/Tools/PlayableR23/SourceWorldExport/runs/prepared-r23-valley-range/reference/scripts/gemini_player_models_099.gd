extends RefCounted
## Overhauled ASHFALL Player Models (0: APEX CAMO, 1: WITCH DOCTOR, 2: ECLIPSE RONIN).
## Preserves the exact 13-mesh player_body.gd contract, camera-eye AABB clearance,
## and SkinnedGlove086 wrist/palm alignment.

const STYLES := ["APEX CAMO · ARGON", "WITCH DOCTOR · ARGON ORIGINAL", "ECLIPSE RONIN · ARGON", "WITCH DOCTOR · GPT-6 ASTRA", "WITCH DOCTOR · GPT-6.1 SOL", "WITCH DOCTOR · GEMINI 4 ARGON", "WITCH DOCTOR · CLAUDE OPUS", "OBSIDIAN", "PBAN", "PJ", "Roy", "Hattie", "Warthog", "Jack-O", "BROODMOTHER", "The Pilgrim", "Bone Collector", "Tormentus", "AXEL (CUPID)", "Valentina (Cupid)", "Kringle (Beach Boy)", "Yesbot", "Anubis (Paw and Order)", "Roy (Trickshot)", "Millie (Punk)", "Anna (Tabby)", "Ace (Sergeant)", "Ace (Trainee)", "Hattie (Varsity)", "PJ (Adventurer)", "Roy (Daring)", "Anna (Rainbow)", "Hattie (Incognito)", "Millie (Defiant)", "Anna", "Smiley", "Haze (Nuclear)", "Ace", "Millie", "Dani (Checkers)", "Grog Swiller", "Headless Horseman", "Tox", "Void Wanderer", "Wingman", "Prism", "Blair (Pumpkin Spice)", "Victoria (Scarlet)", "Raven", "Logan (Maverick)", "The Phoenix", "Kaiju", "Reaper"]
const STYLE_KEYS := ["camo", "witch_doctor", "eclipse_ronin", "astra", "sol", "argon", "claude", "obsidian", "pban", "pj", "roy", "hattie", "warthog", "jack_o", "broodmother", "the_pilgrim", "bone_collector", "tormentus", "axel_cupid", "valentina_cupid", "kringle_beach_boy", "yesbot", "anubis_paw_and_order", "roy_trickshot", "millie_punk", "anna_tabby", "ace_sergeant", "ace_trainee", "hattie_varsity", "pj_adventurer", "roy_daring", "anna_rainbow", "hattie_incognito", "millie_defiant", "anna", "smiley", "haze_nuclear", "ace", "millie", "dani_checkers", "grog_swiller", "headless_horseman", "tox", "void_wanderer", "wingman", "prism", "blair_pumpkin_spice", "victoria_scarlet", "raven", "logan_maverick", "the_phoenix", "kaiju", "reaper"]
const MODEL_DIR := "res://assets/models/gemini_players099/"
const TEX_DIR := "res://assets/models/gemini_players099/textures/"

const EXTRA_KITS := {"pban": {"dir": "res://assets/models/custom_skins/pban/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "pj": {"dir": "res://assets/models/custom_skins/pj/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "roy": {"dir": "res://assets/models/custom_skins/roy/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "hattie": {"dir": "res://assets/models/custom_skins/hattie/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "warthog": {"dir": "res://assets/models/custom_skins/warthog/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "jack_o": {"dir": "res://assets/models/custom_skins/jack_o/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "broodmother": {"dir": "res://assets/models/custom_skins/broodmother/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "the_pilgrim": {"dir": "res://assets/models/custom_skins/the_pilgrim/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "bone_collector": {"dir": "res://assets/models/custom_skins/bone_collector/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "tormentus": {"dir": "res://assets/models/custom_skins/tormentus/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "axel_cupid": {"dir": "res://assets/models/custom_skins/axel_cupid/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "valentina_cupid": {"dir": "res://assets/models/custom_skins/valentina_cupid/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "kringle_beach_boy": {"dir": "res://assets/models/custom_skins/kringle_beach_boy/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "yesbot": {"dir": "res://assets/models/custom_skins/yesbot/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "anubis_paw_and_order": {"dir": "res://assets/models/custom_skins/anubis_paw_and_order/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "roy_trickshot": {"dir": "res://assets/models/custom_skins/roy_trickshot/", "eye_to_shoulder": 0.18, "body_rear": 0.02}, "millie_punk": {"dir": "res://assets/models/custom_skins/millie_punk/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "anna_tabby": {"dir": "res://assets/models/custom_skins/anna_tabby/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "ace_sergeant": {"dir": "res://assets/models/custom_skins/ace_sergeant/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "ace_trainee": {"dir": "res://assets/models/custom_skins/ace_trainee/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "hattie_varsity": {"dir": "res://assets/models/custom_skins/hattie_varsity/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "pj_adventurer": {"dir": "res://assets/models/custom_skins/pj_adventurer/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "roy_daring": {"dir": "res://assets/models/custom_skins/roy_daring/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "anna_rainbow": {"dir": "res://assets/models/custom_skins/anna_rainbow/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "hattie_incognito": {"dir": "res://assets/models/custom_skins/hattie_incognito/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "millie_defiant": {"dir": "res://assets/models/custom_skins/millie_defiant/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "anna": {"dir": "res://assets/models/custom_skins/anna/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "smiley": {"dir": "res://assets/models/custom_skins/smiley/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "haze_nuclear": {"dir": "res://assets/models/custom_skins/haze_nuclear/", "eye_to_shoulder": 0.18, "body_rear": 0.06, "glove_material_parameters": {"metallic_specular": 0.16, "normal_scale": 1.0}}, "ace": {"dir": "res://assets/models/custom_skins/ace/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "millie": {"dir": "res://assets/models/custom_skins/millie/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "dani_checkers": {"dir": "res://assets/models/custom_skins/dani_checkers/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "grog_swiller": {"dir": "res://assets/models/custom_skins/grog_swiller/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "headless_horseman": {"dir": "res://assets/models/custom_skins/headless_horseman/", "eye_to_shoulder": 0.18, "body_rear": 0.02}, "tox": {"dir": "res://assets/models/custom_skins/tox/", "eye_to_shoulder": 0.18, "body_rear": 0.02, "chestplate_follow_jacket": true}, "void_wanderer": {"dir": "res://assets/models/custom_skins/void_wanderer/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "wingman": {"dir": "res://assets/models/custom_skins/wingman/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "prism": {"dir": "res://assets/models/custom_skins/prism/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "blair_pumpkin_spice": {"dir": "res://assets/models/custom_skins/blair_pumpkin_spice/", "eye_to_shoulder": 0.21, "body_rear": 0.0, "chestplate_follow_jacket": true}, "victoria_scarlet": {"dir": "res://assets/models/custom_skins/victoria_scarlet/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "raven": {"dir": "res://assets/models/custom_skins/raven/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "logan_maverick": {"dir": "res://assets/models/custom_skins/logan_maverick/", "eye_to_shoulder": 0.21, "body_rear": 0.04}, "the_phoenix": {"dir": "res://assets/models/custom_skins/the_phoenix/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "kaiju": {"dir": "res://assets/models/custom_skins/kaiju/", "eye_to_shoulder": 0.18, "body_rear": 0.06}, "reaper": {"dir": "res://assets/models/custom_skins/reaper/", "eye_to_shoulder": 0.18, "body_rear": 0.06}}

static var _kit_cache: Dictionary = {}
static var _glove_mat_cache: Dictionary = {}

static func _load_kit_meshes(style: int) -> Dictionary:
	var idx := posmod(style, STYLE_KEYS.size())
	var key:String = STYLE_KEYS[idx]
	var is_light: bool = preload("res://scripts/asset_quality.gd").lightweight()
	var cache_id := "%s_%s" % [key, "light" if is_light else "pcvr"]
	if _kit_cache.has(cache_id):
		return _kit_cache[cache_id]
	var glb_path := MODEL_DIR + ("player_kit_%s_%s.glb" % [key, "light" if is_light else "pcvr"])
	if idx>=3:glb_path="res://assets/models/witch_doctors_r8/witch_doctor_%s.glb"%STYLE_KEYS[idx]
	if idx==6:glb_path="res://assets/models/witch_doctors_r8/witch_doctor_claude_%s.glb"%("light" if is_light else "pcvr")
	if key=="obsidian":glb_path="res://assets/models/obsidian/player_kit_obsidian_%s.glb"%("light" if is_light else "pcvr")
	if EXTRA_KITS.has(key):glb_path=EXTRA_KITS[key]["dir"]+"player_kit_%s_%s.glb"%[key,"light" if is_light else "pcvr"]
	# R23 Quest package ships only the PCVR tier of custom/obsidian kits (1024px textures).
	if is_light and not ResourceLoader.exists(glb_path) and ResourceLoader.exists(glb_path.replace("_light.glb","_pcvr.glb")):glb_path=glb_path.replace("_light.glb","_pcvr.glb")
	var packed: PackedScene = load(glb_path)
	if packed == null:
		return {}
	var inst: Node3D = packed.instantiate()
	var meshes: Dictionary = {}
	for part in inst.find_children("*", "MeshInstance3D", true, false):
		meshes[String(part.name)] = part.mesh
	inst.free()
	_kit_cache[cache_id] = meshes
	return meshes

static func glove_material(style: int) -> BaseMaterial3D:
	var idx := posmod(style, STYLE_KEYS.size())
	if EXTRA_KITS.has(STYLE_KEYS[idx]):
		var custom_key:String=STYLE_KEYS[idx]
		if not _glove_mat_cache.has(custom_key):
			var custom_mat:=ORMMaterial3D.new()
			var directory:String=EXTRA_KITS[custom_key]["dir"]
			custom_mat.albedo_texture=load(directory+"glove_%s_albedo.png"%custom_key)
			custom_mat.orm_texture=load(directory+"glove_%s_orm.png"%custom_key)
			custom_mat.normal_enabled=true;custom_mat.normal_texture=load(directory+"glove_%s_normal.png"%custom_key)
			var custom_parameters:Dictionary=EXTRA_KITS[custom_key].get("glove_material_parameters",{})
			if custom_parameters.has("metallic_specular"):custom_mat.metallic_specular=float(custom_parameters["metallic_specular"])
			if custom_parameters.has("normal_scale"):custom_mat.normal_scale=float(custom_parameters["normal_scale"])
			custom_mat.ao_on_uv2=false;_glove_mat_cache[custom_key]=custom_mat
		return _glove_mat_cache[custom_key]
	if STYLE_KEYS[idx]=="obsidian":
		if not _glove_mat_cache.has("obsidian"):
			var mat:=ORMMaterial3D.new()
			mat.albedo_texture=load("res://assets/models/obsidian/glove_obsidian_albedo.png")
			mat.orm_texture=load("res://assets/models/obsidian/glove_obsidian_orm.png")
			mat.normal_enabled=true;mat.normal_texture=load("res://assets/models/obsidian/glove_obsidian_normal.png")
			mat.ao_on_uv2=false;_glove_mat_cache.obsidian=mat
		return _glove_mat_cache.obsidian
	if idx==6:
		if not _glove_mat_cache.has("claude"):
			var mat:=ORMMaterial3D.new();mat.albedo_texture=load("res://assets/models/witch_doctors_r8/glove_claude_albedo.png");mat.orm_texture=load("res://assets/models/witch_doctors_r8/glove_claude_orm.png");mat.normal_enabled=true;mat.normal_texture=load("res://assets/models/witch_doctors_r8/glove_claude_normal.png");_glove_mat_cache.claude=mat
		return _glove_mat_cache.claude
	if idx>=3:idx=1
	var key:String = STYLE_KEYS[idx]
	if _glove_mat_cache.has(key):
		return _glove_mat_cache[key]
	var mat := ORMMaterial3D.new()
	mat.albedo_texture = load(TEX_DIR + ("glove_%s_albedo.png" % key))
	mat.orm_texture = load(TEX_DIR + ("glove_%s_orm.png" % key))
	mat.normal_enabled = true
	mat.normal_texture = load(TEX_DIR + ("glove_%s_normal.png" % key))
	mat.ao_on_uv2 = false
	_glove_mat_cache[key] = mat
	return mat

static func apply_style(body: Node3D, style: int = 0) -> void:
	if body == null or not ("pieces" in body):
		return
	var idx := posmod(style, STYLE_KEYS.size())
	var meshes := _load_kit_meshes(idx)
	if meshes.is_empty():
		return
	for label in body.pieces.keys():
		if meshes.has(label):
			var piece: MeshInstance3D = body.pieces[label]
			piece.mesh = meshes[label]
			piece.material_override = null
		elif idx>=3:
			body.pieces[label].mesh=ArrayMesh.new()
	body.set_meta("character_style", idx)
	body.set_meta("detailed_field_suit_088", true)

	# If this body is inside a 3rd-person lobby/gallery avatar (FieldCharacter091),
	# also update the AvatarHead and Left/Right SkinnedGlove086 materials!
	var parent := body.get_parent()
	if parent != null and parent.name == "FieldCharacter091":
		_lobby_leg_frames(body,idx)
		parent.set_meta("character_style", idx)
		var head_node := parent.get_node_or_null("AvatarHead") as MeshInstance3D
		if head_node == null:
			head_node = MeshInstance3D.new()
			head_node.name = "AvatarHead"
			parent.add_child(head_node)
		if meshes.has("AvatarHead"):
			head_node.mesh = meshes["AvatarHead"]
			head_node.material_override = null
		var g_mat := glove_material(idx)
		for child in parent.get_children():
			if String(child.name).begins_with("SkinnedGlove086") or ("skeleton" in child and "side" in child):
				_lobby_hand_pose(child,idx)
				for gp in child.find_children("*", "MeshInstance3D", true, false):
					gp.material_override = g_mat

static func style_gloves(root:Node, style:int)->void:
	for mesh in root.find_children("*", "MeshInstance3D", true, false):
		if mesh.get_parent() is Skeleton3D:mesh.material_override=glove_material(style)

static func remote_head(parent:Node3D,style:int)->void:
	for child in parent.get_children():child.queue_free()
	var part:=MeshInstance3D.new();part.name="ArgonHead";part.mesh=_load_kit_meshes(style).get("AvatarHead");parent.add_child(part)
	# Kit heads are authored in standing avatar coordinates. Network head pose is eye space.
	part.position.y=-1.68

static func render_body_profile(style:int)->Vector2:
	var key:String=STYLE_KEYS[posmod(style,STYLE_KEYS.size())]
	if EXTRA_KITS.has(key):return Vector2(float(EXTRA_KITS[key]["eye_to_shoulder"]),float(EXTRA_KITS[key]["body_rear"]))
	if key=="obsidian":return Vector2(.18,.06)
	return Vector2(.29,.12)

static func chestplate_follows_jacket(style:int)->bool:
	var key:String=STYLE_KEYS[posmod(style,STYLE_KEYS.size())]
	return EXTRA_KITS.has(key) and bool(EXTRA_KITS[key].get("chestplate_follow_jacket",false))

static func _lobby_leg_frames(body:Node3D,style:int)->void:
	# The nearly downward gallery segments can twist the authored side stripe.
	# These reviewed Anna kits use a stable avatar-side axis in the static
	# gallery. Retain the original basis for exact restoration on other skins.
	var stabilize:bool=["anna_tabby","anna_rainbow","anna"].has(STYLE_KEYS[posmod(style,STYLE_KEYS.size())])
	for label in ["LThigh","LShin","RThigh","RShin"]:
		if not body.pieces.has(label):continue
		var piece:MeshInstance3D=body.pieces[label]
		if not stabilize:
			if piece.has_meta("original_gallery_leg_basis"):
				piece.basis=piece.get_meta("original_gallery_leg_basis")
			continue
		if not piece.has_meta("original_gallery_leg_basis"):
			piece.set_meta("original_gallery_leg_basis",piece.basis)
		var original:Basis=piece.get_meta("original_gallery_leg_basis")
		var along:Vector3=original.y.normalized()
		var across:Vector3=Vector3.RIGHT-along*Vector3.RIGHT.dot(along)
		if across.length_squared()<.000001:continue
		across=across.normalized()
		piece.basis=Basis(across,along,across.cross(along)).scaled_local(original.get_scale())

static func _glove_bone_local(glove:Node3D,bone_name:String)->Vector3:
	var sk:Skeleton3D=glove.skeleton
	var bone:int=sk.find_bone(bone_name)
	var point:Vector3=sk.get_bone_global_pose(bone).origin
	var mount:Node3D=sk
	while mount!=null and mount!=glove:
		point=mount.transform*point
		mount=mount.get_parent() as Node3D
	return point

static func _lobby_hand_pose(glove:Node3D,style:int)->void:
	if not ("skeleton" in glove and "side" in glove) or glove.skeleton==null:return
	var side:float=-1. if glove.side=="left" else 1.
	var canonical:=Basis.from_euler(Vector3(0,0,-side*.2))
	if not EXTRA_KITS.has(STYLE_KEYS[posmod(style,STYLE_KEYS.size())]):
		glove.basis=canonical;glove.position=Vector3(side*.26,.94,-.04);return
	# The lobby's old visual hand mount misses the wrist by 54 mm. Custom skins
	# use the real wrist bone and a relaxed fingers-down pose. Tracking is not involved.
	var suffix:String="_L" if side<0 else "_R"
	if not glove.has_meta("gallery_reference_099"):
		glove.set_meta("gallery_reference_099",{
			"wrist":_glove_bone_local(glove,"Wrist"+suffix),
			"knuckle":_glove_bone_local(glove,"Middle_Proximal"+suffix)})
	var reference:Dictionary=glove.get_meta("gallery_reference_099")
	var wrist:Vector3=reference["wrist"]
	var knuckle:Vector3=reference["knuckle"]
	var current:Vector3=(canonical*(knuckle-wrist)).normalized()
	var relaxed:=Vector3(0,-1,-.08).normalized()
	glove.basis=Basis(Quaternion(current,relaxed))*canonical
	glove.position=Vector3(side*.26,.97,-.04)-glove.basis*wrist
