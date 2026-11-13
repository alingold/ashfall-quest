extends Node3D
## Compact match layer above existing shield/health. Two shallow single-line notices.
var game:Node
var net:Node
var left_fill:MeshInstance3D
var right_fill:MeshInstance3D
var left_text:Label3D
var right_text:Label3D
var cap:Label3D
var clock:Label3D
var feeds:Array[Label3D]=[]
var feed_panels:Array[MeshInstance3D]=[]
var team_labels:Array[Label3D]=[]
var life_notice_r13:Label3D
var life_panel_r13:MeshInstance3D
func setup(g:Node,n:Node)->void:
	game=g;net=n;name="CompactMatchHUDR3";process_priority=111
	quad(Vector2(.34,.014),Vector3(-.02,-.389,-.804),Color("111c27"))
	left_fill=quad(Vector2(.116,.014),Vector3(-.097,-.389,-.802),Color("00aadd"))
	right_fill=quad(Vector2(.116,.014),Vector3(.057,-.389,-.802),Color("ed2335"))
	quad(Vector2(.030,.016),Vector3(-.175,-.389,-.800),Color("007ba4"))
	quad(Vector2(.030,.016),Vector3(.135,-.389,-.800),Color("c91527"))
	quad(Vector2(.039,.017),Vector3(-.02,-.389,-.799),Color("101821"))
	left_text=label("0",Vector3(-.175,-.389,-.797),.011)
	right_text=label("0",Vector3(.135,-.389,-.797),.011)
	cap=label("25",Vector3(-.02,-.389,-.796),.013)
	clock=label("",Vector3(-.02,-.406,-.797),.0085)
	team_labels.append(label("",Vector3(-.097,-.389,-.795),.0075))
	team_labels.append(label("",Vector3(.057,-.389,-.795),.0075))
	for i in 2:
		feed_panels.append(quad(Vector2(.34,.010),Vector3(-.02,-.365+i*.012,-.803),Color("0b1d29")))
		feeds.append(label("",Vector3(-.02,-.365+i*.012,-.798),.0105))
	life_panel_r13=quad(Vector2(.34,.013),Vector3(-.02,-.333,-.803),Color("17212a"))
	life_notice_r13=label("",Vector3(-.02,-.333,-.798),.0095)
	life_panel_r13.hide();life_notice_r13.hide()
func quad(size:Vector2,at:Vector3,color:Color)->MeshInstance3D:
	var mesh:=MeshInstance3D.new();mesh.mesh=QuadMesh.new();mesh.mesh.size=size;mesh.position=at;mesh.layers=1<<19;mesh.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var mat:=StandardMaterial3D.new();mat.albedo_color=color;mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.no_depth_test=true;mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;mat.render_priority=114;mesh.material_override=mat;add_child(mesh);return mesh
func label(text:String,at:Vector3,height:float)->Label3D:
	var view:=Label3D.new();view.text=text;view.position=at;view.font_size=32;view.pixel_size=height/32;view.vertical_alignment=VERTICAL_ALIGNMENT_CENTER;view.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;view.outline_size=1;view.render_priority=116;view.outline_render_priority=115;view.layers=1<<19;view.no_depth_test=true;add_child(view);return view
func score_rows()->Array:
	var result:Array=[]
	for key in net.match_scores:result.append({"key":int(key),"score":int(net.match_scores[key])})
	for id in net.players:
		if net.players[id].spectator:continue
		var key:int=int(net.players[id].team) if int(net.rules.teams)>0 else int(id)
		if result.filter(func(row):return row.key==key).is_empty():result.append({"key":key,"score":0})
	result.sort_custom(func(a,b):return a.score>b.score if a.score!=b.score else a.key<b.key)
	var own:int=int(net.players.get(net.me(),{}).get("team",0)) if int(net.rules.teams)>0 else net.me()
	var own_rows:Array=result.filter(func(row):return row.key==own)
	var others:Array=result.filter(func(row):return row.key!=own)
	return [own_rows[0] if not own_rows.is_empty() else {"key":own,"score":0},others[0] if not others.is_empty() else {"key":-1,"score":0}]
