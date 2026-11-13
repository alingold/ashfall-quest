extends RefCounted
## Per-avatar overrides; never mutate shared skin materials or meshes.
const SHADER=preload("res://assets/ghost_r13.gdshader")
var active=false
var _records:Array=[]
var _materials:Dictionary={}
func set_enabled(roots:Array,value:bool):
	if value==active:return
	if not value:restore();return
	active=true
	for root in roots:
		if not is_instance_valid(root):continue
		var meshes:Array=root.find_children("*","MeshInstance3D",true,false)
		if root is MeshInstance3D:meshes.append(root)
		for view in meshes:
			if view.mesh==null:continue
			var surfaces:Array=[]
			for i in view.mesh.get_surface_count():surfaces.append(view.get_surface_override_material(i))
			_records.append({"view":weakref(view),"override":view.material_override,"overlay":view.material_overlay,"surfaces":surfaces,"shadow":view.cast_shadow})
			var override:Material=view.material_override;view.material_override=null;view.material_overlay=null;view.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			for i in view.mesh.get_surface_count():
				var source:Material=override if override!=null else (surfaces[i] if surfaces[i]!=null else view.mesh.surface_get_material(i))
				view.set_surface_override_material(i,_ghost_material(source))
func _ghost_material(source:Material)->ShaderMaterial:
	var key:int=source.get_instance_id() if source!=null else 0
	if _materials.has(key):return _materials[key]
	var mat:=ShaderMaterial.new();mat.shader=SHADER;mat.render_priority=12
	if source is BaseMaterial3D:
		mat.set_shader_parameter("source_tint",source.albedo_color)
		mat.set_shader_parameter("has_albedo",source.albedo_texture!=null)
		if source.albedo_texture!=null:mat.set_shader_parameter("source_albedo",source.albedo_texture)
		mat.set_shader_parameter("source_uv_scale",source.uv1_scale);mat.set_shader_parameter("source_uv_offset",source.uv1_offset);mat.set_shader_parameter("vertex_albedo",source.vertex_color_use_as_albedo)
	_materials[key]=mat;return mat
func restore():
	for record in _records:
		var view:MeshInstance3D=record.view.get_ref()
		if not is_instance_valid(view):continue
		view.material_override=record.override;view.material_overlay=record.overlay;view.cast_shadow=record.shadow
		if view.mesh!=null:
			for i in mini(view.mesh.get_surface_count(),record.surfaces.size()):view.set_surface_override_material(i,record.surfaces[i])
	_records.clear();_materials.clear();active=false
