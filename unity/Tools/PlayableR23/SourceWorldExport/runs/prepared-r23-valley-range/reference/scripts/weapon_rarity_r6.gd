extends RefCounted
## Every weapon instance owns its level. Shared data drives both local weapons and server damage.
const COLORS=[Color.WHITE,Color("4b9bff"),Color("bd69ff"),Color("ffc542"),Color("ff415c")]
static var _data:Dictionary={}
static var _glows:Array[ShaderMaterial]=[]
static func data()->Dictionary:
	if _data.is_empty():_data=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/weapon_rarity_r6.json")).weapons
	return _data
static func valid(value:Variant)->bool:return (value is int or value is float) and is_finite(float(value)) and float(value)==floorf(float(value)) and int(value)>=1 and int(value)<=5
static func tiered(catalog:int)->bool:return catalog not in [14,15,16,26,28,29]
static func level(gun:Node)->int:
	return clampi(int(gun.get_meta("rarity0991",1)),1,5) if tiered(int(gun.get_meta("rarity_catalog_r6",0))) else 1
static func color(stars:int)->Color:return COLORS[clampi(stars,1,5)-1]
static func stars_text(stars:int)->String:return "★".repeat(clampi(stars,1,5))
static func row(catalog:int,stars:int)->Dictionary:
	var profile:Dictionary=preload("res://scripts/arsenal_data.gd").profile(catalog)
	var entry:Dictionary=data().get(str(profile.get("id","")),{})
	if entry.is_empty():return {"body":0.0,"head":0.0,"pellets":1,"magazine":int(profile.get("magazine_per_hand",0)),"recoil":1.0,"spread":1.0}
	var r:int=clampi(stars,1,5)-1
	return {"body":float(entry.body[r]),"head":float(entry.head[r]),"pellets":int(entry.pellets),"magazine":int(entry.magazine[r]),"recoil":float(entry.recoil[r]),"spread":float(entry.spread[r])}
static func capacity(catalog:int,stars:int)->int:return int(row(catalog,stars).magazine)*(2 if catalog in [11,12] else 1)
static func apply(gun:Node3D,catalog:int,stars:int,fill:bool=true)->void:
	stars=clampi(stars,1,5) if tiered(catalog) else 1;var stats:Dictionary=row(catalog,stars)
	gun.set_meta("rarity0991",stars);gun.set_meta("rarity_color0991",color(stars));gun.set_meta("rarity_recoil_r6",stats.recoil);gun.set_meta("rarity_spread_r6",stats.spread)
	gun.set_meta("rarity_head_ratio_r6",float(stats.head)/maxf(1,float(stats.body)))
	if float(stats.body)>0 and catalog not in [26,27,28]:gun.damage=float(stats.body)/maxi(1,int(stats.pellets))
	gun.magazine_size=capacity(catalog,stars)
	if "guns" in gun:
		for child in gun.guns:
			child.set_meta("rarity0991",stars);child.set_meta("rarity_recoil_r6",stats.recoil);child.set_meta("rarity_spread_r6",stats.spread);child.damage=gun.damage;child.magazine_size=int(stats.magazine)
			if fill:child.ammo=child.magazine_size
		gun._sync_pair()
	elif fill:
		gun.ammo=gun.magazine_size
		if "seated" in gun:gun.seated=gun.ammo
	gun.set_meta("rarity_catalog_r6",catalog)
	if not gun.get_meta("dormant_r6",false):
		clear_glow(gun.model)
		if "guns" in gun:
			for child in gun.guns:clear_glow(child.model)
	gun.update_ammo_display()
static func glow(stars:int)->ShaderMaterial:
	if _glows.is_empty():
		for c in COLORS:
			var mat:=ShaderMaterial.new();mat.shader=preload("res://assets/cyber_bird/rarity_r6/glow.gdshader");mat.set_shader_parameter("rarity_color",c);_glows.append(mat)
	return _glows[clampi(stars,1,5)-1]
static func decorate(prop:Node3D,stars:int)->void:
	var old:Node=prop.get_node_or_null("RarityStarsR6")
	if old!=null:prop.remove_child(old);old.queue_free()
	# R23: floating star label removed; star count is shown in the weapon selection text.
	_overlay(prop,glow(stars))
static func _overlay(node:Node,material:Material)->void:
	if node is MeshInstance3D or node is MultiMeshInstance3D:node.material_overlay=material
	for child in node.get_children():_overlay(child,material)

static func clear_glow(node:Node)->void:
	if node is MeshInstance3D or node is MultiMeshInstance3D:
		if node.material_overlay in _glows:node.material_overlay=null
	for child in node.get_children():clear_glow(child)
