extends Node3D
## Shared authoritative canvas on measured original map surfaces; no gameplay ownership.
class ScoreCanvas extends Control:
 var game:Node
 var strong:FontVariation=FontVariation.new()
 func _row(font:Font,row:Dictionary,y:float,height:float,player:bool):
  var cyan=Color("43c6ee");var white=Color("eaf5fa")
  draw_string(font,Vector2(42,y+38),str(row.rank),HORIZONTAL_ALIGNMENT_LEFT,-1,28,white)
  # No exported/current-skin portrait thumbnail is available: use an explicit initial badge.
  draw_rect(Rect2(110,y+5,48,48),Color("0b344a"));draw_rect(Rect2(110,y+5,48,48),Color("54788b"),false,1)
  var initial:String=str(row.name).left(1).to_upper() if player else str(int(row.team)+1)
  draw_string(font,Vector2(110,y+40),initial,HORIZONTAL_ALIGNMENT_CENTER,48,28,white)
  draw_string(font,Vector2(182,y+36),str(row.name).left(24),HORIZONTAL_ALIGNMENT_LEFT,550,31,cyan)
  draw_string(font,Vector2(780,y+39),str(row.score),HORIZONTAL_ALIGNMENT_RIGHT,180,38,white)
  if player:draw_string(font,Vector2(182,y+58),"K %d   D %d"%[int(row.kills),int(row.deaths)],HORIZONTAL_ALIGNMENT_LEFT,-1,19,Color("90b0c1"))
  draw_line(Vector2(40,y+height-1),Vector2(982,y+height-1),Color("14384b"),1)
 func _draw():
  if game==null:return
  var net:Node=game.network_bridge.net;var font:Font=ThemeDB.fallback_font
  if strong.base_font==null:strong.base_font=font;strong.variation_embolden=.8
  var state:Dictionary=preload("res://scripts/world_score_state_r18.gd").snapshot(net)
  var white=Color("eaf5fa")
  draw_rect(Rect2(0,0,1024,1280),Color("032237"));draw_rect(Rect2(8,8,1008,1264),Color("54788b"),false,3)
  draw_rect(Rect2(12,12,1000,94),Color("072b40"))
  draw_string(strong,Vector2(32,78),state.header,HORIZONTAL_ALIGNMENT_CENTER,960,48,white)
  draw_line(Vector2(24,108),Vector2(1000,108),white,3)
  var layout_helper=preload("res://scripts/world_score_layout_r18.gd")
  var clock:String=layout_helper.clock_text(state);var cap:String=layout_helper.cap_text(state)
  draw_string(font,Vector2(40,156),"%s    TIME %s    CAP %s"%[state.stage.to_upper() if state.active else "PRACTICE",clock,cap],HORIZONTAL_ALIGNMENT_LEFT,-1,28,white)
  if not state.active:
   draw_string(strong,Vector2(40,260),"EXPLORE / PRACTICE",HORIZONTAL_ALIGNMENT_LEFT,-1,36,white)
   draw_string(font,Vector2(40,330),"Host a session to keep match scores.",HORIZONTAL_ALIGNMENT_LEFT,-1,29,Color("90b0c1"));return
  var plan:Dictionary=layout_helper.layout(state)
  for entry in plan.primary:_row(font,entry.row,entry.y,entry.height,state.teams==0)
  if plan.header_y>=0:draw_string(strong,Vector2(40,plan.header_y),"PLAYERS",HORIZONTAL_ALIGNMENT_LEFT,-1,27,white)
  for entry in plan.players:_row(font,entry.row,entry.y,entry.height,true)
var viewport:SubViewport
var canvas:ScoreCanvas
var faces:Array[MeshInstance3D]=[]
var _clock=0.0
static func display_material(texture:Texture2D)->StandardMaterial3D:
 var mat=StandardMaterial3D.new();mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.albedo_texture=texture;mat.cull_mode=BaseMaterial3D.CULL_DISABLED
 # Board-local late transparent pass with alpha1; opaque world depth still wins.
 mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA;mat.render_priority=100;mat.no_depth_test=false;mat.depth_draw_mode=BaseMaterial3D.DEPTH_DRAW_ALWAYS
 return mat
func setup(game:Node,rows:Array)->void:
 name="AuthoredWorldScoreR9";viewport=SubViewport.new();viewport.size=Vector2i(1024,1280);viewport.disable_3d=true;viewport.render_target_update_mode=SubViewport.UPDATE_DISABLED;add_child(viewport)
 canvas=ScoreCanvas.new();canvas.game=game;canvas.size=Vector2(1024,1280);viewport.add_child(canvas)
 var mat=display_material(viewport.get_texture())
 for row in rows:
  var face=MeshInstance3D.new();face.name="SourceScoreFaceR9";face.mesh=row;face.layers=int(row.get_meta("source_layers_r18",1));face.material_override=mat;face.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF;add_child(face)
  for group in row.get_meta("source_groups_r18",[]):face.add_to_group(group)
  faces.append(face)
 _process(1)
func _process(delta:float)->void:
 _clock+=delta
 if _clock<1:return
 _clock=0;canvas.queue_redraw();viewport.render_target_update_mode=SubViewport.UPDATE_ONCE
