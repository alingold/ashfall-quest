extends SceneTree
const Runtime=preload("res://scripts/map_runtime_rules_r14.gd")
var failures:Array=[]
var checks=0
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run():
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);freeze(game)
	var saved:Dictionary=game.field_menu.get_settings().duplicate(true);var calibration:Transform3D=game.player.xr_origin.transform
	game.player.walk_speed_multiplier=2.0;game.player.fling_power=2.2
	var original:Dictionary=game.field_menu.get_settings().duplicate(true)
	Runtime.apply(game,"archive:alfa-aglae-souza")
	check(game.player.walk_speed_multiplier==1.5,"READY applies authored Alpha ground speed")
	check(game.field_menu.player_damage_multiplier==2.65,"READY applies authored damage")
	check(game.field_builder.charges==10,"READY applies authored starting builds")
	check(game.player.fling_power==2.2,"absent source fling preserves personal preference")
	game._save_settings(game.field_menu.get_settings());var disk:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("user://settings.json"))
	check(disk.walk_speed_multiplier==original.walk_speed_multiplier and disk.player_damage_multiplier==original.player_damage_multiplier,"map defaults never leak into saved movement/damage")
	check(game.player.xr_origin.transform.is_equal_approx(calibration),"temporary defaults retain calibration")
	Runtime.restore(game)
	check(game.player.walk_speed_multiplier==2.0 and game.field_menu.player_damage_multiplier==original.player_damage_multiplier,"leaving restores original personal values")
	Runtime.apply(game,"archive:alfa-aglae-souza");game.player.walk_speed_multiplier=2.5;Runtime.restore(game)
	check(game.player.walk_speed_multiplier==2.5,"deliberate personal override survives map exit")
	Runtime.apply(game,"archive:alfa-aglae-souza")
	var net:Node=game.network_bridge.net
	check(net.host("Rules",25423,"private")==OK,"actual host starts")
	check(not game.has_meta(Runtime.KEY) and game.network_bridge._local_rules.walk_speed_multiplier==2.5,"hosting restores temporary map rules before capturing preferences")
	net.request("rules",{"map":"archive:alfa-aglae-souza"})
	check(net.rules.walk==1.5 and net.rules.damage==2.65 and net.rules.starting_builds==10 and net.rules.infinite_ammo,"host automatically receives same source defaults")
	check(net.rules.mode=="revive" and net.rules.score_limit==0,"source elimination is not treated as one-kill deathmatch")
	net.request("rules",{"walk":.75,"damage":1.1});check(net.rules.walk==.75 and net.rules.damage==1.1,"explicit host overrides remain editable")
	net.request("rules",{"time_limit":300});check(net.rules.walk==.75,"unrelated host setting preserves prior override")
	net.close(false);game._quitting=true;game.queue_free();await process_frame
	var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RUNTIME-RULES-R14.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"scope":"actual Main settings save and ENet host rules boundary, temporary READY values; private offline profile, XR off"},"\t"));print("RUNTIME_RULES_R14 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
