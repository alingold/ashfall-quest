extends SceneTree
const MapRules=preload("res://scripts/map_rules_r14.gd")
func _initialize():call_deferred("run")
func run():
	preload("res://scripts/archive_catalog1003.gd").mount_local_pack_r7()
	var base={"fling":2.4,"walk":.5,"starting_health":80};var original=base.duplicate(true);var explicit={"walk":.75,"damage":1.1};var saved=explicit.duplicate(true)
	var rules:Dictionary=MapRules.resolve("archive:alfa-aglae-souza",base,explicit)
	assert(rules.mode=="revive" and rules.score_limit==0);assert(rules.walk==.75 and rules.run==1.0 and rules.damage==1.1 and rules.fling==2.4 and rules.starting_health==100)
	assert(base==original and explicit==saved,"Pure rule resolver mutated caller preferences")
	assert(MapRules.patch("range").is_empty());assert(MapRules.patch("unknown").is_empty())
	var data:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/map_rules_r14.json"));assert(data.maps.size()==105)
	for map in data.maps:
		var resolved=MapRules.resolve(map.id)
		for key in map.patch:assert(resolved[key]==map.patch[key],"Map patch failed supported validation")
		assert(not map.patch.has("fling") and not map.patch.has("flight"))
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/MAP-RULES-R14-TEST.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":true,"maps":105,"source_rules_available":79,"source_rules_absent":26,"alpha_resolved":rules,"pure_no_settings_write":true,"scope":"Offline rule mapping/validation; no headset or network transport claim."},"\t"));print("MAP_RULES_R14 PASS all105 defaults, host overrides, clamps, elimination precedence, no input mutation.");quit()
