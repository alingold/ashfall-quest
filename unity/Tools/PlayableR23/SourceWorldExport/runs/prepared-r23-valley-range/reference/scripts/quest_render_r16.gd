extends RefCounted
## Keep the native XR target size. Reduce geometry/texture work, not center resolution.
static func apply(game:Node)->void:
	if OS.get_name()!="Android" and not OS.get_cmdline_user_args().has("--quest-render-r16") and not OS.get_cmdline_user_args().has("--quest-render-r17"):return
	game.get_viewport().mesh_lod_threshold=6.0
	# R18: no global mip bias. It is per-viewport, so it also softens the held weapon
	# (measured: +0.2 bias = -9% distant shimmer but -4..5% weapon detail). Leaf
	# shimmer is fixed per material in archive_materials1004.cutout_r18 instead.
	game.get_viewport().texture_mipmap_bias=0.0
	var interface:OpenXRInterface=game._xr_interface
	if game.xr_enabled and interface!=null and interface.is_initialized() and interface.is_foveation_supported():
		game.get_viewport().vrs_mode=Viewport.VRS_XR
		interface.foveation_level=1
		interface.foveation_dynamic=false
	print("QUEST_RENDER_R17 native_resolution=true mesh_lod_pixels=6 fixed_low_foveation=",game.get_viewport().vrs_mode==Viewport.VRS_XR)
