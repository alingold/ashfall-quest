extends SceneTree
## R18 rendering audit: classify the live material state of a loaded archive map
## (what the GPU actually draws after every runtime pass) and sample the
## center-of-map view. Read-only for gameplay; writes evidence JSON only.
var game:Node
var out:String
func _initialize():call_deferred("run")
func arg(prefix:String,fallback:String)->String:
	for value in OS.get_cmdline_user_args():
		if value.begins_with(prefix):return value.trim_prefix(prefix)
	return fallback
func material_class(m:Material)->String:
	if m==null:return "none"
	if m is ShaderMaterial:return "shader"
	if not m is BaseMaterial3D:return "other"
	var b:BaseMaterial3D=m
	var aa:String=["","+a2c","+a2c_one"][b.alpha_antialiasing_mode] if b.alpha_antialiasing_mode<3 else "+aa?"
	match b.transparency:
		BaseMaterial3D.TRANSPARENCY_DISABLED:return "opaque"
		BaseMaterial3D.TRANSPARENCY_ALPHA:return "blend"+aa
		BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR:return "scissor"+aa
		BaseMaterial3D.TRANSPARENCY_ALPHA_HASH:return "hash"+aa
		BaseMaterial3D.TRANSPARENCY_ALPHA_DEPTH_PRE_PASS:return "prepass"+aa
	return "other"
func inventory(map:Node)->Dictionary:
	var classes:Dictionary={};var where:Dictionary={"override":0,"surface":0,"surface_override":0};var materials:Dictionary={};var alpha_textures:Dictionary={}
	for node in map.find_children("*","GeometryInstance3D",true,false):
		if not node.is_visible_in_tree() or node.layers==0:continue
		if not node is MeshInstance3D or node.mesh==null:continue
		var view:MeshInstance3D=node
		for s in view.mesh.get_surface_count():
			var m:Material=view.material_override;var origin="override"
			if m==null:m=view.get_surface_override_material(s);origin="surface_override"
			if m==null:m=view.mesh.surface_get_material(s);origin="surface"
			var c:String=material_class(m)
			if not classes.has(c):classes[c]={"surfaces":0,"triangles":0,"materials":{}}
			var arrays_len:int=view.mesh.surface_get_array_index_len(s);if arrays_len<=0:arrays_len=view.mesh.surface_get_array_len(s)
			classes[c].surfaces+=1;classes[c].triangles+=arrays_len/3;where[origin]+=1
			if m!=null:
				classes[c].materials[m.get_instance_id()]=true
				if not materials.has(m.get_instance_id()):
					var row:Dictionary={"class":c,"origin":origin,"name":str(m.resource_name),"node":str(view.name)}
					if m is BaseMaterial3D:
						row.merge({"threshold":m.alpha_scissor_threshold,"edge":m.alpha_antialiasing_edge,"albedo_a":m.albedo_color.a,"cull":m.cull_mode,"tex":str(m.albedo_texture.resource_path if m.albedo_texture!=null else ""),"normal":m.normal_enabled,"normal_scale":m.normal_scale,"spec":m.metallic_specular})
						if m.albedo_texture!=null and c!="opaque":
							var key:String=str(m.albedo_texture.get_instance_id())
							if not alpha_textures.has(key):
								var image:Image=m.albedo_texture.get_image()
								if image!=null:
									if image.is_compressed():image.decompress()
									alpha_textures[key]={"alpha":image.detect_alpha(),"size":[image.get_width(),image.get_height()],"mips":image.has_mipmaps()}
							row["tex_alpha"]=alpha_textures.get(key,{})
					materials[m.get_instance_id()]=row
				materials[m.get_instance_id()]["triangles"]=int(materials[m.get_instance_id()].get("triangles",0))+arrays_len/3
	for c in classes:classes[c].materials=classes[c].materials.size()
	var sorted:Array=materials.values().filter(func(r):return r.class!="opaque")
	sorted.sort_custom(func(a,b):return int(a.triangles)>int(b.triangles))
	return {"classes":classes,"origins":where,"distinct_materials":materials.size(),"non_opaque_materials":sorted.slice(0,60)}
func sample(label:String)->Dictionary:
	var rid:RID=root.get_viewport_rid();RenderingServer.viewport_set_measure_render_time(rid,true)
	for i in 6:await process_frame
	var gpu:Array=[];var cpu:Array=[]
	for i in 24:
		await RenderingServer.frame_post_draw
		gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(rid));cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(rid))
	gpu.sort();cpu.sort()
	var result={"draw_calls":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),"objects":Performance.get_monitor(Performance.RENDER_TOTAL_OBJECTS_IN_FRAME),"pc_gpu_ms_median":gpu[12],"pc_render_cpu_ms_median":cpu[12]}
	if arg("--r18-png=","")=="1" and (label.contains("yaw") or label.contains("nomap")):root.get_texture().get_image().save_png(out+"/R18-AUDIT-"+label+".png")
	return result