func _process(_delta:float)->void:
	visible=net.active and net.stage=="match" and not game.front_end_active
	if not visible:return
	var own:Dictionary=net.players.get(net.me(),{});var life:String=str(own.get("life","alive"))
	life_notice_r13.visible=life in ["downed","eliminated"];life_panel_r13.visible=life_notice_r13.visible
	if life_notice_r13.visible:
		life_notice_r13.text="YOU ARE A GHOST · WAITING FOR REVIVE" if life=="downed" else ("YOU DIED · RESPAWN %.1fs"%maxf(0,float(own.get("respawn_at",0))-net.now()) if net.rules.mode=="respawn" else "ELIMINATED · WAITING FOR ROUND")
		life_notice_r13.pixel_size=minf(.0095/32,.34/maxf(1,ThemeDB.fallback_font.get_string_size(life_notice_r13.text,HORIZONTAL_ALIGNMENT_LEFT,-1,32).x))
	var hud:Node=game.combat_hud;scale=Vector3(hud.hud_size,hud.hud_size,1);position.y=hud.SHIELD_Y*(1-hud.hud_size)
	position.x=0.0 if hud.style=="reference" else .02*hud.hud_size
	var rows:=score_rows();var limit:int=int(net.rules.score_limit)
	left_text.text=str(rows[0].score);right_text.text=str(rows[1].score);cap.text=str(limit) if limit>0 else "∞"
	for i in 2:team_labels[i].text=""
	for i in 2:
		var ratio:=clampf(float(rows[i].score)/limit,0,1) if limit>0 else 0.0
		var fill:MeshInstance3D=left_fill if i==0 else right_fill
		fill.scale.x=maxf(.0001,ratio);fill.position.x=-.155+.058*ratio if i==0 else .115-.058*ratio
	var remaining:=ceili(maxf(0,float(net.rules.time_limit)-(net.now()-net.match_started)))
	clock.text="TIME %d:%02d"%[int(remaining)/60,int(remaining)%60] if int(net.rules.time_limit)>0 else "NO TIME CAP"
	if not net.zone.is_empty():
		clock.text+="  ·  "+("FINAL ZONE" if net.zone.final else "ZONE %s"%("MOVING" if net.now()>=float(net.zone.hold_until) else "%ds"%ceili(float(net.zone.hold_until)-net.now())))
		var origin:Vector3=preload("res://scripts/map_catalog1000.gd").info(str(net.rules.map)).origin
		var outside:bool=not preload("res://scripts/square_zone_r3.gd").contains(preload("res://scripts/square_zone_r3.gd").current(net.zone,net.now()),game.player.global_position,origin)
		if outside:clock.text+=" · OUTSIDE %.0f HP/s"%float(net.rules.zone_damage)
		clock.modulate=Color("ff666e") if outside else Color.WHITE
	clock.pixel_size=minf(.0085/32,.34/maxf(1,ThemeDB.fallback_font.get_string_size(clock.text,HORIZONTAL_ALIGNMENT_LEFT,-1,32).x))
	var current:Array=net.combat_feed.filter(func(entry):return entry.epoch==net.epoch and float(entry.until)>net.now())
	for i in feeds.size():
		var view:Label3D=feeds[i];view.text=""
		feed_panels[i].visible=i<current.size()
		if i>=current.size():continue
		var entry:Dictionary=current[i];var catalog:int=int(entry.catalog)
		var weapon:String=game.weapon.weapon_names[catalog] if catalog>=0 and catalog<30 else ("zone" if entry.attacker_name=="ZONE" else "world")
		view.text="%s %s %s with %s"%[str(entry.attacker_name).left(14),"knocked down" if entry.downed else "eliminated",str(entry.victim_name).left(14),weapon]
		view.pixel_size=minf(.0105/32,.34/maxf(1,ThemeDB.fallback_font.get_string_size(view.text,HORIZONTAL_ALIGNMENT_LEFT,-1,32).x))
		view.modulate=Color("85eaff") if net.allied(net.me(),int(entry.attacker)) else Color("ffb3b8")
