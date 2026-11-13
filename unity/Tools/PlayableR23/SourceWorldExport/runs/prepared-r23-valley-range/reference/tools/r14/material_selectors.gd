extends SceneTree
const Art=preload("res://scripts/map_art_repair_r14.gd")
func _initialize():call_deferred("run")
func run():
	var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.mount_local_pack_r7()
	var path="res://assets/data/map_art_audit_r14.json";var data:Dictionary=JSON.parse_string(FileAccess.get_file_as_string(path));var summary:Array=[]
	for row in data.maps:
		var scene:Node=load("res://local_archive_r10/"+row.slug+".res").geometry.instantiate();var selected:Array=[];var begin=Time.get_ticks_msec();var stats:Dictionary=Art.prepare(scene,row.id,selected,true);row.repair_selectors=selected;row.repair_runtime_counts=stats;row.repair_selector_source="R10 prepared geometry. Paths/counts guarded; apply before any later geometry extraction.";summary.append({"id":row.id,"stats":stats,"prepare_ms":Time.get_ticks_msec()-begin});print("ART_SELECTORS_R14 ",row.slug," ",stats," ms=",Time.get_ticks_msec()-begin);scene.free()
		await process_frame
	data.audit_summary.prepared_repair_maps=0;data.audit_summary.prepared_container_end_panels=0;data.audit_summary.prepared_wood_panels=0;data.audit_summary.prepared_draw_chunks=0
	for row in data.maps:
		if not row.repair_selectors.is_empty():data.audit_summary.prepared_repair_maps+=1
		data.audit_summary.prepared_container_end_panels+=row.repair_runtime_counts.container_end_panels;data.audit_summary.prepared_wood_panels+=row.repair_runtime_counts.wood_panels;data.audit_summary.prepared_draw_chunks+=row.repair_runtime_counts.draw_chunks
	var f=FileAccess.open(path,FileAccess.WRITE);f.store_string(JSON.stringify(data,"\t"));f.close()
	f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/ART-SELECTOR-GENERATION-R14.json",FileAccess.WRITE);f.store_string(JSON.stringify({"maps":summary},"\t"));f.close();Art._materials.clear()
	for i in 4:await process_frame
	quit()
