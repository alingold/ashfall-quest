extends SceneTree
var failures:Array=[]
var checks=0
var facts:Dictionary={}
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
	checks+=1
	if not ok:failures.append(label);print("FAIL ",label)
func run():
	var game:Node=load("res://scripts/main.gd").new();root.add_child(game)
	game.front_menu.selected_map="archive:alfa-aglae-souza";game._start_from_title()
	for i in 6:await physics_frame;await process_frame
	var map:Node=game.active_sandbox_map;var play:Node=map.gameplay_r8;var actor:Node=game.player
	var palette:Array=[]
	for level in range(1,6):palette.append(preload("res://scripts/weapon_rarity_r6.gd").color(level).to_html())
	facts.palette=palette;check(palette[0]=="ffffffff" and palette[1]=="4b9bffff" and palette[2]=="bd69ffff" and palette[3]=="ffc542ff" and palette[4]=="ff415cff","white blue purple gold red rarity order")
	game.field_menu.settings_page="pause";check(not game.field_menu._labels().has("SKINS"),"in-game menu has no skin selector")
	var glass=0
	for row in map._details_r8.panels:
		if row.glass:glass+=1
	facts.alpha_glass=glass;check(glass>0,"Alpha baked template glass is recognized")
	# Exercise normal player physics; an instantaneous velocity assignment is insufficient.
	var chosen:=Vector3.INF
	for at in play.pads:
		var roof:Dictionary=map.get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(at+Vector3.UP*.3,at+Vector3.UP*68,1))
		if roof.is_empty() and map.safe_spawn_r8(at+Vector3.UP*.12):chosen=at;break
	check(chosen.is_finite(),"Alpha has an actual source pad with clear high-flight space")
	if not chosen.is_finite():chosen=map.spawn
	var start:Vector3=chosen+Vector3.UP*.12;actor.global_position=start;actor.reset_motion();actor.input_enabled=true;play._launch_cooldown=0
	facts.pad_position=str(chosen);facts.source_pads=play.pads.size()
	var highest:float=start.y;var deadline:int=Time.get_ticks_msec()+2200
	while Time.get_ticks_msec()<deadline:
		await physics_frame;highest=maxf(highest,actor.global_position.y)
	facts.pad_height=highest-start.y;facts.pad_end_velocity=str(actor.velocity);check(highest-start.y>25,"pad produces sustained high flight through real player ticks")
	# Arms-out intent must not replace an ascending pad impulse with downward glide velocity.
	actor.set_physics_process(false);actor.global_position=start;actor.reset_motion();actor.launch_from_pad_r10(45)
	var no_glide=true
	for frame in 40:
		await physics_frame;actor.apply_locomotion_motion(Vector3.ZERO,Vector3.FORWARD,true,false,1.0/90)
		if actor.gliding or actor.velocity.y<=0:no_glide=false
	check(no_glide,"arms-out glide intent preserves pad ascent")
	game._quitting=true;game.queue_free();for i in 5:await process_frame
	var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RUNTIME-R10.json",FileAccess.WRITE);file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"xr":"off"},"\t"));print("RUNTIME_R10 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
