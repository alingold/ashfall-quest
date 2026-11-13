extends SceneTree
## R18 panel-batch behaviour + cost on the real Alpha map (headless OK).
## - every batched tile draws exactly the triangles of its unbroken source panels
## - breaking a panel removes exactly its triangles and its collision; reset restores
## - rebuild time per tile (break/reset hitch on the main thread) is reported
var game:Node
var checks=0
var failures:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func tile_triangles(bucket:Dictionary)->int:
	if bucket.view.mesh==null or bucket.view.mesh.get_surface_count()==0:return 0
	return bucket.view.mesh.surface_get_array_index_len(0)/3
func expected(bucket:Dictionary)->int:
	var total=0
	for i in bucket.parts.size():
		if not bucket.parts[i].panel._broken:total+=bucket.ranges[i].y/3
	return total
func run():
	var out:String=OS.get_cmdline_user_args()[0]
	game=load("res://scripts/main.gd").new();root.add_child(game);game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
	while not is_instance_valid(game.active_sandbox_map) or not game.active_sandbox_map.map_ready:await process_frame
	var map:Node=game.active_sandbox_map;game.player.set_physics_process(false)
	var batcher:Node=map.get_node_or_null("PanelBatchesR17")
	check(batcher!=null,"panel batcher present")
	var tiles:Array=[];var times:Array=[]
	for key in batcher.buckets:
		var bucket:Dictionary=batcher.buckets[key]
		if bucket.view==null:continue
		tiles.append(key);check(tile_triangles(bucket)==expected(bucket),"tile draws exactly its live source triangles "+key)
		var t0:=Time.get_ticks_usec();batcher._rebuild(key);times.append(Time.get_ticks_usec()-t0)
		check(tile_triangles(bucket)==expected(bucket),"rebuild idempotent "+key)
	times.sort()
	# Break the largest tile's first panel, then restore: triangles and collision follow.
	var biggest:String=tiles[0]
	for key in tiles:if batcher.buckets[key].arrays[Mesh.ARRAY_VERTEX].size()>batcher.buckets[biggest].arrays[Mesh.ARRAY_VERTEX].size():biggest=key
	var bucket:Dictionary=batcher.buckets[biggest];var panel:Node=bucket.parts[0].panel;var before:int=tile_triangles(bucket)
	panel._apply(true);await process_frame;await process_frame
	var owned=0
	for i in bucket.parts.size():if bucket.parts[i].panel==panel:owned+=bucket.ranges[i].y/3
	check(owned>0 and tile_triangles(bucket)==before-owned,"break removes exactly the panel's triangles (all its parts)")
	check(panel.collision_layer==0,"broken panel collision removed")
	var t1:=Time.get_ticks_usec();batcher._rebuild(biggest);var big_us:int=Time.get_ticks_usec()-t1
	panel._apply(false);await process_frame;await process_frame
	check(tile_triangles(bucket)==before and panel.collision_layer==1,"reset restores triangles and collision")
	var stats:Dictionary=map.statistics.get("panel_batches_r17",{})
	var report={"passed":failures.is_empty(),"checks":checks,"failures":failures,"tiles":tiles.size(),"statistics":stats,"rebuild_us":{"median":times[times.size()/2],"p95":times[int(times.size()*.95)],"max":times[-1],"sum":times.reduce(func(a,b):return a+b,0)},"largest_tile":{"key":biggest,"vertices":bucket.arrays[Mesh.ARRAY_VERTEX].size(),"rebuild_us":big_us},"scope":"PC CPU timing (Quest CPU is several times slower); exact triangle/collision behaviour."}
	var file=FileAccess.open(out+"/R18-PANEL-BATCHES"+("-"+OS.get_cmdline_user_args()[-1].trim_prefix("--r18-tag=") if OS.get_cmdline_user_args()[-1].begins_with("--r18-tag=") else "")+".json",FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close()
	print("R18_PANELS ",JSON.stringify(report.rebuild_us)," largest=",report.largest_tile," stats=",stats," checks=",checks," failures=",failures.size())
	game._quitting=true;game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
