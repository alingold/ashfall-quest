extends SceneTree
func _initialize():
	var probe=preload("res://scripts/render_diagnostics_r19.gd").new()
	probe.output_path=OS.get_cmdline_user_args()[0]+"/diagnostics-test.json"
	for i in 1310:probe.tick(.1,root,null,"alfa-aglae-souza","map",false)
	var report:Dictionary=probe.snapshot();assert(report.snapshots.size()<=128 and report.snapshots.size()>0)
	assert(report.snapshots[-1].xr.initialized==false);assert(report.snapshots[-1].viewport_gpu_ms==null)
	probe.tick(1,root,null,"builtin_map_01","map",false)
	assert(probe.snapshot().snapshots[-1].map_slug=="builtin_map_01")
	probe.tick(1,root,null,"private/path@example.com","private-stage",true,-1.0,{"meshes":2,"rejected":3})
	report=probe.snapshot();assert(report.snapshots[-1].map_slug=="");assert(report.snapshots[-1].stage=="unknown");assert(report.snapshots[-1].scope_active);assert(report.snapshots[-1].postcut_lods.accepted==2 and report.snapshots[-1].postcut_lods.rejected==3)
	assert(FileAccess.get_file_as_string(probe.output_path).length()<262144)
	print("RENDER_DIAGNOSTICS_TEST bounded=true null_xr=true invalid_labels_filtered=true bytes=",FileAccess.get_file_as_string(probe.output_path).length());quit()
