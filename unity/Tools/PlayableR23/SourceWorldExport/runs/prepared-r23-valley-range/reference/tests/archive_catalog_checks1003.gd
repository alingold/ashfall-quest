extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
var failures:Array=[]
var game:Node
func _initialize()->void:call_deferred("run")
func check(ok:bool,label:String)->void:
	if not ok:failures.append(label);print("FAIL ",label)
func run()->void:
	game=preload("res://scripts/main.gd").new();root.add_child(game)
	for i in 5:await process_frame
	var menu:Node=game.front_menu;var ids:Array=menu._map_ids1003
	var expected=Catalog.rows().size()+3+(1 if preload("res://scripts/community_catalog1002.gd").installed() else 0)
	check(ids.size()==expected,"all indexed archive maps plus preserved maps")
	var unique:Dictionary={}
	for id in ids:unique[id]=true
	check(unique.size()==ids.size(),"unique catalog IDs")
	var pages=ceili(float(ids.size())/4);var seen:Dictionary={}
	for page in pages:
		menu._map_page1003=page;menu.page="maps";menu.refresh_page()
		for slot in 4:
			var index=page*4+slot
			if index>=ids.size():continue
			var button:Button=menu.buttons[menu._map_slots1003[slot]];check(button.visible,"map visible on page");seen[ids[index]]=true
			if not button.disabled:menu.activate(menu._map_slots1003[slot]);check(menu.selected_map==ids[index],"release action selects exact paged map ID");menu.page="maps";menu.refresh_page()
	check(seen.size()==expected,"every map reachable through paging")
	check(game.find_children("ArchiveGeometry","Node3D",true,false).is_empty(),"catalog does not preload archive geometry")
	# Synthetic own-byte cache fixture: no imported assets or public cache files are changed.
	var dir=Catalog.ROOT+"maps/_integrity-fixture/converted/";DirAccess.make_dir_recursive_absolute(dir)
	var file=FileAccess.open(dir+"map.scn",FileAccess.WRITE);file.store_string("synthetic own-byte integrity test");file.close()
	var manifest={"cache_fingerprint":"synthetic-semantic-map-id","dependencies":[],"local_cache_hash":(FileAccess.get_sha256(dir+"map.scn")+"\n").sha256_text()};file=FileAccess.open(dir+"manifest.json",FileAccess.WRITE);file.store_string(JSON.stringify(manifest));file.close()
	var old:Dictionary=Catalog._catalog;Catalog._catalog=old.duplicate(true);Catalog._catalog.maps.append({"id":"archive:_integrity-fixture","slug":"_integrity-fixture","status":"ready"});Catalog._hashes.erase("archive:_integrity-fixture")
	check(Catalog.verify("archive:_integrity-fixture"),"known cache bytes validate")
	file=FileAccess.open(dir+"map.scn",FileAccess.WRITE);file.store_string("one changed synthetic byte");file.close();Catalog._hashes.erase("archive:_integrity-fixture")
	check(not Catalog.verify("archive:_integrity-fixture"),"changed cache bytes rejected before scene loading")
	Catalog._catalog=old;Catalog._hashes.erase("archive:_integrity-fixture")
	var report={"passed":failures.is_empty(),"failures":failures,"entries":ids.size(),"pages":pages,"seen":seen.size(),"scope":"actual paginated menu selection and synthetic own-byte cache-corruption regression; no archive asset modified"};var args=OS.get_cmdline_user_args();file=FileAccess.open(args[0],FileAccess.WRITE);file.store_string(JSON.stringify(report,"\t"));file.close();print("ARCHIVE_CATALOG "+JSON.stringify(report));quit(0 if failures.is_empty() else 1)
