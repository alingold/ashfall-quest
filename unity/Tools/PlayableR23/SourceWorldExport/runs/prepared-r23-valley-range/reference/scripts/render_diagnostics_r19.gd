extends RefCounted
## Caller-owned lifecycle. No scene scans, optics changes, hardware IDs or uploads.
const CAP:int=128
## Rolling ~128 seconds at 1Hz; older map snapshots can age out in a long session.
var _elapsed:float=0.0
var _poll:float=0.0
var _callback_total:float=0.0
var _callbacks:int=0
var _rows:Array=[]
var output_path:String="user://logs/render-diagnostics-r19.json"

func tick(delta:float,viewport:Viewport,interface:XRInterface,map_slug:String,stage:String,scope_active:bool,gpu_ms:float=-1.0,lod_state:Dictionary={})->void:
	if not is_finite(delta) or delta<=0 or viewport==null:return
	_elapsed+=delta;_poll+=delta;_callback_total+=delta;_callbacks+=1
	if _poll<1.0:return
	_poll=0.0
	# Accept catalog-like slugs only; never arbitrary labels, paths or device names.
	var safe_slug:String=""
	if map_slug.length()<=96 and map_slug==map_slug.validate_filename() and not map_slug.contains("/") and not map_slug.contains("\\"):
		var valid:bool=true
		for c in map_slug:
			if not (c in "abcdefghijklmnopqrstuvwxyz0123456789-_"):valid=false;break
		if valid:safe_slug=map_slug
	var xr:Dictionary={"initialized":interface!=null and interface.is_initialized()}
	if xr.initialized:
		var target:Vector2=interface.get_render_target_size()
		xr["recommended_target_size"]=[target.x,target.y];xr["view_count"]=interface.get_view_count()
		if interface is OpenXRInterface:
			xr["session_state"]=interface.get_session_state();xr["foveation_supported"]=interface.is_foveation_supported()
			if xr.foveation_supported:xr["foveation_level"]=interface.foveation_level;xr["foveation_dynamic"]=interface.foveation_dynamic
	var rid:RID=viewport.get_viewport_rid()
	var row:Dictionary={"elapsed_seconds":_elapsed,"map_slug":safe_slug,"stage":stage if stage in ["startup","menu","loading","map","shutdown"] else "unknown","scope_active":scope_active,"xr":xr,"viewport_size":[viewport.get_visible_rect().size.x,viewport.get_visible_rect().size.y],"use_xr":viewport.use_xr,"scaling_3d_mode":viewport.scaling_3d_mode,"scaling_3d_scale":viewport.scaling_3d_scale,"msaa_3d":viewport.msaa_3d,"mesh_lod_threshold":viewport.mesh_lod_threshold,"vrs_mode":viewport.vrs_mode,"visible_primitives":RenderingServer.viewport_get_render_info(rid,RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE,RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME),"visible_draw_calls":RenderingServer.viewport_get_render_info(rid,RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE,RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME),"callback_mean_ms":1000.0*_callback_total/maxi(1,_callbacks),"callback_count":_callbacks,"viewport_gpu_ms":gpu_ms if is_finite(gpu_ms) and gpu_ms>0 else null}
	row["postcut_lods"]={"accepted":maxi(0,int(lod_state.get("meshes",0))),"rejected":maxi(0,int(lod_state.get("rejected",0))),"disabled":bool(lod_state.get("disabled",false)),"status":"unavailable" if lod_state.is_empty() else ("cache_unavailable" if lod_state.has("reason") else "reported")}
	_callback_total=0.0;_callbacks=0
	if _rows.size()>=CAP:_rows.pop_front()
	_rows.append(row)
	write_checkpoint()

func snapshot()->Dictionary:
	return {"schema":1,"cap":CAP,"poll_hz_max":1,"snapshots":_rows.duplicate(true),"limits":"XR interface recommended target is not independently measured swapchain allocation. Viewport counters/timing exclude compositor; callback intervals do not establish headset FPS. No app DRS inference."}

func write_checkpoint()->Error:
	var error:Error=DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_path.get_base_dir()))
	if error!=OK:return error
	var file:=FileAccess.open(output_path,FileAccess.WRITE)
	if file==null:return FileAccess.get_open_error()
	file.store_string(JSON.stringify(snapshot()));file.close();return OK
