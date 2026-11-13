extends SceneTree
## R18 distant-view shimmer attribution on real Alpha views (native Vulkan Mobile).
## Same sub-pixel head drift as foliage_stability_r18; whole central crop. Each
## variant changes exactly one global factor so the dominant mechanism is visible:
## depth precision (near plane), MSAA, texture mip bias, mesh LOD threshold.
## Diagnostic only: variants are not shipped settings. PC numbers are relative.
var game:Node
const W=640
const H=400
func _initialize():call_deferred("run")
func grab()->PackedFloat32Array:
	await process_frame;await RenderingServer.frame_post_draw
	var image:Image=root.get_texture().get_image()
	var region:Image=image.get_region(Rect2i(image.get_width()/2-W/2,image.get_height()/2-H/2-60,W,H));region.convert(Image.FORMAT_L8)
	var bytes:PackedByteArray=region.get_data();var result:=PackedFloat32Array();result.resize(W*H)
	for i in W*H:result[i]=bytes[i]/255.0
	return result
## Mean absolute luminance gradient: detail retained in a still frame.
func sharpness(image:Image,rect:Rect2i)->float:
	var region:Image=image.get_region(rect);region.convert(Image.FORMAT_L8);var d:PackedByteArray=region.get_data();var w:int=rect.size.x;var total=0.0
	for y in rect.size.y-1:
		for x in w-1:
			var i:int=y*w+x;total+=absf(d[i+1]-d[i])+absf(d[i+w]-d[i])
	return snappedf(total/(255.0*(w-1)*(rect.size.y-1)),.000001)
func measure(camera:Camera3D,eye:Vector3,target:Vector3)->Dictionary:
	var side:Vector3=(target-eye).cross(Vector3.UP).normalized();var previous:PackedFloat32Array;var diff=0.0;var hot=0;var steps=0
	for f in 16:
		camera.global_position=eye+side*.0015*f;camera.look_at(target+side*.0005*f,Vector3.UP)
		var frame:PackedFloat32Array=await grab()
		if f>0:
			for i in W*H:
				var d:float=absf(frame[i]-previous[i]);diff+=d
				if d>.08:hot+=1
			steps+=1
		previous=frame
	return {"mean_abs_luma":snappedf(diff/(steps*W*H),.000001),"hot_fraction":snappedf(float(hot)/(steps*W*H),.000001),"draws":Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),"primitives":Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)}
func run():
	var out:String=OS.get_cmdline_user_args()[0]
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	if game.get("mission_label") is CanvasItem:game.mission_label.visible=false
	var o:Vector3=map.global_position;var start:Vector3=game.player.global_position+Vector3.UP*1.7
	var views:Array=[
		{"name":"hill-to-city-260m","eye":o+Vector3(-778.95,121.07,-307.49),"target":o+Vector3(-560,60,-60)},
		{"name":"start-yaw45","eye":start,"target":start+Vector3(-sin(deg_to_rad(45)),0,-cos(deg_to_rad(45)))*100},
		{"name":"start-yaw90-roofs","eye":start,"target":start+Vector3(-100,-4,0)}]
	var camera:Camera3D=game.player.desktop_camera;var near0:float=camera.near;var msaa0:int=root.msaa_3d;var bias0:float=root.texture_mipmap_bias;var lod0:float=root.mesh_lod_threshold
	var variants:Array=["shipped","near_0.2","msaa_off","bias_0","bias_0.5","lod_1px"]
	if OS.get_cmdline_user_args().has("--r18-bias-sweep"):variants=["bias_0","bias_0.2","bias_0.3","bias_0.4","bias_0.5"];views=views.slice(1)
	var rows:Array=[]
	for v in views:
		var row:Dictionary={"view":v.name}
		for name in variants:
			camera.near=near0;root.msaa_3d=msaa0;root.texture_mipmap_bias=bias0;root.mesh_lod_threshold=lod0
			match name:
				"near_0.2":camera.near=.2
				"msaa_off":root.msaa_3d=Viewport.MSAA_DISABLED
				"bias_0":root.texture_mipmap_bias=0.0
				"bias_0.2":root.texture_mipmap_bias=.2
				"bias_0.3":root.texture_mipmap_bias=.3
				"bias_0.4":root.texture_mipmap_bias=.4
				"bias_0.5":root.texture_mipmap_bias=.5
				"lod_1px":root.mesh_lod_threshold=1.0
			camera.global_position=v.eye;camera.look_at(v.target,Vector3.UP);for i in 4:await grab()
			row[name]=await measure(camera,v.eye,v.target)
			var still:Image=root.get_texture().get_image()
			row[name]["sharpness_scene"]=sharpness(still,Rect2i(still.get_width()/2-W/2,still.get_height()/2-H/2-60,W,H))
			row[name]["sharpness_weapon"]=sharpness(still,Rect2i(still.get_width()-560,still.get_height()-420,520,400))
			if name=="shipped":root.get_texture().get_image().save_png(out+"/R18-DISTANCE-"+v.name+".png")
		rows.append(row);print("R18_DISTANCE ",JSON.stringify(row))
	camera.near=near0;root.msaa_3d=msaa0;root.texture_mipmap_bias=bias0;root.mesh_lod_threshold=lod0
	var file=FileAccess.open(out+"/R18-DISTANCE-STABILITY.json",FileAccess.WRITE);file.store_string(JSON.stringify({"near_shipped":near0,"msaa_enum":msaa0,"bias_shipped":bias0,"lod_shipped":lod0,"views":rows,"scope":"Native PC diagnostic attribution; relative only."},"\t"));file.close()
	game._quitting=true;game.queue_free();await process_frame;quit(0)
