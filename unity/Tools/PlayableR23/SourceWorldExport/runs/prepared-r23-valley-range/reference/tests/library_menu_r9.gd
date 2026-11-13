extends SceneTree
const Maps=preload("res://scripts/map_catalog1000.gd")
const Archive=preload("res://scripts/archive_catalog1003.gd")
const Rarity=preload("res://scripts/weapon_rarity_r6.gd")
var checks=0
var failures:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func overlays(node:Node,material:Material)->int:
	var count=1 if node is GeometryInstance3D and node.material_overlay==material else 0
	for child in node.get_children():count+=overlays(child,material)
	return count
func run():
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game)
	for i in 4:await physics_frame
	var front:Node=game.front_menu;var ids:Array=Maps.ids()
	check(ids.size()==108,"All105 archive maps plus three built-in maps are available")
	for index in ids.size():
		front.page="maps";front._map_page1003=index/4;front.refresh_page()
		var slot:int=index%4;var button:int=front._map_slots1003[slot];var id:String=ids[index]
		check(front.buttons[button].visible and not front.buttons[button].disabled,"Enabled visible selector: "+id)
		check(front.buttons[button].text.begins_with(str(Maps.info(id).title)),"Correct title: "+id)
		front.activate(button);check(front.selected_map==id and front.page=="lobby","Actual selector click chooses map: "+id)
		if id.begins_with("archive:"):check(Archive.verify(id),"Original scene/dependencies fingerprint valid: "+id)
	check(front._map_page1003==26,"Final library page reachable")
	front.page="maps";front._map_page1003=0;front.refresh_page();check(front.buttons[front._map_nav1003[0]].disabled,"First-page previous disabled")
	var rack:Node=game.weapon
	for cat in rack.ACTIVE_WEAPON_INDICES:
		for stars in [1,3,5]:
			var index:int=rack.create_instance(cat,stars);var gun:Node=rack.weapons[index];var material:Material=Rarity.glow(stars)
			check(overlays(gun,material)==0,"Held weapon has no rarity glow: %d/%d"%[cat,stars])
			var prop:Node=game.weapon_inventory._create_prop(index)
			if Rarity.tiered(cat):
				check(overlays(prop,material)>0,"Ground firearm retains rarity glow: %d/%d"%[cat,stars])
				check(prop.get_node_or_null("RarityStarsR6")!=null,"Ground firearm stars retained")
				check(Rarity.level(gun)==stars,"Firearm level preserved without held glow")
			else:
				check(overlays(prop,material)==0,"Fixed-form item has no rarity glow")
				check(prop.get_node_or_null("RarityStarsR6")==null,"Fixed-form item has no stars")
				check(Rarity.level(gun)==1,"Fixed-form item stays at base form")
			prop.queue_free();gun.queue_free();rack.weapons.pop_back();rack.weapon_names.pop_back();rack.weapon_ids.pop_back();rack.instance_catalog.erase(index);await process_frame
	var receipt={"passed":failures.is_empty(),"checks":checks,"failures":failures,"maps":ids.size(),"archive_maps":Archive.rows().size(),"mounted_pack":Archive.mounted_pack_r7,"headset_tested":false}
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/LIBRARY-MENU-R9.json",FileAccess.WRITE);file.store_string(JSON.stringify(receipt,"\t"));file.close()
	print("LIBRARY_MENU_R9 checks=",checks," failures=",failures.size());game.queue_free();await process_frame;quit(0 if failures.is_empty() else 1)
