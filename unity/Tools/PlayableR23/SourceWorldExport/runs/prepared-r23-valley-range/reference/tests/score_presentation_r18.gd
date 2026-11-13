extends SceneTree
class Fixture extends Node:
 var rules={"teams":0,"mode":"respawn","score_limit":0,"time_limit":0}
 var active=true
 var stage="match"
 var match_started=10.0
 var match_scores={1:4,2:9}
 var players={1:{"name":"Alpha","team":-1,"score":4,"kills":4,"deaths":2},2:{"name":"Bravo","team":-1,"score":9,"kills":9,"deaths":3},7:{"name":"Spectator","spectator":true,"team":-1,"score":999}}
 func now():return 40.9
func _initialize():
 var helper=load("res://scripts/world_score_state_r18.gd");var net=Fixture.new();var original=net.players.duplicate(true)
 var ffa=helper.snapshot(net)
 assert(ffa.teams==0 and ffa.header=="PLAYER SCORES" and ffa.rows.size()==2)
 assert(ffa.rows[0].id==2 and ffa.rows[0].name=="Bravo" and ffa.rows[0].score==9 and ffa.rows[0].rank==1)
 assert(ffa.rows[1].id==1 and ffa.rows[1].score==4 and ffa.scores_by_id[1]==4 and ffa.scores_by_id[2]==9)
 assert(ffa.time_unlimited and ffa.score_unlimited and ffa.seconds==0)
 assert(net.players==original,"Presentation may not mutate authority rows")
 net.match_scores={1:4,2:4};net.players[1].name="same";net.players[2].name="SAME"
 assert(helper.snapshot(net).rows[0].id==1,"Equal-name/score ranks must be stable by peer ID")
 net.rules.teams=4;net.rules.score_limit=25;net.rules.time_limit=120
 net.match_scores={0:2,1:8,2:4,3:11};net.players[1].team=0;net.players[2].team=2
 var teams=helper.snapshot(net)
 assert(teams.rows.size()==4 and teams.header=="TEAM SCORES" and teams.scores==[2,8,4,11])
 assert(teams.rows[0].id==3 and teams.rows[0].score==11 and teams.rows[1].id==1 and teams.rows[1].score==8)
 assert(not teams.time_unlimited and not teams.score_unlimited and teams.seconds==89)
 net.players[2].team=5;net.match_scores[5]=6
 assert(helper.snapshot(net).scores_by_id[5]==6,"Current roster teams must remain representable")
 var layout=load("res://scripts/world_score_layout_r18.gd")
 assert(layout.clock_text(ffa)=="UNLIMITED" and layout.cap_text(ffa)=="UNLIMITED")
 assert(layout.clock_text(teams)=="01:29" and layout.cap_text(teams)=="25")
 var maximum=teams.duplicate(true);maximum.teams=8;maximum.rows=[];maximum.players=[]
 for id in 8:
  maximum.rows.append({"id":id,"name":"TEAM %d"%(id+1),"score":id,"team":id,"rank":id+1})
  maximum.players.append({"id":id+1,"name":"Player %d"%(id+1),"score":id,"team":id,"kills":id,"deaths":0,"rank":id+1})
 var plan=layout.layout(maximum)
 assert(plan.primary.size()==8 and plan.players.size()==8 and plan.end_y<=1240)
 assert(plan.players[7].row.id==8 and plan.players[7].y+plan.players[7].height<=1240)
 var ffa_plan=layout.layout(ffa)
 assert(ffa_plan.primary.size()==2 and ffa_plan.players.is_empty())
 var board=load("res://scripts/archive_world_score_r9.gd");var material=board.display_material(null)
 assert(material.transparency==BaseMaterial3D.TRANSPARENCY_ALPHA and material.render_priority==100)
 assert(not material.no_depth_test and material.depth_draw_mode==BaseMaterial3D.DEPTH_DRAW_ALWAYS)
 net.free();print("R18_MODE_AWARE_SCORE_PRESENTATION_PASS FFA peer1/2, four teams, roster teams, stable ranks, unlimited cap/time, opaque-world depth retained; eight teams plus eight players fit; ASCII unlimited labels");quit()
