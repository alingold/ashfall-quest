extends SceneTree
const Art=preload("res://scripts/map_art_repair_r14.gd")
const Base=preload("res://scripts/archive_semantics_r8.gd")
const Helper=preload("res://scripts/archive_semantics_r9.gd")
func _initialize():call_deferred("run")
func run():
	preload("res://scripts/archive_catalog1003.gd").mount_local_pack_r7()
	var scene:Node=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate();var examples:Dictionary={}
	for view in scene.find_children("*","MeshInstance3D",true,false):
		if view.material_override==null or view.material_override.resource_name!="vc" or view.mesh.get_surface_count()==0:continue
		var groups:Dictionary=Base._components(view.mesh);var arrays:Array=view.mesh.surface_get_arrays(0)
		for id in groups.rows:
			var part:Dictionary=groups.rows[id]
			if part.triangles not in [2,28]:continue
			var extracted:Dictionary=Helper._extract(arrays,groups,id,view.material_override);var colors:PackedColorArray=extracted.mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR]
			if colors.is_empty():continue
			if not examples.has("container") and not Art._panel(extracted.faces).is_empty():examples.container=extracted
			if not examples.has("wood") and part.triangles==28 and not Art._wood(extracted.faces,colors[0]).is_empty():examples.wood=extracted
	var row=0
	for kind in ["container","wood"]:
		assert(examples.has(kind));var source:Dictionary=examples[kind];var box:AABB=source.mesh.get_aabb()
		var horizontal=Vector3.ZERO
		for a in source.faces:
			for b in source.faces:
				var edge:Vector3=b-a
				if absf(edge.y)<.03 and edge.length()>horizontal.length():horizontal=edge
		var angle=atan2(horizontal.z,horizontal.x)
		for col in 2:
			var holder:=Node3D.new();root.add_child(holder);var copy:=MeshInstance3D.new();holder.add_child(copy);copy.mesh=source.mesh;copy.material_override=source.material
			if col==1:print("DETAIL_CAPTURE_R14 ",kind," ",Art.prepare(holder))
			var factor=3.1/maxf(box.size.y,horizontal.length());var rotation=Basis(Vector3.UP,angle+.17)
			holder.basis=rotation.scaled(Vector3.ONE*factor);holder.position=Vector3(col*5.5,(1-row)*4.5,0)-rotation*box.get_center()*factor
			var label:=Label3D.new();root.add_child(label);label.text=("SUPPLIED FLAT COLOR" if col==0 else "R14 ORIGINAL DETAIL")+"\n"+("CONTAINER END" if kind=="container" else "WOOD PANEL");label.font_size=42;label.pixel_size=.006;label.position=Vector3(col*5.5,(1-row)*4.5+2.15,.1);label.billboard=BaseMaterial3D.BILLBOARD_ENABLED
		row+=1
	var env:=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR;env.environment.background_color=Color("202c36");env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;env.environment.ambient_light_color=Color.WHITE;env.environment.ambient_light_energy=.65;root.add_child(env)
	var sun:=DirectionalLight3D.new();root.add_child(sun);sun.rotation_degrees=Vector3(-30,-30,0);var camera:=Camera3D.new();root.add_child(camera);camera.position=Vector3(2.75,2.8,13);camera.look_at(Vector3(2.75,2.8,0));camera.current=true;camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=12
	for i in 8:await process_frame
	await RenderingServer.frame_post_draw;root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/MATERIAL-DETAIL-R14.png");scene.free();quit()
