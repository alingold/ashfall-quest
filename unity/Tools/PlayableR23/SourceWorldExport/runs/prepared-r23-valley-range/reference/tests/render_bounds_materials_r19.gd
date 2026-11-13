extends SceneTree
const Cache=preload("res://scripts/postcut_lods_r19.gd")
func _initialize():
	var view:=MeshInstance3D.new();var mesh:=ArrayMesh.new();var arrays:Array=[];arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX]=PackedVector3Array([Vector3.ZERO,Vector3.RIGHT,Vector3.UP]);arrays[Mesh.ARRAY_INDEX]=PackedInt32Array([0,1,2])
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays);view.mesh=mesh
	var material:=StandardMaterial3D.new();view.material_override=material;assert(Cache.safe_bounds_materials(view))
	material.grow=true;assert(not Cache.safe_bounds_materials(view));material.grow=false
	material.fixed_size=true;assert(not Cache.safe_bounds_materials(view));material.fixed_size=false
	material.billboard_mode=BaseMaterial3D.BILLBOARD_ENABLED;assert(not Cache.safe_bounds_materials(view));material.billboard_mode=BaseMaterial3D.BILLBOARD_DISABLED
	material.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR;assert(not Cache.safe_bounds_materials(view));material.transparency=BaseMaterial3D.TRANSPARENCY_DISABLED
	material.next_pass=ShaderMaterial.new();assert(not Cache.safe_bounds_materials(view));material.next_pass=null
	view.material_overlay=ShaderMaterial.new();assert(not Cache.safe_bounds_materials(view));view.material_overlay=null
	view.material_override=ShaderMaterial.new();assert(not Cache.safe_bounds_materials(view))
	view.material_override=material;view.skin=Skin.new();assert(not Cache.safe_bounds_materials(view));view.skin=null
	assert(Cache.render_bounds(mesh)==mesh.get_aabb());mesh.custom_aabb=AABB(Vector3(-1,-1,-1),Vector3(3,3,3));assert(Cache.render_bounds(mesh)==mesh.custom_aabb)
	view.free();print("BOUNDS_MATERIAL_TEST shader_overlay_nextpass_skin_cutout_billboard_grow_fixedsize_rejected=true custom_bounds_getter=true");quit()
