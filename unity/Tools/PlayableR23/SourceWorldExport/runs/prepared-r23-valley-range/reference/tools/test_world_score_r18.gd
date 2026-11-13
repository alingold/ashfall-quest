extends SceneTree
class FakeNet extends Node:
 var rules={"score_limit":25,"time_limit":120}
 var players={}
 var active=true
 var stage="match"
 var match_started=10.0
 var match_scores={0:7,1:11}
 func now():return 40.9
func _initialize():
 var net=FakeNet.new()
 var helper=load("res://scripts/world_score_state_r18.gd")
 var state=helper.snapshot(net)
 assert(state.scores==[7,11] and state.cap==25 and state.seconds==89)
 net.match_scores[1]=12
 assert(helper.snapshot(net).scores[1]==12)
 net.stage="warmup"
 assert(helper.snapshot(net).seconds==120)
 net.stage="match";net.match_started=-200
 assert(helper.snapshot(net).seconds==0)
 net.free()
 print("R18 score authority snapshot PASS")
 quit()
