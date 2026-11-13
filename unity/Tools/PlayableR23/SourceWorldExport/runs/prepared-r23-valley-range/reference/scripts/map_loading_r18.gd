extends Node3D
var game:Node
var map_r20=""
var stage_r20=""
var breadcrumbs_r20:Array=[]
var serial=0
var active=false
var pending=false
var requests_r18:Dictionary={}
var progress:MeshInstance3D
var text:Label3D
var desktop_r19:CanvasLayer
var desktop_back_r19:ColorRect
var desktop_text_r19:Label
static func get_for(owner_game:Node)->Node:
 var node=owner_game.get_node_or_null("MapLoadingR18")
 if node==null:node=load("res://scripts/map_loading_r18.gd").new();owner_game.add_child(node);node.setup(owner_game)
 return node
func setup(g:Node):
 game=g;name="MapLoadingR18"
 # Recovery: ordinary R18 StandardMaterial/physical panel path, no clip-space shader.
 var back=MeshInstance3D.new();back.mesh=QuadMesh.new();back.mesh.size=Vector2(.65,.23);back.layers=1<<19;back.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
 var mat=StandardMaterial3D.new();mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.no_depth_test=true;mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;mat.depth_draw_mode=BaseMaterial3D.DEPTH_DRAW_DISABLED;mat.cull_mode=BaseMaterial3D.CULL_DISABLED;mat.albedo_color=Color("06131d");mat.render_priority=125;back.material_override=mat;add_child(back)
 # R23: opaque backdrop around the head while loading. The first load previously showed
 # the original valley world behind the small panel. Drawn after opaque geometry with
 # depth testing off, below the panel's render priority.
 var shroud=MeshInstance3D.new();shroud.name="LoadingShroudR23";var ball=SphereMesh.new();ball.radius=3.0;ball.height=6.0;ball.radial_segments=16;ball.rings=8;shroud.mesh=ball;shroud.position=Vector3(0,.08,1.0);shroud.layers=1<<19;shroud.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
 var shroud_mat=StandardMaterial3D.new();shroud_mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;shroud_mat.no_depth_test=true;shroud_mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;shroud_mat.depth_draw_mode=BaseMaterial3D.DEPTH_DRAW_DISABLED;shroud_mat.cull_mode=BaseMaterial3D.CULL_DISABLED;shroud_mat.albedo_color=Color(.008,.018,.027,1.0);shroud_mat.render_priority=100;shroud_mat.disable_fog=true;shroud.material_override=shroud_mat;add_child(shroud)
 text=Label3D.new();text.text="LOADING MAP";text.font_size=48;text.pixel_size=.0012;text.position=Vector3(0,0,.005);text.no_depth_test=true;text.render_priority=127;text.outline_render_priority=126;text.layers=1<<19;add_child(text)
 desktop_r19=CanvasLayer.new();desktop_r19.layer=300;add_child(desktop_r19)
 desktop_back_r19=ColorRect.new();desktop_back_r19.color=Color(.008,.018,.027);desktop_r19.add_child(desktop_back_r19)
 desktop_text_r19=Label.new();desktop_text_r19.text="LOADING MAP";desktop_text_r19.add_theme_font_size_override("font_size",32);desktop_text_r19.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;desktop_text_r19.vertical_alignment=VERTICAL_ALIGNMENT_CENTER;desktop_text_r19.size=Vector2(400,80);desktop_r19.add_child(desktop_text_r19)
 desktop_r19.hide();hide()
func begin()->int:
 serial+=1;active=true;pending=false;stage_r20="";game.player.set_meta("map_loading_r18",true);show();desktop_r19.visible=not game.xr_enabled;update_phase("LOADING",.03);_process(0);return serial
func update_phase(label:String,fraction:float):
 text.text="LOADING MAP"
 record_stage_r20(label)
func finish(token:int):
 if token!=serial:return
 record_stage_r20("READY")
 active=false;pending=false;hide();desktop_r19.hide()
 if is_instance_valid(game.player):game.player.remove_meta("map_loading_r18");game.player.restore_calibration_r17();game.player.reset_motion()
func cancel():
 if active:record_stage_r20("CANCELLED")
 serial+=1;active=false;pending=false;hide();desktop_r19.hide()
 if is_instance_valid(game.player) and game.player.has_meta("map_loading_r18"):game.player.remove_meta("map_loading_r18")
func _process(_delta:float):
 if not active:return
 var size:Vector2=get_viewport().get_visible_rect().size;desktop_back_r19.size=size;desktop_text_r19.position=(size-desktop_text_r19.size)*.5
 var camera:Camera3D=game.player.get_view_camera();global_transform=camera.global_transform*Transform3D(Basis.IDENTITY,Vector3(0,-.08,-1.0))

func load_resource_r18(path:String,token:int)->Resource:
 # Always reap the threaded request, including a cancelled selection. Otherwise
 # the loader's completed task can retain the discarded map resource indefinitely.
 var tree:SceneTree=get_tree()
 var request:Dictionary=requests_r18.get(path,{})
 if DisplayServer.get_name()=="headless":
  # Godot4.7.2 Dummy mesh RID storage produced allocator errors when worker
  # resource creation overlapped avatar/UI creation. Servers have no visible
  # load animation: stage one frame, then load on the main thread there only.
  if not request.is_empty():
   while not request.done:await tree.process_frame
   return request.resource
  request={"done":false,"resource":null};requests_r18[path]=request
  await tree.process_frame
  if is_instance_valid(self) and token==serial:request.resource=ResourceLoader.load(path)
  request.done=true
  if is_instance_valid(self):requests_r18.erase(path)
  return request.resource
 if request.is_empty():
  var error:Error=ResourceLoader.load_threaded_request(path,"Resource",false)
  if error not in [OK,ERR_BUSY]:return null
  request={"done":false,"resource":null};requests_r18[path]=request
 while not request.done:
  var progress_value:Array=[]
  var status:int=ResourceLoader.load_threaded_get_status(path,progress_value)
  if status==ResourceLoader.THREAD_LOAD_IN_PROGRESS:
   if is_instance_valid(self) and token==serial:update_phase("LOADING MAP",.12+.45*(float(progress_value[0]) if not progress_value.is_empty() else 0))
   await tree.process_frame
  else:
   request.resource=ResourceLoader.load_threaded_get(path) if status==ResourceLoader.THREAD_LOAD_LOADED else null
   request.done=true
   if is_instance_valid(self):requests_r18.erase(path)
 return request.resource

func record_stage_r20(stage:String,map_id:String=""):
 if not map_id.is_empty():map_r20=map_id
 if stage==stage_r20:return
 stage_r20=stage
 breadcrumbs_r20.append({"serial":serial,"map":map_r20,"stage":stage,"elapsed_ms":Time.get_ticks_msec()})
 if breadcrumbs_r20.size()>32:breadcrumbs_r20.pop_front()
 DirAccess.make_dir_recursive_absolute(OS.get_user_data_dir().path_join("logs"))
 var file=FileAccess.open("user://logs/map-loading-r20.json",FileAccess.WRITE)
 if file!=null:file.store_string(JSON.stringify({"schema":1,"build":ProjectSettings.get_setting("application/config/version"),"rows":breadcrumbs_r20}));file.close()
