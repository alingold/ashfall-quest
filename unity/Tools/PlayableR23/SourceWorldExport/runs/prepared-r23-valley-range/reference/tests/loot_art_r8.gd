extends SceneTree
func _initialize():call_deferred("run")
func freeze(node:Node):
	node.set_process(false);node.set_physics_process(false)
	for child in node.get_children():freeze(child)
func run():
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game);freeze(game);game.front_menu.selected_map="archive:battlefield-paintball-gods-pitdevil";game._start_from_title()
	for i in 5:await physics_frame;await process_frame
	freeze(game);var play:Node=game.active_sandbox_map.gameplay_r8
	for index in play.cache_ids.values()[0]:
		var prop:Node3D=game.weapon_inventory.pickups[index]
		if prop.get_child_count()==0:game.weapon_inventory._populate_art_r8(prop,index)
		var cat:int=game.weapon.catalog_index(index);print("LOOT_ART_R8 index=",index," catalog=",cat," name=",game.weapon.weapon_names[index]," bounds=",game.weapon_inventory._world_mesh_bounds(prop)," scale=",game.weapon.weapons[cat].model.scale)
	game.queue_free();await process_frame;quit()
