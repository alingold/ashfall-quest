extends SceneTree
const Catalog=preload("res://scripts/archive_catalog1003.gd")
const Materials=preload("res://scripts/archive_materials1004.gd")
func _initialize()->void:call_deferred("run")
func run()->void:
	var args=OS.get_cmdline_user_args();var id="archive:melis-cafe-gods-pitdevil";var path=Catalog.scene(id);var before=FileAccess.get_sha256(path);var baseline=load(path).instantiate();root.add_child(baseline)
	var first=Materials.apply(baseline);baseline.queue_free();await process_frame
	var again=load(path).instantiate();root.add_child(again);var second=Materials.apply(again)
	var valid=first.failures.is_empty() and second.failures.is_empty() and second.cache_hits==second.textures and second.textures>0 and before==FileAccess.get_sha256(path) and Catalog.verify(id)
	var result={"passed":valid,"first":first,"second":second,"original_scene_unchanged":before==FileAccess.get_sha256(path),"original_dependencies_verify":Catalog.verify(id),"scope":"actual production adapter, local-only derived cache miss/hit and unchanged original scene/dependencies; headless, no rendered-performance claim"};var f=FileAccess.open(args[0],FileAccess.WRITE);f.store_string(JSON.stringify(result,"\t"));f.close();print("MATERIAL_CACHE1004 "+JSON.stringify(result));quit(0 if valid else 1)