## Per stored mip level: how much of the texture survives each cut. Box-filtered
## mips average alpha down, so cutout foliage thins/vanishes with distance.
static func mip_coverage(texture:Texture2D)->Array:
	var image:Image=texture.get_image()
	if image==null:return []
	if image.is_compressed():image.decompress()
	image.convert(Image.FORMAT_RGBA8)
	var rows:Array=[];var data:PackedByteArray=image.get_data()
	for level in image.get_mipmap_count()+1:
		var w:int=maxi(1,image.get_width()>>level);var h:int=maxi(1,image.get_height()>>level)
		var offset:int=image.get_mipmap_offset(level);var a35=0;var a65=0;var a10=0;var total=0.0
		for i in w*h:
			var a:int=data[offset+i*4+3];total+=a
			if a>=26:a10+=1
			if a>=89:a35+=1
			if a>=166:a65+=1
		rows.append({"level":level,"size":w,"mean_alpha":snappedf(total/(255.0*w*h),.0001),"ge_0.10":snappedf(float(a10)/(w*h),.0001),"ge_0.35":snappedf(float(a35)/(w*h),.0001),"ge_0.65":snappedf(float(a65)/(w*h),.0001)})
	return rows
func breakdown(map:Node)->Array:
	var rows:Array=[];var base:Dictionary=await sample("breakdown-base")
	var groups:Array=[]
	for child in map.get_children():if child is Node3D and child.visible:groups.append(child)
	for child in game.get_children():if child is Node3D and child.visible and child!=map:groups.append(child)
	while not groups.is_empty():
		var node:Node3D=groups.pop_front()
		node.visible=false;var row:Dictionary=await sample("x");node.visible=true
		var delta:int=int(base.draw_calls-row.draw_calls)
		if delta==0:continue
		var kids:Array=node.get_children().filter(func(c):return c is Node3D and c.visible)
		# Drill into large non-map owners so the report names real sources.
		if delta>=12 and node!=map._geometry and node.name!="PanelBatchesR17" and kids.size()>0 and kids.size()<=60:groups.append_array(kids);continue
		rows.append({"node":str(node.name),"class":node.get_class(),"script":str(node.get_script().resource_path) if node.get_script()!=null else "","path":str(game.get_path_to(node)),"draws":delta,"primitives":int(base.primitives-row.primitives)})
	rows.sort_custom(func(a,b):return a.draws>b.draws)
	return rows
func run():
	out=OS.get_cmdline_user_args()[0]
	var slug:=arg("--r18-map=","alfa-aglae-souza")
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:"+slug;game._start_from_title()
	var deadline=Time.get_ticks_msec()+180000
	while (not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready) and Time.get_ticks_msec()<deadline:await process_frame
	var map:Node=game.active_sandbox_map
	for i in 10:await process_frame
	game.player.set_physics_process(false)
	var report:Dictionary={"map":slug,"msaa_3d":root.msaa_3d,"mipmap_bias":root.texture_mipmap_bias,"mesh_lod_threshold":root.mesh_lod_threshold,"statistics":{}}
	for key in ["mobile_textures_r17","material_mips1004","mesh_lods_r16","panel_batches_r17","camera_far_r9"]:report.statistics[key]=map.statistics.get(key)
	report["inventory"]=inventory(map)
	var camera:Camera3D=game.player.desktop_camera;var meta:Dictionary=map._meta
	var focus:Vector3=map.global_position+Vector3(meta.center[0],meta.center[1],meta.center[2])
	var floor:Vector3=map._floor(focus+Vector3.UP*40,120)
	var eye:Vector3=(floor if floor.is_finite() else focus)+Vector3.UP*1.7
	if arg("--r18-eye=","start")=="start":eye=game.player.global_position+Vector3.UP*1.7
	report["eye"]=str(eye-map.global_position);report["near"]=camera.near;report["far"]=camera.far;report["fov"]=camera.fov
	var views:Array=[]
	if DisplayServer.get_name()!="headless":
		for yaw in range(0,360,45):
			camera.global_position=eye;camera.global_rotation=Vector3(0,deg_to_rad(yaw),0)
			var row:Dictionary=await sample("%s-yaw%03d"%[slug,yaw]);row["yaw"]=yaw;views.append(row)
		# Same view with the whole map hidden: the non-map floor of the frame.
		camera.global_rotation=Vector3(0,0,0);map._geometry.visible=false
		var empty:Dictionary=await sample(slug+"-nomap");map._geometry.visible=true
		report["nomap_view_yaw0"]=empty
		camera.global_rotation=Vector3(0,deg_to_rad(315),0)
		report["breakdown_yaw315"]=await breakdown(map)
	report["center_views"]=views
	var coverage:Dictionary={}
	for row in report.inventory.non_opaque_materials:
		if str(row.get("tex","")).is_empty() or coverage.has(row.tex):continue
		coverage[row.tex]=mip_coverage(load(row.tex))
	report["cutout_mip_coverage"]=coverage
	var file=FileAccess.open(out+"/R18-AUDIT-"+slug+arg("--r18-tag=","")+".json",FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("R18_AUDIT ",slug," classes=",JSON.stringify(report.inventory.classes)," origins=",report.inventory.origins)
	for b in report.get("breakdown_yaw315",[]):print("R18_OWNER ",b)
	for v in views:print("R18_VIEW yaw=",v.yaw," draws=",v.draw_calls," prims=",v.primitives," gpu=",snappedf(v.pc_gpu_ms_median,.01))
	game._quitting=true;game.queue_free();await process_frame;quit(0)
