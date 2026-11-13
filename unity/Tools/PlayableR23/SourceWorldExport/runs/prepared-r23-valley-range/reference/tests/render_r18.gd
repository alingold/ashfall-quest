extends SceneTree
## R18 render regression (headless OK, ~1-2 min).
## 1. On real prepared maps with cutout foliage, the material pass must give every
##    cutout an alpha-to-coverage edge EXACTLY at its authored cut (Godot adds the
##    edge on top of the scissor value), a near-empty hard discard, and never more
##    than the authored normal strength.
## 2. That pass changes materials only: mesh faces, AABBs and collision identities
##    are byte-identical before/after.
## 3. Quest render settings: native resolution path, 6px LOD, no global mip bias.
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func geometry(scene:Node)->Dictionary:
	var result:Dictionary={}
	for view in scene.find_children("*","MeshInstance3D",true,false):
		if view.mesh==null:continue
		var hash:=HashingContext.new();hash.start(HashingContext.HASH_SHA256);var bytes:PackedByteArray=view.mesh.get_faces().to_byte_array()
		if not bytes.is_empty():hash.update(bytes)
		result[str(scene.get_path_to(view))]=[hash.finish().hex_encode(),view.mesh.get_aabb(),view.transform]
	for shape in scene.find_children("*","CollisionShape3D",true,false):result[str(scene.get_path_to(shape))]=[shape.shape.get_instance_id(),shape.transform,shape.disabled]
	return result
func run():
	var Materials=preload("res://scripts/archive_materials1004.gd")
	var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog()
	for slug in ["alfa-aglae-souza","classic-bigbox-vr","statmaster-evolving-soulsmight"]:
		var scene:Node=load("res://local_archive_r10/"+slug+".res").geometry.instantiate()
		var source:Dictionary={}
		for view in scene.find_children("*","MeshInstance3D",true,false):
			var m=view.material_override
			if m is BaseMaterial3D and m.transparency in [BaseMaterial3D.TRANSPARENCY_ALPHA,BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR] and m.resource_name!="pb8a19331":
				if m.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR or m.albedo_color.a>.95:source[view]={"cut":m.alpha_scissor_threshold if m.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR else .35,"normal":m.normal_scale}
		var before:Dictionary=geometry(scene)
		var stats:Dictionary=Materials.apply(scene)
		check(stats.failures.is_empty(),slug+" material pass without failures")
		check(geometry(scene)==before,slug+" faces/AABB/transforms/collision byte-identical")
		check(source.size()>0,slug+" has real cutout foliage surfaces")
		var configured=0
		for view in source:
			var m:BaseMaterial3D=view.material_override;var want:Dictionary=source[view]
			check(m.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR and m.alpha_antialiasing_mode==BaseMaterial3D.ALPHA_ANTIALIASING_ALPHA_TO_COVERAGE,slug+" cutout uses alpha-to-coverage")
			check(absf(m.alpha_scissor_threshold+m.alpha_antialiasing_edge-want.cut)<.001,slug+" effective coverage edge equals authored cut (no stacked thinning)")
			check(m.alpha_scissor_threshold<=Materials.CUTOUT_DISCARD_R18+.0001,slug+" hard discard limited to near-empty texels")
			check(m.normal_scale<=want.normal+.0001,slug+" leaf normal strength never amplified")
			check(absf(float(m.get_meta("r18_cutout_source").threshold)-want.cut)<.0001,slug+" authored cut recorded for provenance")
			configured+=1
		check(Materials.apply(scene)==stats,slug+" idempotent material pass")
		facts.append({"map":slug,"cutout_surfaces":configured,"material_pass":stats});scene.free()
	# Quest render path settings on the actual game root.
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);await process_frame;await process_frame
	var mobile:bool=OS.get_cmdline_user_args().has("--quest-render-r17")
	if mobile:
		check(root.mesh_lod_threshold==6.0,"root viewport 6px mesh LOD")
		check(root.texture_mipmap_bias==0.0,"no global mip bias (weapon detail preserved)")
		check(root.scaling_3d_scale==1.0,"native 3D resolution")
	facts.append({"mobile_path":mobile,"msaa_3d":root.msaa_3d,"lod":root.mesh_lod_threshold,"bias":root.texture_mipmap_bias})
	game._quitting=true;game.queue_free();await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RENDER-R18.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"Cutout material configuration on3 real cutout maps, materials-only proof, Quest root settings. Visual stability is measured separately by foliage_stability_r18/distance_stability_r18 (native)."},"\t"));file.close()
	print("RENDER_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
